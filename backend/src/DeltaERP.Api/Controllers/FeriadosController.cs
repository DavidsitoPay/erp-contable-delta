using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/feriados")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class FeriadosController : ControllerBase
{
    private const string Tabla = "feriado";
    private const int LongitudMaximaNombre = 100;

    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly TimeProvider _tiempo;

    public FeriadosController(DeltaErpDbContext db, AuditoriaService auditoria, TimeProvider tiempo)
    {
        _db = db;
        _auditoria = auditoria;
        _tiempo = tiempo;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? anio)
    {
        if (anio is < 1900 or > 2200)
        {
            return BadRequest(new { error = "El año debe estar entre 1900 y 2200." });
        }
        var consulta = _db.Feriados.AsNoTracking();
        if (anio is { } a)
        {
            var desde = new DateOnly(a, 1, 1);
            var hasta = new DateOnly(a, 12, 31);
            consulta = consulta.Where(f => f.Fecha >= desde && f.Fecha <= hasta);
        }
        var filas = await consulta.OrderBy(f => f.Fecha).ToListAsync();
        return Ok(filas.Select(FeriadoDto.Desde));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Crear([FromBody] FeriadoDto datos)
    {
        var error = Validar(datos);
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        if (await _db.Feriados.AnyAsync(f => f.Fecha == datos.Fecha))
        {
            return Conflict(new { error = "Ya existe un feriado en esa fecha." });
        }
        var feriado = new Feriado
        {
            Fecha = datos.Fecha,
            Nombre = datos.Nombre!.Trim(),
            MedioDia = datos.MedioDia,
            CreadoPor = this.UsuarioId(),
            CreadoEn = _tiempo.GetUtcNow(),
        };
        _db.Feriados.Add(feriado);
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "crear_feriado", Tabla, async () =>
        {
            await _db.SaveChangesAsync();
            return Describir("creado", feriado);
        });
        return conflicto ?? Created($"/api/feriados?anio={feriado.Fecha.Year}", FeriadoDto.Desde(feriado));
    }

    [HttpPut("{fecha}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Actualizar(DateOnly fecha, [FromBody] FeriadoDto datos)
    {
        var feriado = await _db.Feriados.FindAsync(fecha);
        if (feriado is null)
        {
            return NotFound(new { error = "El feriado indicado no existe." });
        }
        var error = Validar(datos with { Fecha = fecha });
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        feriado.Nombre = datos.Nombre!.Trim();
        feriado.MedioDia = datos.MedioDia;
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "actualizar_feriado", Tabla, async () =>
        {
            await _db.SaveChangesAsync();
            return Describir("actualizado", feriado);
        });
        return conflicto ?? Ok(FeriadoDto.Desde(feriado));
    }

    [HttpDelete("{fecha}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Eliminar(DateOnly fecha)
    {
        var feriado = await _db.Feriados.FindAsync(fecha);
        if (feriado is null)
        {
            return NotFound(new { error = "El feriado indicado no existe." });
        }
        _db.Feriados.Remove(feriado);
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "eliminar_feriado", Tabla, async () =>
        {
            await _db.SaveChangesAsync();
            return Describir("eliminado", feriado);
        });
        return conflicto ?? (IActionResult)NoContent();
    }

    private static string? Validar(FeriadoDto datos)
    {
        if (datos.Fecha == default)
        {
            return "La fecha es obligatoria.";
        }
        return string.IsNullOrWhiteSpace(datos.Nombre) || datos.Nombre.Trim().Length > LongitudMaximaNombre
            ? "El nombre es obligatorio y admite hasta 100 caracteres."
            : null;
    }

    private static string Describir(string accion, Feriado f) =>
        $"Feriado {f.Fecha:yyyy-MM-dd} {accion}: {f.Nombre}{(f.MedioDia ? " (medio día, hábil)" : string.Empty)}";
}
