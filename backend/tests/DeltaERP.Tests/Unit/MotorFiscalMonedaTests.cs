using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class MotorFiscalMonedaTests
{
    private static readonly ParametroImpuesto Iva = new(1, TipoImpuesto.IvaGeneral, 12m, true);
    private static readonly ParametroImpuesto Exento = new(3, TipoImpuesto.Exento, 0m, false);

    [Fact]
    public void Calcular_ConTipoCambio_IvaSobreElEquivalenteEnGtq()
    {
        var resumen = MotorFiscal.Calcular([new EntradaLinea(1m, 1120m, Iva)], 7.60m);

        var linea = resumen.Lineas.Single();
        Assert.Equal(
            (8512.00m, 912.00m, 7600.00m, 1120.00m, 8512.00m, 1120.00m),
            (linea.MontoLinea, linea.Iva, linea.Base, linea.MontoLineaOriginal, resumen.Total, resumen.TotalOriginal));
    }

    [Fact]
    public void Calcular_RedondeaPorLinea()
    {
        var resumen = MotorFiscal.Calcular([new EntradaLinea(1m, 33.33m, Exento), new EntradaLinea(1m, 33.33m, Exento)], 7.64157m);

        Assert.Equal((509.38m, 66.66m), (resumen.Total, resumen.TotalOriginal));
    }

    [Fact]
    public void Calcular_SinTipoCambio_ConservaElComportamientoAnterior()
    {
        var linea = MotorFiscal.CalcularLinea(new EntradaLinea(2m, 100m, Iva));

        Assert.Equal(linea.MontoLinea, linea.MontoLineaOriginal);
    }
}
