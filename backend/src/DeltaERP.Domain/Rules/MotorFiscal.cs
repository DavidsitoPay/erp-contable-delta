namespace DeltaERP.Domain.Rules;

public enum TipoImpuesto
{
    IvaGeneral,
    Exento,
    NoAfecto,
    PequenoContribuyente,
    Legado,
}

public enum ColumnaLibro
{
    BaseBien,
    BaseServicio,
    Exento,
}

public static class TipoBienServicio
{
    public const string Bien = "BIEN";
    public const string Servicio = "SERVICIO";
    public static readonly string[] Validos = { Bien, Servicio };
}

public sealed record ParametroImpuesto(int Id, TipoImpuesto Tipo, decimal Tasa, bool GeneraCredito);

public sealed record EntradaLinea(decimal Cantidad, decimal PrecioUnitario, ParametroImpuesto Impuesto, string TipoBienServicio = Rules.TipoBienServicio.Servicio);

public sealed record ResultadoLinea(decimal MontoLinea, decimal Base, decimal Iva, decimal TasaAplicada, bool IvaAcreditable, string TipoBienServicio);

public sealed record ResumenFiscal(IReadOnlyList<ResultadoLinea> Lineas, decimal Total, decimal Base, decimal Iva, decimal IvaAcreditable);

public sealed record ImportesLibro(decimal BaseBienes, decimal BaseServicios, decimal Exento, decimal Iva);

public static class MotorFiscal
{
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static ResultadoLinea CalcularLinea(EntradaLinea linea, decimal tipoCambio = 1m)
    {
        var impuesto = linea.Impuesto;
        if (impuesto.Tipo == TipoImpuesto.Legado)
        {
            throw new InvalidOperationException("Los impuestos históricos no se pueden usar en documentos nuevos.");
        }

        var totalOriginal = Redondear(linea.Cantidad * linea.PrecioUnitario);
        var total = Redondear(totalOriginal * tipoCambio);
        var iva = impuesto.Tipo == TipoImpuesto.IvaGeneral
            ? Redondear(total * impuesto.Tasa / (100m + impuesto.Tasa)) // Dto. 27-92 art. 10
            : 0m;
        var acreditable = iva > 0 && impuesto.GeneraCredito;
        return new ResultadoLinea(total, total - iva, iva, impuesto.Tasa, acreditable, linea.TipoBienServicio);
    }

    public static ResumenFiscal Calcular(IEnumerable<EntradaLinea> lineas, decimal tipoCambio = 1m)
    {
        var resultados = lineas.Select(l => CalcularLinea(l, tipoCambio)).ToList();
        return new ResumenFiscal(
            resultados,
            resultados.Sum(r => r.MontoLinea),
            resultados.Sum(r => r.Base),
            resultados.Sum(r => r.Iva),
            resultados.Where(r => r.IvaAcreditable).Sum(r => r.Iva));
    }

    public static bool IvaVaAlLibro(ParametroImpuesto impuesto, bool esVenta) =>
        (impuesto.Tipo == TipoImpuesto.IvaGeneral || (impuesto.Tipo == TipoImpuesto.Legado && impuesto.Tasa > 0))
        && (esVenta || impuesto.GeneraCredito);

    public static ColumnaLibro ClasificarParaLibro(ParametroImpuesto impuesto, string tipoBienServicio)
    {
        if (impuesto.Tipo is TipoImpuesto.Exento or TipoImpuesto.NoAfecto)
        {
            return ColumnaLibro.Exento;
        }
        return tipoBienServicio == TipoBienServicio.Bien ? ColumnaLibro.BaseBien : ColumnaLibro.BaseServicio;
    }

    public static ImportesLibro DistribuirParaLibro(ParametroImpuesto impuesto, ResultadoLinea resultado, bool esVenta)
    {
        var iva = IvaVaAlLibro(impuesto, esVenta) ? resultado.Iva : 0m;
        var valor = resultado.MontoLinea - iva;
        return ClasificarParaLibro(impuesto, resultado.TipoBienServicio) switch
        {
            ColumnaLibro.BaseBien => new ImportesLibro(valor, 0m, 0m, iva),
            ColumnaLibro.BaseServicio => new ImportesLibro(0m, valor, 0m, iva),
            _ => new ImportesLibro(0m, 0m, valor, 0m),
        };
    }
}
