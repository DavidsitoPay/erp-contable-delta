using System.Globalization;

namespace DeltaERP.Domain.Rules;

public sealed record CotizacionBanguat(DateOnly Fecha, decimal Venta, decimal Compra);

public sealed record TasaCalculada(
    decimal Tasa, DateOnly FechaReferencia, bool Arrastrado,
    decimal? CotizacionVenta, decimal? CotizacionCompra, decimal? ReferenciaUsd);

public sealed record TipoCambioAplicable(
    int MonedaId, string MonedaCodigo, int? TipoCambioId, decimal Tasa, string? Fuente, bool Arrastrado, DateOnly Fecha);

public static class CambioRules
{
    public const int DecimalesTasa = 6;
    public const int LongitudMaximaMotivo = 300;
    private const decimal TasaMaxima = 99999.999999m;
    private const int HorasGuatemala = -6;

    public static decimal ConvertirAGtq(decimal montoOriginal, decimal tasa) => MotorFiscal.Redondear(montoOriginal * tasa);

    public static TasaCalculada? CalcularUsd(DateOnly fecha, IReadOnlyList<CotizacionBanguat> filas)
    {
        var propia = filas.FirstOrDefault(f => f.Fecha == fecha);
        if (propia is not null)
        {
            return EsFinDeSemana(fecha)
                ? new TasaCalculada(propia.Venta, ViernesAnterior(fecha), true, null, null, null)
                : new TasaCalculada(propia.Venta, fecha, false, null, null, null);
        }
        var anterior = UltimaAnterior(fecha, filas);
        return anterior is null ? null : new TasaCalculada(anterior.Venta, anterior.Fecha, true, null, null, null);
    }

    public static TasaCalculada? CalcularEur(DateOnly fecha, IReadOnlyList<CotizacionBanguat> filasEur, TasaCalculada referenciaUsd)
    {
        var fila = filasEur.FirstOrDefault(f => f.Fecha == fecha) ?? UltimaAnterior(fecha, filasEur);
        if (fila is null)
        {
            return null;
        }
        var tasa = Math.Round(fila.Venta * referenciaUsd.Tasa, DecimalesTasa, MidpointRounding.AwayFromZero);
        return new TasaCalculada(
            tasa, fila.Fecha, fila.Fecha != fecha || referenciaUsd.Arrastrado, fila.Venta, fila.Compra, referenciaUsd.Tasa);
    }

    public static string? ValidarTasaManual(decimal tasa, string? motivo)
    {
        if (tasa <= 0 || tasa > TasaMaxima || Math.Round(tasa, DecimalesTasa) != tasa)
        {
            return "La tasa de cambio debe ser mayor a cero, con hasta 6 decimales.";
        }
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return "El motivo es obligatorio.";
        }
        return motivo.Trim().Length > LongitudMaximaMotivo ? "El motivo no puede exceder 300 caracteres." : null;
    }

    public static bool EsFechaFutura(DateOnly fecha, DateOnly hoyGuatemala) => fecha > hoyGuatemala;

    public static DateOnly HoyGuatemala(DateTimeOffset ahoraUtc) =>
        DateOnly.FromDateTime(ahoraUtc.UtcDateTime.AddHours(HorasGuatemala));

    public static string FormatoTasa(decimal tasa) => tasa.ToString("0.000000", CultureInfo.InvariantCulture);

    private static CotizacionBanguat? UltimaAnterior(DateOnly fecha, IReadOnlyList<CotizacionBanguat> filas) =>
        filas.Where(f => f.Fecha < fecha).OrderByDescending(f => f.Fecha).FirstOrDefault();

    private static bool EsFinDeSemana(DateOnly fecha) => fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static DateOnly ViernesAnterior(DateOnly fecha) => fecha.AddDays(fecha.DayOfWeek == DayOfWeek.Saturday ? -1 : -2);
}
