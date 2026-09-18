using System.Security.Claims;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// M1 Catálogo: periodos contables. Cerrar/reabrir invocan los procedimientos
/// de database/04_procedures.sql, que consolidan SaldoCuentaPeriodo de forma
/// inmutable y validan el perfil autorizado (Contador/Administrador del
/// sistema para cerrar; solo Administrador del sistema para reabrir) — esa
/// autorización vive en la base de datos, no se duplica aquí.
/// </summary>
[ApiController]
[Route("api/periodos")]
[Authorize]
public class PeriodosContablesController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public PeriodosContablesController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
        => Ok(await _db.PeriodosContables.OrderByDescending(p => p.FechaInicio).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var periodo = await _db.PeriodosContables.FindAsync(id);
        return periodo is null ? NotFound() : Ok(periodo);
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Crear([FromBody] PeriodoContable periodo)
    {
        if (periodo.FechaFin <= periodo.FechaInicio)
        {
            return BadRequest(new { error = "La fecha de fin debe ser posterior a la fecha de inicio." });
        }
        var conflicto = await BuscarPeriodoSolapadoAsync(periodo, excluirId: null);
        if (conflicto is not null)
        {
            return BadRequest(new { error = $"El rango de fechas se solapa con el periodo '{conflicto.Nombre}'." });
        }

        periodo.Id = 0;
        periodo.Estado = PeriodoContable.EstadoAbierto; // se ignora cualquier valor enviado por el cliente.
        _db.PeriodosContables.Add(periodo);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obtener), new { id = periodo.Id }, periodo);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] PeriodoContable cambios)
    {
        var periodo = await _db.PeriodosContables.FindAsync(id);
        if (periodo is null)
        {
            return NotFound();
        }
        if (periodo.Estado == PeriodoContable.EstadoCerrado)
        {
            return BadRequest(new { error = "No se puede editar un periodo cerrado." });
        }
        if (cambios.FechaFin <= cambios.FechaInicio)
        {
            return BadRequest(new { error = "La fecha de fin debe ser posterior a la fecha de inicio." });
        }
        var conflicto = await BuscarPeriodoSolapadoAsync(cambios, excluirId: id);
        if (conflicto is not null)
        {
            return BadRequest(new { error = $"El rango de fechas se solapa con el periodo '{conflicto.Nombre}'." });
        }

        periodo.Nombre = cambios.Nombre;
        periodo.FechaInicio = cambios.FechaInicio;
        periodo.FechaFin = cambios.FechaFin;
        await _db.SaveChangesAsync();
        return Ok(periodo);
    }

    /// <summary>
    /// Busca un periodo (distinto de <paramref name="excluirId"/>) cuyo rango de
    /// fechas se solape con el de <paramref name="periodo"/>. Condición de solapamiento
    /// estándar: existente.FechaInicio &lt;= nuevo.FechaFin y existente.FechaFin &gt;= nuevo.FechaInicio.
    /// </summary>
    private async Task<PeriodoContable?> BuscarPeriodoSolapadoAsync(PeriodoContable periodo, int? excluirId)
    {
        return await _db.PeriodosContables
            .Where(p => excluirId == null || p.Id != excluirId)
            .Where(p => p.FechaInicio <= periodo.FechaFin && p.FechaFin >= periodo.FechaInicio)
            .FirstOrDefaultAsync();
    }

    [HttpPost("{id:int}/cerrar")]
    public Task<IActionResult> Cerrar(int id) =>
        EjecutarTransicionAsync(id, usuarioId => _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_cerrar_periodo({id}, {usuarioId})"));

    [HttpPost("{id:int}/reabrir")]
    public Task<IActionResult> Reabrir(int id) =>
        EjecutarTransicionAsync(id, usuarioId => _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_reabrir_periodo({id}, {usuarioId})"));

    private async Task<IActionResult> EjecutarTransicionAsync(int periodoId, Func<int, Task> ejecutarProcedimiento)
    {
        if (!await _db.PeriodosContables.AnyAsync(p => p.Id == periodoId))
        {
            return NotFound();
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await ejecutarProcedimiento(usuarioId);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            // RAISE EXCEPTION del procedimiento: el perfil del usuario no está autorizado.
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.MessageText });
        }

        var periodo = await _db.PeriodosContables.FindAsync(periodoId);
        return Ok(periodo);
    }
}
