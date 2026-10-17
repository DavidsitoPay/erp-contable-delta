using System.Collections.Concurrent;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Support;

public sealed class FakeBanguatClient : IBanguatClient
{
    private readonly ConcurrentDictionary<(int Codigo, DateOnly Fecha), (decimal Venta, decimal Compra)> _datos = new();
    private int _llamadas;

    public bool Falla { get; set; }

    public int Llamadas => _llamadas;

    public void Sembrar(int codigoMoneda, DateOnly fecha, decimal venta, decimal? compra = null) =>
        _datos[(codigoMoneda, fecha)] = (venta, compra ?? venta);

    public void Reiniciar()
    {
        _datos.Clear();
        Falla = false;
        Interlocked.Exchange(ref _llamadas, 0);
    }

    public Task<IReadOnlyList<CotizacionBanguat>> ObtenerRangoAsync(int codigoMoneda, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _llamadas);
        if (Falla)
        {
            throw new BanguatNoDisponibleException("Banguat simulado no disponible.");
        }
        IReadOnlyList<CotizacionBanguat> filas = _datos
            .Where(d => d.Key.Codigo == codigoMoneda && d.Key.Fecha >= desde && d.Key.Fecha <= hasta)
            .Select(d => new CotizacionBanguat(d.Key.Fecha, d.Value.Venta, d.Value.Compra))
            .OrderBy(c => c.Fecha)
            .ToList();
        return Task.FromResult(filas);
    }
}
