using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/libros")]
[Authorize]
public class LibrosController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public LibrosController(DeltaErpDbContext db)
    {
        _db = db;
    }

    // La vista solo suma asientos Confirmado/Anulado, aplica el signo según la naturaleza y acumula las subcuentas en cada cuenta de mayor.
    [HttpGet("balance-saldos")]
    public async Task<IActionResult> BalanceSaldos()
    {
        var balance = await _db.BalanceSaldos
            .OrderBy(b => b.Codigo)
            .ToListAsync();
        return Ok(balance);
    }

    [HttpGet("diario")]
    public async Task<IActionResult> LibroDiario([FromQuery] int periodoId)
    {
        var periodo = await _db.PeriodosContables.FindAsync(periodoId);
        if (periodo is null)
        {
            return BadRequest(new { error = "El periodo indicado no existe." });
        }

        var asientos = await _db.AsientosContables
            .Include(a => a.Lineas)
            .Where(a => a.PeriodoId == periodoId && AsientoContable.EstadosContabilizados.Contains(a.Estado))
            .OrderBy(a => a.Fecha).ThenBy(a => a.Numero)
            .ToListAsync();

        // Lookup único de cuentas usadas, no una consulta por línea.
        var idsCuenta = asientos.SelectMany(a => a.Lineas).Select(l => l.CuentaId).Distinct().ToList();
        var cuentas = await _db.CuentasContables
            .Where(c => idsCuenta.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var idsAsiento = asientos.Select(a => a.Id).ToList();
        var conCxC = await IdsComoConjuntoAsync(
            _db.DocumentosCxC.Where(d => d.AsientoId != null && idsAsiento.Contains(d.AsientoId.Value)).Select(d => d.AsientoId!.Value));
        var conCxP = await IdsComoConjuntoAsync(
            _db.DocumentosCxP.Where(d => d.AsientoId != null && idsAsiento.Contains(d.AsientoId.Value)).Select(d => d.AsientoId!.Value));
        var conTesoreria = await IdsComoConjuntoAsync(
            _db.MovimientosTesoreria.Where(m => idsAsiento.Contains(m.AsientoId)).Select(m => m.AsientoId));
        var porId = asientos.ToDictionary(a => a.Id);
        var reversaPorOriginal = asientos.Where(a => a.ReversaDeId != null).ToDictionary(a => a.ReversaDeId!.Value);

        var resultado = asientos.Select(a =>
        {
            var reversa = reversaPorOriginal.GetValueOrDefault(a.Id);
            var original = a.ReversaDeId is { } idOriginal ? porId.GetValueOrDefault(idOriginal) : null;
            var vinculos = new VinculosAsiento(conCxC.Contains(a.Id), conCxP.Contains(a.Id), conTesoreria.Contains(a.Id));
            return new
            {
                a.Id,
                a.Numero,
                a.Fecha,
                a.Monto,
                a.Estado,
                a.ReversaDeId,
                ReversaDeNumero = original?.Numero,
                ReversadoPorId = reversa?.Id,
                ReversadoPorNumero = reversa?.Numero,
                Reversible = ReversaAsiento.ObtenerBloqueo(a, periodo, vinculos) is null,
                Lineas = a.Lineas.Select(l =>
                {
                    cuentas.TryGetValue(l.CuentaId, out var cuenta);
                    return new
                    {
                        l.CuentaId,
                        CuentaCodigo = cuenta?.Codigo,
                        CuentaNombre = cuenta?.Nombre,
                        l.CentroCostoId,
                        l.Debito,
                        l.Credito,
                    };
                }),
            };
        });

        return Ok(resultado);
    }

    [HttpGet("mayor")]
    public async Task<IActionResult> LibroMayor([FromQuery] int cuentaId, [FromQuery] int? periodoId)
    {
        var cuenta = await _db.CuentasContables.FindAsync(cuentaId);
        if (cuenta is null)
        {
            return BadRequest(new { error = "La cuenta indicada no existe." });
        }
        if (periodoId is not null && await _db.PeriodosContables.FindAsync(periodoId.Value) is null)
        {
            return BadRequest(new { error = "El periodo indicado no existe." });
        }

        var catalogo = await _db.CuentasContables
            .Select(c => new { c.Id, c.Codigo, c.Nombre, c.CuentaPadreId })
            .ToDictionaryAsync(c => c.Id);
        var descendientes = JerarquiaCuentas.IdsDescendientes(
            catalogo.Values.Select(c => (c.Id, c.CuentaPadreId)), cuentaId);
        var idsCuenta = descendientes.Append(cuentaId).ToList();

        var query = _db.LineasAsiento
            .Where(l => idsCuenta.Contains(l.CuentaId))
            .Join(
                _db.AsientosContables.Where(a => AsientoContable.EstadosContabilizados.Contains(a.Estado)),
                l => l.AsientoId,
                a => a.Id,
                (l, a) => new { Linea = l, Asiento = a });

        if (periodoId is not null)
        {
            query = query.Where(x => x.Asiento.PeriodoId == periodoId.Value);
        }

        var movimientos = await query
            .OrderBy(x => x.Asiento.Fecha).ThenBy(x => x.Asiento.Numero).ThenBy(x => x.Linea.Id)
            .Select(x => new
            {
                x.Asiento.Id,
                x.Asiento.Numero,
                x.Asiento.Fecha,
                x.Linea.CuentaId,
                x.Linea.Debito,
                x.Linea.Credito,
            })
            .ToListAsync();

        // Signo del acumulado sigue la misma convención que vw_balance_saldos.
        var esDeudora = cuenta.Naturaleza == "Deudora";
        decimal acumulado = 0;
        var resultado = movimientos.Select(m =>
        {
            acumulado += esDeudora ? m.Debito - m.Credito : m.Credito - m.Debito;
            var cuentaMovimiento = catalogo[m.CuentaId];
            return new
            {
                AsientoId = m.Id,
                AsientoNumero = m.Numero,
                m.Fecha,
                m.CuentaId,
                CuentaCodigo = cuentaMovimiento.Codigo,
                CuentaNombre = cuentaMovimiento.Nombre,
                m.Debito,
                m.Credito,
                SaldoAcumulado = acumulado,
            };
        });

        return Ok(new
        {
            Cuenta = new
            {
                cuenta.Id,
                cuenta.Codigo,
                cuenta.Nombre,
                cuenta.Naturaleza,
                EsHoja = descendientes.Count == 0,
                Subcuentas = descendientes.Count,
            },
            Movimientos = resultado,
        });
    }

    private static async Task<HashSet<int>> IdsComoConjuntoAsync(IQueryable<int> consulta) =>
        (await consulta.ToListAsync()).ToHashSet();
}
