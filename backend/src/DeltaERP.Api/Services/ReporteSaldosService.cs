using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class ReporteSaldosService
{
    public const string TipoCxC = "CxC";
    public const string TipoCxP = "CxP";
    private const string Leyenda = "Los saldos en GTQ están a los tipos de cambio de libros (antes de revaluar).";

    private readonly DeltaErpDbContext _db;
    private readonly SaldoAlCorteService _saldos;

    public ReporteSaldosService(DeltaErpDbContext db, SaldoAlCorteService saldos)
    {
        _db = db;
        _saldos = saldos;
    }

    public async Task<(ReporteSaldosRespuesta? Reporte, ErrorValidacion? Error)> GenerarAsync(string? tipo, string? moneda, DateOnly? corte)
    {
        if (tipo is not (TipoCxC or TipoCxP) || corte is null)
        {
            return (null, ErrorValidacion.Solicitud("Indique el tipo (CxC o CxP) y la fecha de corte."));
        }
        int? monedaId = null;
        if (!string.IsNullOrWhiteSpace(moneda))
        {
            var codigo = moneda.Trim().ToUpperInvariant();
            monedaId = await _db.Monedas.AsNoTracking().Where(m => m.Codigo == codigo && m.Activa).Select(m => (int?)m.Id).FirstOrDefaultAsync();
            if (monedaId is null)
            {
                return (null, ErrorValidacion.Solicitud("La moneda indicada no existe o está inactiva."));
            }
        }

        var esVenta = tipo == TipoCxC;
        var documentos = await _saldos.DocumentosAsync(esVenta, corte.Value, monedaId);
        var filas = documentos
            .OrderBy(d => d.FechaVencimiento).ThenBy(d => d.Numero, StringComparer.Ordinal)
            .Select(d => new FilaSaldoAntiguedad(
                d.DocumentoId, d.Numero, d.TerceroId, d.TerceroNombre, d.Fecha, d.FechaVencimiento, d.MonedaCodigo, d.SaldoOriginal,
                d.TipoCambioLibros, d.SaldoGtq, SaldoAntiguedadRules.DiasVencido(d.FechaVencimiento, corte.Value),
                SaldoAntiguedadRules.Tramo(d.FechaVencimiento, corte.Value)))
            .ToList();
        var conciliacion = monedaId is null ? await ConciliarAsync(documentos, esVenta, corte.Value) : [];
        return (new ReporteSaldosRespuesta(tipo, corte.Value, filas, Totalizar(filas), filas.Sum(f => f.SaldoGtq), conciliacion, Leyenda), null);
    }

    private static List<TotalSaldoMoneda> Totalizar(IReadOnlyList<FilaSaldoAntiguedad> filas) =>
        filas.GroupBy(f => f.Moneda).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TotalSaldoMoneda(
                g.Key, g.Sum(f => f.SaldoOriginal), g.Sum(f => f.SaldoGtq),
                SaldoAntiguedadRules.Tramos.ToDictionary(t => t, t => g.Where(f => f.Tramo == t).Sum(f => f.SaldoOriginal))))
            .ToList();

    // diferencia = saldo del mayor - revaluación vigente - total del reporte (en la naturaleza de la cuenta de control).
    private async Task<List<ConciliacionSaldo>> ConciliarAsync(IReadOnlyList<SaldoDocumentoAlCorte> documentos, bool esVenta, DateOnly corte)
    {
        var resultado = new List<ConciliacionSaldo>();
        foreach (var grupo in documentos.Where(d => d.CuentaControlId != 0).GroupBy(d => d.CuentaControlId))
        {
            var signo = esVenta ? 1m : -1m;
            var mayor = signo * await _saldos.SaldoMayorAsync(grupo.Key, corte);
            var revaluacion = signo * await RevaluacionNetaAsync(grupo.Key, corte);
            var total = grupo.Sum(d => d.SaldoGtq);
            var cuenta = await _db.CuentasContables.AsNoTracking().FirstAsync(c => c.Id == grupo.Key);
            resultado.Add(new ConciliacionSaldo(grupo.Key, cuenta.Codigo, total, mayor, revaluacion, mayor - revaluacion - total));
        }
        return resultado;
    }

    private async Task<decimal> RevaluacionNetaAsync(int cuentaId, DateOnly corte)
    {
        var vigentes = _db.Revaluaciones.Where(r => r.Estado == Revaluacion.EstadoVigente).Select(r => new { r.AsientoId, r.AsientoReversaId });
        var asientos = _db.AsientosContables.Where(a => a.Fecha <= corte
            && vigentes.Any(r => r.AsientoId == a.Id || r.AsientoReversaId == a.Id));
        var lineas = _db.LineasAsiento.Where(l => l.CuentaId == cuentaId && asientos.Any(a => a.Id == l.AsientoId));
        return (await lineas.SumAsync(l => (decimal?)l.Debito) ?? 0m) - (await lineas.SumAsync(l => (decimal?)l.Credito) ?? 0m);
    }
}
