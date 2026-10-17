using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public sealed record SaldoDocumentoAlCorte(
    int DocumentoId, string Numero, int TerceroId, string TerceroNombre, DateOnly Fecha, DateOnly FechaVencimiento,
    int MonedaId, string MonedaCodigo, decimal TipoCambioLibros, decimal SaldoOriginal, decimal SaldoGtq, int CuentaControlId);

public sealed record SaldoBancoAlCorte(
    int CuentaBancariaId, string Nombre, int MonedaId, int CuentaContableId, decimal SaldoOriginal, decimal LibrosGtq);

public class SaldoAlCorteService
{
    private const string EstadoVigente = "Vigente";

    private sealed record Base(
        int Id, string Numero, int TerceroId, DateOnly Fecha, DateOnly FechaVencimiento, int MonedaId,
        decimal TipoCambio, decimal TotalOriginal, decimal TotalGtq, int? AsientoId);

    private sealed record Movimiento(int DocumentoId, decimal Original, decimal Gtq);

    private readonly DeltaErpDbContext _db;
    private readonly TesoreriaService _tesoreria;

    public SaldoAlCorteService(DeltaErpDbContext db, TesoreriaService tesoreria)
    {
        _db = db;
        _tesoreria = tesoreria;
    }

    public async Task<IReadOnlyList<SaldoDocumentoAlCorte>> DocumentosAsync(bool esVenta, DateOnly corte, int? monedaId)
    {
        var bases = esVenta ? await BasesCxCAsync(corte, monedaId) : await BasesCxPAsync(corte, monedaId);
        if (bases.Count == 0)
        {
            return [];
        }
        var ids = bases.Select(b => b.Id).ToList();
        var aplicado = (esVenta ? await AplicadoCxCAsync(ids, corte) : await AplicadoCxPAsync(ids, corte)).ToLookup(m => m.DocumentoId);
        var notas = (esVenta ? await NotasCxCAsync(ids, corte) : await NotasCxPAsync(ids, corte)).ToLookup(m => m.DocumentoId);
        var retenido = (esVenta ? new List<Movimiento>() : await RetenidoAsync(ids, corte)).ToLookup(m => m.DocumentoId);
        var controles = await _tesoreria.ObtenerControlPorAsientoAsync(esVenta ? LadoControl.Debito : LadoControl.Credito, bases.Select(b => b.AsientoId));
        var idsTercero = bases.Select(b => b.TerceroId).Distinct().ToList();
        var terceros = await _db.Contrapartes.AsNoTracking().Where(c => idsTercero.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre);
        var monedas = await _db.Monedas.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Codigo);

