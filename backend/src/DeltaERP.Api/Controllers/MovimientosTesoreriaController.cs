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
[Route("api/movimientos-tesoreria")]
[Authorize(Roles = Roles.GestionTesoreria)]
public class MovimientosTesoreriaController : ControllerBase
{
    private readonly DeltaErpDbContext _db;
    private readonly TesoreriaService _tesoreria;

    public MovimientosTesoreriaController(DeltaErpDbContext db, TesoreriaService tesoreria)
    {
        _db = db;
        _tesoreria = tesoreria;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? cuentaBancariaId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] string? origen)
    {
        if (desde > hasta)
        {
            return BadRequest(new { error = "La fecha inicial no puede ser posterior a la final." });
        }

        var movimientos = await _db.MovimientosTesoreria
            .Where(m => cuentaBancariaId == null || m.CuentaBancariaId == cuentaBancariaId)
            .Where(m => desde == null || m.Fecha >= desde)
            .Where(m => hasta == null || m.Fecha <= hasta)
            .Where(m => origen == null || m.Origen == origen)
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Select(m => new MovimientoDto(
                m.Id, m.CuentaBancariaId, m.Fecha, m.Tipo, m.Monto, m.Descripcion, m.Referencia, m.Origen, m.AsientoId,
                m.TransferenciaId, m.ReciboPagoId, m.PagoProveedorId,
                _db.DetallesConciliacion.Any(d => d.MovimientoId == m.Id
                    && _db.ConciliacionesBancarias.Any(c => c.Id == d.ConciliacionId && c.Estado == ConciliacionBancaria.EstadoConciliado))))
            .ToListAsync();
        return Ok(movimientos);
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] NuevoMovimientoManual solicitud)
    {
        var (movimiento, error) = await _tesoreria.RegistrarManualAsync(solicitud, this.UsuarioId());
        return error is not null
            ? BadRequest(new { error })
            : CreatedAtAction(nameof(Listar), new { cuentaBancariaId = movimiento!.CuentaBancariaId }, movimiento);
    }

    [HttpPost("transferencias")]
    public async Task<IActionResult> Transferir([FromBody] NuevaTransferencia solicitud)
    {
        var (resultado, error) = await _tesoreria.RegistrarTransferenciaAsync(solicitud, this.UsuarioId());
        return error is not null
            ? BadRequest(new { error })
            : CreatedAtAction(nameof(Listar), new { cuentaBancariaId = solicitud.CuentaOrigenId }, resultado);
    }
}
