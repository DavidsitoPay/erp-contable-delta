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
[Route("api/conciliaciones")]
[Authorize(Roles = Roles.GestionTesoreria)]
public class ConciliacionesController : ControllerBase
{
    private const string MensajeNoPendiente = "La conciliación no está pendiente; no admite cambios.";

    private readonly DeltaErpDbContext _db;
    private readonly TesoreriaService _tesoreria;
    private readonly AuditoriaService _auditoria;

    public ConciliacionesController(DeltaErpDbContext db, TesoreriaService tesoreria, AuditoriaService auditoria)
    {
        _db = db;
        _tesoreria = tesoreria;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? cuentaBancariaId, [FromQuery] string? estado) =>
        Ok(await _db.ResumenesConciliacion
            .Where(r => cuentaBancariaId == null || r.CuentaBancariaId == cuentaBancariaId)
            .Where(r => estado == null || r.Estado == estado)
            .OrderByDescending(r => r.Id)
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var resumen = await ObtenerResumenAsync(id);
        if (resumen is null)
        {
            return NotFound();
        }

        var marcados = await (
            from d in _db.DetallesConciliacion
            join m in _db.MovimientosTesoreria on d.MovimientoId equals m.Id
            where d.ConciliacionId == id
            orderby m.Fecha, m.Id
            select m).ToListAsync();
        var disponibles = resumen.Estado == ConciliacionBancaria.EstadoPendiente
            ? await _db.MovimientosTesoreria
                .Where(m => m.CuentaBancariaId == resumen.CuentaBancariaId
                    && m.Fecha <= resumen.Fecha
                    && m.Origen != MovimientoTesoreria.OrigenApertura
                    && !_db.DetallesConciliacion.Any(d => d.MovimientoId == m.Id))
                .OrderBy(m => m.Fecha)
                .ThenBy(m => m.Id)
                .ToListAsync()
            : new List<MovimientoTesoreria>();
        return Ok(new ConciliacionDetalleDto(resumen, marcados, disponibles));
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] NuevaConciliacion solicitud)
    {
        var (_, errorCuenta) = await _tesoreria.ValidarCuentaOperableAsync(solicitud.CuentaBancariaId);
        if (errorCuenta is not null)
        {
            return BadRequest(new { error = errorCuenta });
        }
        var (periodoId, errorCorte) = await ValidarCorteAsync(solicitud.Fecha, solicitud.SaldoExtracto);
        if (errorCorte is not null)
        {
            return BadRequest(new { error = errorCorte });
        }

        var conciliacion = new ConciliacionBancaria
        {
            CuentaBancariaId = solicitud.CuentaBancariaId,
            PeriodoId = periodoId,
            Fecha = solicitud.Fecha,
            SaldoExtracto = solicitud.SaldoExtracto,
        };
        _db.ConciliacionesBancarias.Add(conciliacion);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, "ux_conciliacion_pendiente_por_cuenta"))
        {
            return Conflict(new { error = "Ya existe una conciliación pendiente para esta cuenta bancaria." });
        }
        return CreatedAtAction(nameof(Obtener), new { id = conciliacion.Id }, await ObtenerResumenAsync(conciliacion.Id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, [FromBody] EditarConciliacion solicitud)
    {
        var (conciliacion, fallo) = await CargarPendienteAsync(id, MensajeNoPendiente);
        if (fallo is not null)
        {
            return fallo;
        }
        var (periodoId, errorCorte) = await ValidarCorteAsync(solicitud.Fecha, solicitud.SaldoExtracto);
        if (errorCorte is not null)
        {
            return BadRequest(new { error = errorCorte });
        }
        var hayMarcadosPosteriores = await (
            from d in _db.DetallesConciliacion
            join m in _db.MovimientosTesoreria on d.MovimientoId equals m.Id
            where d.ConciliacionId == id && m.Fecha > solicitud.Fecha
            select d.Id).AnyAsync();
        if (hayMarcadosPosteriores)
        {
            return Conflict(new { error = "Hay movimientos marcados posteriores a la nueva fecha de corte; desmárcalos primero." });
        }

        try
        {
            await _auditoria.EjecutarAsync(this.UsuarioId(), "editar_conciliacion", "conciliacionbancaria", async () =>
            {
                conciliacion!.Fecha = solicitud.Fecha;
                conciliacion.SaldoExtracto = solicitud.SaldoExtracto;
                conciliacion.PeriodoId = periodoId;
                await _db.SaveChangesAsync();
                return $"Conciliación {id} editada: corte {solicitud.Fecha:yyyy-MM-dd}, extracto {solicitud.SaldoExtracto}";
            });
        }
        catch (DbUpdateException ex)
        {
            var fallo_trig = ErroresPostgres.TraducirActualizacionAsync(ex);
            if (fallo_trig is not null)
            {
                return fallo_trig;
            }
            throw;
        }
        return Ok(await ObtenerResumenAsync(id));
    }

    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var (conciliacion, fallo) = await CargarPendienteAsync(id, "Solo se puede cancelar una conciliación pendiente.");
        if (fallo is not null)
        {
            return fallo;
        }

        try
        {
            await _auditoria.EjecutarAsync(this.UsuarioId(), "cancelar_conciliacion", "conciliacionbancaria", async () =>
            {
                var liberados = await _db.DetallesConciliacion.Where(d => d.ConciliacionId == id).ExecuteDeleteAsync();
                conciliacion!.Estado = ConciliacionBancaria.EstadoCancelada;
                await _db.SaveChangesAsync();
                return $"Conciliación {id} cancelada; {liberados} movimientos liberados";
            });
        }
        catch (DbUpdateException ex)
        {
            var fallo_trig = ErroresPostgres.TraducirActualizacionAsync(ex);
            if (fallo_trig is not null)
            {
                return fallo_trig;
            }
            throw;
        }
        return Ok(await ObtenerResumenAsync(id));
    }

    [HttpPost("{id:int}/movimientos")]
    public async Task<IActionResult> Marcar(int id, [FromBody] MarcarMovimiento solicitud)
    {
        var (conciliacion, fallo) = await CargarPendienteAsync(id, MensajeNoPendiente);
        if (fallo is not null)
        {
            return fallo;
        }
        var movimiento = await _db.MovimientosTesoreria.FindAsync(solicitud.MovimientoId);
        var error = TesoreriaRules.ValidarMarca(conciliacion!, movimiento);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        _db.DetallesConciliacion.Add(new DetalleConciliacion { ConciliacionId = id, MovimientoId = solicitud.MovimientoId });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, "uq_detalleconciliacion_movimiento"))
        {
            return Conflict(new { error = "El movimiento ya está incluido en una conciliación." });
        }
        catch (DbUpdateException ex)
        {
            var fallo_trig = ErroresPostgres.TraducirActualizacionAsync(ex);
            if (fallo_trig is not null)
            {
                return fallo_trig;
            }
            throw;
        }
        return Ok(await ObtenerResumenAsync(id));
    }

    [HttpDelete("{id:int}/movimientos/{movimientoId:int}")]
    public async Task<IActionResult> Desmarcar(int id, int movimientoId)
    {
        var (_, fallo) = await CargarPendienteAsync(id, MensajeNoPendiente);
        if (fallo is not null)
        {
            return fallo;
        }
        var detalle = await _db.DetallesConciliacion.FirstOrDefaultAsync(d => d.ConciliacionId == id && d.MovimientoId == movimientoId);
        if (detalle is null)
        {
            return NotFound();
        }

        _db.DetallesConciliacion.Remove(detalle);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var fallo_trig = ErroresPostgres.TraducirActualizacionAsync(ex);
            if (fallo_trig is not null)
            {
                return fallo_trig;
            }
            throw;
        }
        return Ok(await ObtenerResumenAsync(id));
    }

    [HttpPost("{id:int}/finalizar")]
    public async Task<IActionResult> Finalizar(int id)
    {
        var usuarioId = this.UsuarioId();
        var fallo = await ErroresPostgres.TraducirProcedimientoAsync(
            () => _db.Database.ExecuteSqlInterpolatedAsync($"CALL sp_finalizar_conciliacion({id}, {usuarioId})"));
        return fallo ?? Ok(await ObtenerResumenAsync(id));
    }

    private Task<ConciliacionResumen?> ObtenerResumenAsync(int id) =>
        _db.ResumenesConciliacion.FirstOrDefaultAsync(r => r.Id == id);

    private async Task<(ConciliacionBancaria? Conciliacion, IActionResult? Fallo)> CargarPendienteAsync(int id, string mensajeNoPendiente)
    {
        var conciliacion = await _db.ConciliacionesBancarias.FindAsync(id);
        if (conciliacion is null)
        {
            return (null, NotFound());
        }
        if (conciliacion.Estado != ConciliacionBancaria.EstadoPendiente)
        {
            return (null, Conflict(new { error = mensajeNoPendiente }));
        }
        return (conciliacion, null);
    }

    private async Task<(int PeriodoId, string? Error)> ValidarCorteAsync(DateOnly fecha, decimal saldoExtracto)
    {
        var error = TesoreriaRules.ValidarSaldoExtracto(saldoExtracto);
        if (error is not null)
        {
            return (0, error);
        }
        var periodo = await _tesoreria.BuscarPeriodoPorFechaAsync(fecha);
        return periodo is null ? (0, TesoreriaService.MensajeSinPeriodo(fecha)) : (periodo.Id, null);
    }
}
