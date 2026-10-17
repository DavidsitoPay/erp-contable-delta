using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

public interface IBanguatClient
{
    // Cotizaciones publicadas en [desde, hasta]; vacío si no hay datos. Lanza BanguatNoDisponibleException si el servicio falla.
    Task<IReadOnlyList<CotizacionBanguat>> ObtenerRangoAsync(int codigoMoneda, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

public sealed class BanguatNoDisponibleException : Exception
{
    public BanguatNoDisponibleException(string mensaje, Exception? interna = null) : base(mensaje, interna)
    {
    }
}
