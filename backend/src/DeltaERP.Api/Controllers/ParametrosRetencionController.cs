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
[Route("api/retenciones/parametros")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class ParametrosRetencionController : ControllerBase
{
    private static readonly string MensajeMismaFecha = VigenciaRules.MensajeMismaFecha("parámetro de retención");

    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;

    public ParametrosRetencionController(DeltaErpDbContext db, AuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? concepto, [FromQuery] string? codigo, [FromQuery] DateOnly? vigenteEn, [FromQuery] bool incluirInactivos = false)
    {
        var consulta = _db.ParametrosRetencion.AsQueryable();
        if (!incluirInactivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }
        if (!string.IsNullOrWhiteSpace(concepto))
        {
            consulta = consulta.Where(p => p.Concepto == concepto);
        }
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            consulta = consulta.Where(p => p.Codigo == codigo);
        }
        if (vigenteEn is { } dia)
        {
            consulta = consulta.Where(p => p.VigenteDesde <= dia && (p.VigenteHasta == null || p.VigenteHasta >= dia));
        }

        var parametros = await consulta.OrderBy(p => p.Codigo).ThenByDescending(p => p.VigenteDesde).ToListAsync();
        var enUso = await IdsEnUsoAsync();
        return Ok(parametros.Select(p => ParametroRetencionRespuesta.Desde(p, enUso.Contains(p.Id))));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var parametro = await _db.ParametrosRetencion.FindAsync(id);
        return parametro is null ? NotFound() : Ok(ParametroRetencionRespuesta.Desde(parametro, (await IdsEnUsoAsync()).Contains(id)));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Crear([FromBody] DatosParametroRetencion datos)
    {
        var error = ParametroRetencionRules.ValidarAlta(datos);
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        if (await _db.ParametrosRetencion.AnyAsync(p => p.Codigo == datos.Codigo && p.VigenteDesde == datos.VigenteDesde))
        {
            return Conflict(new { error = MensajeMismaFecha });
        }

        var parametro = Nuevo(datos);
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "crear_parametro_retencion", "parametroretencion", async () =>
        {
            _db.ParametrosRetencion.Add(parametro);
            await _db.SaveChangesAsync();
            return $"Parámetro de retención {parametro.Codigo} (id {parametro.Id}) creado, vigente desde {VigenciaRules.Iso(parametro.VigenteDesde)}";
        });
        return conflicto ?? CreatedAtAction(nameof(Obtener), new { id = parametro.Id }, ParametroRetencionRespuesta.Desde(parametro, false));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CambiosParametroRetencion cambios)
    {
        var parametro = await _db.ParametrosRetencion.FindAsync(id);
        if (parametro is null)
        {
            return NotFound();
        }
        var error = ParametroRetencionRules.ValidarCambio(cambios, parametro.VigenteDesde);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        if (!string.IsNullOrWhiteSpace(cambios.Nota))
        {
            parametro.Nota = cambios.Nota.Trim();
        }
        parametro.Activo = cambios.Activo ?? parametro.Activo;
        parametro.VigenteHasta = cambios.VigenteHasta;
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "actualizar_parametro_retencion", "parametroretencion", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Parámetro de retención {parametro.Codigo} (id {id}) actualizado, activo {parametro.Activo}, vigente hasta {parametro.VigenteHasta?.ToString("yyyy-MM-dd") ?? "sin fecha final"}";
        });
        return conflicto ?? Ok(ParametroRetencionRespuesta.Desde(parametro, (await IdsEnUsoAsync()).Contains(id)));
    }

    private static ParametroRetencion Nuevo(DatosParametroRetencion datos) => new()
    {
        Codigo = datos.Codigo ?? string.Empty,
        Concepto = datos.Concepto ?? string.Empty,
        Nombre = datos.Nombre?.Trim() ?? string.Empty,
        TasaBase = datos.TasaBase,
        TramoLimite = datos.TramoLimite,
        TasaExcedente = datos.TasaExcedente,
        UmbralMinimo = datos.UmbralMinimo ?? 0m,
        UmbralSobre = datos.UmbralSobre ?? ParametroRetencion.UmbralBase,
        DiaLimite = datos.DiaLimite,
        ArticuloLegal = datos.ArticuloLegal?.Trim() ?? string.Empty,
        Nota = string.IsNullOrWhiteSpace(datos.Nota) ? ParametroRetencion.NotaReferencial : datos.Nota.Trim(),
        VigenteDesde = datos.VigenteDesde ?? default,
        VigenteHasta = datos.VigenteHasta,
        Activo = datos.Activo ?? true,
    };

    private async Task<HashSet<int>> IdsEnUsoAsync() =>
        (await _db.Retenciones.Select(r => r.ParametroId).Distinct().ToListAsync()).ToHashSet();
}
