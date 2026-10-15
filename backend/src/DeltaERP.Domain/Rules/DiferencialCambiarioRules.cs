namespace DeltaERP.Domain.Rules;

public sealed record SaldoDocumentoMe(decimal SaldoOriginal, decimal SaldoGtq, decimal TipoCambioLibros);

public sealed record AplicacionLiquidada(int DocumentoId, decimal MontoOriginal, decimal LibrosGtq, decimal ValorGtq, decimal ResultadoGtq);

public static class DiferencialCambiarioRules
{
    private const decimal Centavo = 0.01m;

    public static decimal LibrosDeAplicacion(decimal montoAplicado, SaldoDocumentoMe saldo)
    {
        if (montoAplicado == saldo.SaldoOriginal)
        {
            return saldo.SaldoGtq;
        }
        var libros = MotorFiscal.Redondear(montoAplicado * saldo.TipoCambioLibros);
        return libros >= saldo.SaldoGtq ? Math.Max(saldo.SaldoGtq - Centavo, 0m) : libros;
    }

    public static decimal ValorEnCuentaMonedaExtranjera(decimal montoAplicado, decimal tasaReferencia) =>
        MotorFiscal.Redondear(montoAplicado * tasaReferencia);

    // RN-26: positivo = ganancia, negativo = pérdida, tanto en cobros como en pagos.
    public static AplicacionLiquidada Liquidar(bool esCobro, int documentoId, decimal montoAplicado, decimal valorGtq, SaldoDocumentoMe saldo)
    {
        var libros = LibrosDeAplicacion(montoAplicado, saldo);
        return new AplicacionLiquidada(documentoId, montoAplicado, libros, valorGtq, esCobro ? valorGtq - libros : libros - valorGtq);
    }

    public static AplicacionLiquidada SinDiferencial(int documentoId, decimal monto) =>
        new(documentoId, monto, monto, monto, 0m);

    public static (decimal Ganancia, decimal Perdida) Totalizar(IEnumerable<AplicacionLiquidada> aplicaciones)
    {
        var lista = aplicaciones.ToList();
        return (lista.Sum(a => Math.Max(a.ResultadoGtq, 0m)), lista.Sum(a => Math.Max(-a.ResultadoGtq, 0m)));
    }
}
