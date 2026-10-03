using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// M3 Libros y auxiliares: balance de saldos, libro diario y libro mayor.
/// Las tres son consultas de solo lectura derivadas en tiempo real de
/// AsientoContable/LineaAsiento (ver database/05_views.sql y
/// docs/architecture.md) — no hay POST/PUT/DELETE en este controlador.
/// </summary>
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

    /// <summary>
    /// E3-F1-H1. Devuelve vw_balance_saldos completa: una fila por cada cuenta
    /// del catálogo (activa o no), saldo en cero si nunca tuvo movimiento. La
    /// vista ya excluye asientos no confirmados y ya aplica el signo según la
    /// naturaleza de cada cuenta (ver database/05_views.sql) — este endpoint
    /// no repite esa lógica, solo la expone.
    /// </summary>
    [HttpGet("balance-saldos")]
    public async Task<IActionResult> BalanceSaldos()
    {
        var balance = await _db.BalanceSaldos
            .OrderBy(b => b.Codigo)
            .ToListAsync();
        return Ok(balance);
    }

    /// <summary>
    /// E3-F1-H2 (primera mitad). Asientos confirmados de un periodo, con sus
    /// líneas, ordenados por fecha y número — igual que pediría un contador al
    /// imprimir el diario para el cierre mensual. Los asientos en Borrador o
    /// Anulados no pueden existir hoy (AsientosController solo acepta
    /// "Confirmado" al registrar), pero el filtro se deja explícito para no
    /// depender de esa invariante si el flujo cambia más adelante.
    /// </summary>
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
            .Where(a => a.PeriodoId == periodoId && a.Estado == "Confirmado")
            .OrderBy(a => a.Fecha).ThenBy(a => a.Numero)
            .ToListAsync();

        // Mismo patrón que AsientosController.Registrar: un solo lookup de las
        // cuentas realmente usadas, no una consulta por línea.
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

    /// <summary>
    /// E3-F1-H2 (segunda mitad). Movimientos de una cuenta específica, en
    /// asientos confirmados, con saldo acumulado línea a línea — el signo del
    /// acumulado respeta la naturaleza de la cuenta, igual que
    /// vw_balance_saldos (naturaleza Deudora: débito - crédito; Acreedora al
    /// revés). periodoId es opcional: sin él se ve el histórico completo de la
    /// cuenta, igual que vw_balance_saldos.
    /// </summary>
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
