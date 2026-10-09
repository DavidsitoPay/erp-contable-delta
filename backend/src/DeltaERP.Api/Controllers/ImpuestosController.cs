using System.Security.Claims;
using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/impuestos")]
[Authorize]
public class ImpuestosController : ControllerBase
{
    private const string MensajeMismaFecha = "Ya existe una versión de ese impuesto con la misma fecha de inicio.";

    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;

    public ImpuestosController(DeltaErpDbContext db, AuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateOnly? vigenteEn, [FromQuery] string? aplicaA, [FromQuery] string? tipo, [FromQuery] bool incluirInactivos = false)
    {
        var query = _db.Impuestos.AsQueryable();
        query = string.IsNullOrWhiteSpace(tipo)
            ? query.Where(i => i.Tipo != Impuesto.TipoLegado)
            : query.Where(i => i.Tipo == tipo);
        if (!incluirInactivos)
        {
            query = query.Where(i => i.Activo);
        }
        if (aplicaA is Impuesto.AmbitoVentas or Impuesto.AmbitoCompras)
        {
            query = query.Where(i => i.AplicaA == aplicaA || i.AplicaA == Impuesto.AmbitoAmbos);
        }
        if (vigenteEn is { } fecha)
        {
            query = query.Where(i => i.VigenteDesde <= fecha && (i.VigenteHasta == null || i.VigenteHasta >= fecha));
        }

        var impuestos = await query.OrderBy(i => i.Tipo).ThenBy(i => i.Nombre).ToListAsync();
        var enUso = await IdsEnUsoAsync();
        return Ok(impuestos.Select(i => ImpuestoRespuesta.Desde(i, enUso.Contains(i.Id))));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var impuesto = await _db.Impuestos.FindAsync(id);
        return impuesto is null ? NotFound() : Ok(ImpuestoRespuesta.Desde(impuesto, (await IdsEnUsoAsync()).Contains(id)));
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Crear([FromBody] DatosImpuesto datos)
    {
        var error = ImpuestoRules.ValidarAlta(datos);
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        if (await _db.Impuestos.AnyAsync(i => i.Codigo == datos.Codigo && i.VigenteDesde == datos.VigenteDesde))
        {
            return Conflict(new { error = MensajeMismaFecha });
        }

        var impuesto = new Impuesto();
        Aplicar(impuesto, datos);
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var conflicto = await GuardarAsync(usuarioId, "crear_impuesto", async () =>
        {
            _db.Impuestos.Add(impuesto);
            await _db.SaveChangesAsync();
            return $"Impuesto {impuesto.Codigo} (id {impuesto.Id}) creado, tasa {impuesto.Tasa}, vigente desde {ImpuestoRules.Iso(impuesto.VigenteDesde)}";
        });
        return conflicto ?? CreatedAtAction(nameof(Obtener), new { id = impuesto.Id }, ImpuestoRespuesta.Desde(impuesto, false));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] DatosImpuesto cambios)
    {
        var impuesto = await _db.Impuestos.FindAsync(id);
        if (impuesto is null)
        {
            return NotFound();
        }

        var datos = cambios with
        {
            Codigo = impuesto.Codigo,
            Tipo = cambios.Tipo ?? impuesto.Tipo,
            GeneraCredito = cambios.GeneraCredito ?? impuesto.GeneraCredito,
            VigenteDesde = cambios.VigenteDesde ?? impuesto.VigenteDesde,
            Activo = cambios.Activo ?? impuesto.Activo,
        };
        var error = ImpuestoRules.ValidarAlta(datos);
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        if (await _db.Impuestos.AnyAsync(i => i.Id != id && i.Codigo == impuesto.Codigo && i.VigenteDesde == datos.VigenteDesde))
        {
            return Conflict(new { error = MensajeMismaFecha });
        }

        Aplicar(impuesto, datos);
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var conflicto = await GuardarAsync(usuarioId, "actualizar_impuesto", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Impuesto {impuesto.Codigo} (id {id}) actualizado, tasa {impuesto.Tasa}, activo {impuesto.Activo}";
        });
        return conflicto ?? Ok(ImpuestoRespuesta.Desde(impuesto, (await IdsEnUsoAsync()).Contains(id)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Desactivar(int id)
    {
        var impuesto = await _db.Impuestos.FindAsync(id);
        if (impuesto is null)
        {
            return NotFound();
        }

        impuesto.Activo = false;
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await GuardarAsync(usuarioId, "desactivar_impuesto", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Impuesto {impuesto.Codigo} (id {id}) desactivado";
        });
        return NoContent();
    }

    private static void Aplicar(Impuesto impuesto, DatosImpuesto datos)
    {
        impuesto.Codigo = datos.Codigo!;
        impuesto.Nombre = datos.Nombre!.Trim();
        impuesto.Tipo = datos.Tipo!;
        impuesto.Tasa = datos.Tasa;
        impuesto.AplicaA = datos.AplicaA!;
        impuesto.GeneraCredito = ImpuestoRules.ResolverGeneraCredito(datos);
        impuesto.ArticuloLegal = datos.ArticuloLegal!.Trim();
        impuesto.Nota = datos.Nota;
        impuesto.VigenteDesde = datos.VigenteDesde!.Value;
        impuesto.VigenteHasta = datos.VigenteHasta;
        impuesto.Activo = datos.Activo ?? true;
    }

    private async Task<ObjectResult?> GuardarAsync(int usuarioId, string accion, Func<Task<string>> operacion)
    {
        try
        {
            await _auditoria.EjecutarAsync(usuarioId, accion, "impuesto", operacion);
            return null;
        }
        catch (DbUpdateException ex) when (ErroresPostgres.TraducirActualizacionAsync(ex) is { } conflicto)
        {
            return conflicto;
        }
    }

    private async Task<HashSet<int>> IdsEnUsoAsync()
    {
        var enCxC = await _db.LineasDocumentoCxC.Select(l => l.ImpuestoId).Distinct().ToListAsync();
        var enCxP = await _db.LineasDocumentoCxP.Select(l => l.ImpuestoId).Distinct().ToListAsync();
        return enCxC.Concat(enCxP).ToHashSet();
    }
}
