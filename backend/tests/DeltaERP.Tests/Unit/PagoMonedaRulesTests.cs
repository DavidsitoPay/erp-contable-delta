using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class PagoMonedaRulesTests
{
    private static readonly CuentaMonedaInfo Gtq = new(1, "GTQ", true);
    private static readonly CuentaMonedaInfo Usd = new(2, "USD", false);

    private static DocumentoMonedaInfo Doc(int id, string numero, int monedaId, string codigo, bool funcional) =>
        new(id, numero, monedaId, codigo, funcional);

    private static AplicacionEntrada Ap(int id, decimal monto, decimal? valor = null) => new(id, monto, valor);

    [Fact]
    public void DocumentosDeMasDeUnaMoneda_Error400()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false), Doc(2, "F-2", 3, "EUR", false) };

        var error = PagoMonedaRules.Validar(Usd, documentos, [Ap(1, 10m), Ap(2, 10m)]);

        Assert.Equal(
            ("Un cobro o pago aplica facturas de una sola moneda; separe las facturas en USD y en EUR.", false),
            (error!.Mensaje, error.EsConflicto));
    }

    [Fact]
    public void FacturaEnMonedaExtranjeraAplicadaDosVeces_Error400()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        var error = PagoMonedaRules.Validar(Usd, documentos, [Ap(1, 10m), Ap(1, 5m)]);

        Assert.Equal("La factura F-1 en moneda extranjera admite una sola aplicación por cobro o pago.", error!.Mensaje);
    }

    [Fact]
    public void FacturaEnQuetzalesAplicadaDosVeces_EsValida()
    {
        var documentos = new[] { Doc(1, "F-1", 1, "GTQ", true) };

        Assert.Null(PagoMonedaRules.Validar(Gtq, documentos, [Ap(1, 10m), Ap(1, 5m)]));
    }

    [Fact]
    public void CuentaUsdConFacturaEnQuetzales_Conflicto()
    {
        var documentos = new[] { Doc(1, "F-1", 1, "GTQ", true) };

        var error = PagoMonedaRules.Validar(Usd, documentos, [Ap(1, 10m)]);

        Assert.Equal(
            ("RN-27: una factura en quetzales no puede cobrarse ni pagarse con una cuenta en USD en esta versión.", true),
            (error!.Mensaje, error.EsConflicto));
    }

    [Fact]
    public void CuentaUsdConFacturaEur_Conflicto()
    {
        var documentos = new[] { Doc(2, "F-2", 3, "EUR", false) };

        var error = PagoMonedaRules.Validar(Usd, documentos, [Ap(2, 10m)]);

        Assert.Equal(
            ("La moneda de la factura F-2 (EUR) no coincide con la de la cuenta bancaria (USD).", true),
            (error!.Mensaje, error.EsConflicto));
    }

    [Fact]
    public void CuentaGtqConFacturaUsdSinValorEfectivo_Error400()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        var error = PagoMonedaRules.Validar(Gtq, documentos, [Ap(1, 10m)]);

        Assert.Equal("Indique el efectivo en quetzales (valorEfectivoGtq) de la factura F-1.", error!.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.123)]
    public void ValorEfectivoInvalido_Error400(double valor)
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        var error = PagoMonedaRules.Validar(Gtq, documentos, [Ap(1, 10m, (decimal)valor)]);

        Assert.Equal("Indique el efectivo en quetzales (valorEfectivoGtq) de la factura F-1.", error!.Mensaje);
    }

    [Fact]
    public void ValorEfectivoConFacturaEnQuetzales_Error400()
    {
        var documentos = new[] { Doc(1, "F-1", 1, "GTQ", true) };

        var error = PagoMonedaRules.Validar(Gtq, documentos, [Ap(1, 10m, 10m)]);

        Assert.Equal(
            "valorEfectivoGtq solo aplica a facturas en moneda extranjera cobradas o pagadas desde una cuenta en quetzales.",
            error!.Mensaje);
    }

    [Fact]
    public void ValorEfectivoConCuentaUsd_Error400()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        var error = PagoMonedaRules.Validar(Usd, documentos, [Ap(1, 10m, 75m)]);

        Assert.Equal(
            "valorEfectivoGtq solo aplica a facturas en moneda extranjera cobradas o pagadas desde una cuenta en quetzales.",
            error!.Mensaje);
    }

    [Fact]
    public void CuentaUsdConFacturaUsd_SinValorEfectivo_EsValida()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        Assert.Null(PagoMonedaRules.Validar(Usd, documentos, [Ap(1, 10m)]));
    }

    [Fact]
    public void CuentaGtqConFacturaUsdYValorEfectivo_EsValida()
    {
        var documentos = new[] { Doc(1, "F-1", 2, "USD", false) };

        Assert.Null(PagoMonedaRules.Validar(Gtq, documentos, [Ap(1, 10m, 75.50m)]));
    }
}
