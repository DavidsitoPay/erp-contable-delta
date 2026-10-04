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

    // La vista ya excluye asientos no confirmados y aplica el signo según la
    // naturaleza de cada cuenta; este endpoint no repite esa lógica, solo la expone.
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

        // Filtro por Confirmado explícito aunque hoy no existan otros estados, para
        // no depender de esa invariante si el flujo cambia más adelante.
        var asientos = await _db.AsientosContables
            .Include(a => a.Lineas)
            .Where(a => a.PeriodoId == periodoId && a.Estado == "Confirmado")
            .OrderBy(a => a.Fecha).ThenBy(a => a.Numero)
            .ToListAsync();

        // Lookup único de cuentas usadas, no una consulta por línea.
        var idsCuenta = asientos.SelectMany(a => a.Lineas).Select(l => l.CuentaId).Distinct().ToList();
        var cuentas = await _db.CuentasContables
            .Where(c => idsCuenta.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var resultado = asientos.Select(a => new
        {
            a.Id,
            a.Numero,
            a.Fecha,
            a.Monto,
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

        var query = _db.LineasAsiento
            .Where(l => l.CuentaId == cuentaId)
            .Join(
                _db.AsientosContables.Where(a => a.Estado == "Confirmado"),
                l => l.AsientoId,
                a => a.Id,
                (l, a) => new { Linea = l, Asiento = a });

        if (periodoId is not null)
        {
            query = query.Where(x => x.Asiento.PeriodoId == periodoId.Value);
        }

        var movimientos = await query
            .OrderBy(x => x.Asiento.Fecha).ThenBy(x => x.Asiento.Numero)
            .Select(x => new
            {
                x.Asiento.Id,
                x.Asiento.Numero,
                x.Asiento.Fecha,
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
            return new
            {
                AsientoId = m.Id,
                AsientoNumero = m.Numero,
                m.Fecha,
                m.Debito,
                m.Credito,
                SaldoAcumulado = acumulado,
            };
        });

        return Ok(new
        {
            Cuenta = new { cuenta.Id, cuenta.Codigo, cuenta.Nombre, cuenta.Naturaleza },
            Movimientos = resultado,
        });
    }
}