        var resultado = new List<SaldoDocumentoAlCorte>();
        foreach (var b in bases)
        {
            var original = b.TotalOriginal - aplicado[b.Id].Sum(m => m.Original) - notas[b.Id].Sum(m => m.Original) - retenido[b.Id].Sum(m => m.Original);
            var gtq = b.TotalGtq - aplicado[b.Id].Sum(m => m.Gtq) - notas[b.Id].Sum(m => m.Gtq) - retenido[b.Id].Sum(m => m.Gtq);
            if (original == 0 && gtq == 0)
            {
                continue;
            }
            resultado.Add(new SaldoDocumentoAlCorte(
                b.Id, b.Numero, b.TerceroId, terceros.GetValueOrDefault(b.TerceroId, string.Empty), b.Fecha, b.FechaVencimiento,
                b.MonedaId, monedas[b.MonedaId], b.TipoCambio, original, gtq, ControlDe(b.AsientoId, controles)));
        }
        return resultado;
    }

    private static int ControlDe(int? asientoId, Dictionary<int, int> controles) =>
        asientoId is { } id && controles.TryGetValue(id, out var control) ? control : 0;

    public async Task<IReadOnlyList<SaldoBancoAlCorte>> BancosAsync(DateOnly corte, int monedaId)
    {
        var cuentas = await _db.CuentasBancarias.AsNoTracking().Where(c => c.MonedaId == monedaId).ToListAsync();
        var resultado = new List<SaldoBancoAlCorte>();
        foreach (var cuenta in cuentas)
        {
            var original = await _db.MovimientosTesoreria
                .Where(m => m.CuentaBancariaId == cuenta.Id && m.Fecha <= corte)
                .SumAsync(m => m.Tipo == MovimientoTesoreria.TipoIngreso ? m.Monto : -m.Monto);
            var libros = await SaldoMayorAsync(cuenta.CuentaContableId, corte);
            if (original != 0 || libros != 0)
            {
                resultado.Add(new SaldoBancoAlCorte(cuenta.Id, $"{cuenta.Banco} {cuenta.Numero}", cuenta.MonedaId, cuenta.CuentaContableId, original, libros));
            }
        }
        return resultado;
    }

    // Débitos menos créditos de asientos Confirmado o Anulado con fecha <= corte; el reporte de CxP lo usa invertido.
    public async Task<decimal> SaldoMayorAsync(int cuentaId, DateOnly corte)
    {
        var lineas = _db.LineasAsiento
            .Where(l => l.CuentaId == cuentaId)
            .Join(
                _db.AsientosContables.Where(a => AsientoContable.EstadosContabilizados.Contains(a.Estado) && a.Fecha <= corte),
                l => l.AsientoId, a => a.Id, (l, a) => l);
        return (await lineas.SumAsync(l => (decimal?)l.Debito) ?? 0m) - (await lineas.SumAsync(l => (decimal?)l.Credito) ?? 0m);
    }

    private Task<List<Base>> BasesCxCAsync(DateOnly corte, int? monedaId) =>
        _db.DocumentosCxC.AsNoTracking()
            .Where(d => d.Estado == EstadoVigente && d.TipoDocumento != TiposDocumento.NotaCredito && d.Fecha <= corte && (monedaId == null || d.MonedaId == monedaId))
            .Select(d => new Base(d.Id, d.Numero, d.ClienteId, d.Fecha, d.FechaVencimiento, d.MonedaId, d.TipoCambioAplicado, d.MontoTotalOriginal, d.MontoTotal, d.AsientoId))
            .ToListAsync();

    private Task<List<Base>> BasesCxPAsync(DateOnly corte, int? monedaId) =>
        _db.DocumentosCxP.AsNoTracking()
            .Where(d => d.Estado == EstadoVigente && d.TipoDocumento != TiposDocumento.NotaCredito && d.Fecha <= corte && (monedaId == null || d.MonedaId == monedaId))
            .Select(d => new Base(d.Id, d.Numero, d.ProveedorId, d.Fecha, d.FechaVencimiento, d.MonedaId, d.TipoCambioAplicado, d.MontoTotalOriginal, d.MontoTotal, d.AsientoId))
            .ToListAsync();

    private Task<List<Movimiento>> AplicadoCxCAsync(List<int> ids, DateOnly corte) =>
        (from a in _db.AplicacionesPagoCliente
         join r in _db.RecibosPagoCliente on a.ReciboPagoId equals r.Id
         where ids.Contains(a.DocumentoId) && r.Fecha <= corte
         select new Movimiento(a.DocumentoId, a.MontoAplicado, a.MontoAplicadoGtq)).ToListAsync();

    private Task<List<Movimiento>> AplicadoCxPAsync(List<int> ids, DateOnly corte) =>
        (from a in _db.AplicacionesPagoProveedor
         join p in _db.PagosProveedor on a.PagoCabeceraId equals p.Id
         where ids.Contains(a.DocumentoId) && p.Fecha <= corte
         select new Movimiento(a.DocumentoId, a.MontoAplicado, a.MontoAplicadoGtq)).ToListAsync();

    private Task<List<Movimiento>> NotasCxCAsync(List<int> ids, DateOnly corte) =>
        _db.DocumentosCxC
            .Where(n => n.TipoDocumento == TiposDocumento.NotaCredito && n.Estado == EstadoVigente && n.Fecha <= corte
                        && n.DocumentoOrigenId != null && ids.Contains(n.DocumentoOrigenId.Value))
            .Select(n => new Movimiento(n.DocumentoOrigenId!.Value, n.MontoTotalOriginal, n.MontoTotal)).ToListAsync();

    private Task<List<Movimiento>> NotasCxPAsync(List<int> ids, DateOnly corte) =>
        _db.DocumentosCxP
            .Where(n => n.TipoDocumento == TiposDocumento.NotaCredito && n.Estado == EstadoVigente && n.Fecha <= corte
                        && n.DocumentoOrigenId != null && ids.Contains(n.DocumentoOrigenId.Value))
            .Select(n => new Movimiento(n.DocumentoOrigenId!.Value, n.MontoTotalOriginal, n.MontoTotal)).ToListAsync();

    private Task<List<Movimiento>> RetenidoAsync(List<int> ids, DateOnly corte) =>
        _db.Retenciones
            .Where(r => r.Concepto == Retencion.ConceptoIsr && r.DocumentoCxpId != null && ids.Contains(r.DocumentoCxpId.Value) && r.FechaOperacion <= corte)
            .Select(r => new Movimiento(r.DocumentoCxpId!.Value, r.MontoRetenidoOriginal, r.MontoRetenido)).ToListAsync();
}
