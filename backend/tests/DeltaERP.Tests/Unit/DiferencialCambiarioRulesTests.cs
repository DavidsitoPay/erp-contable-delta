using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class DiferencialCambiarioRulesTests
{
    [Fact]
    public void TresCobrosParciales_ElUltimoAbsorbeElRedondeo()
    {
        var primero = DiferencialCambiarioRules.LibrosDeAplicacion(33.33m, new SaldoDocumentoMe(100.00m, 764.16m, 7.64157m));
        var segundo = DiferencialCambiarioRules.LibrosDeAplicacion(33.33m, new SaldoDocumentoMe(66.67m, 509.47m, 7.64157m));
        var tercero = DiferencialCambiarioRules.LibrosDeAplicacion(33.34m, new SaldoDocumentoMe(33.34m, 254.78m, 7.64157m));

        Assert.Equal((254.69m, 254.69m, 254.78m), (primero, segundo, tercero));
        Assert.Equal(764.16m, primero + segundo + tercero);
    }

    [Fact]
    public void CobroParcial_ResultadoPositivoEsGanancia()
    {
        var resultado = DiferencialCambiarioRules.Liquidar(true, 1, 400m, 3080m, new SaldoDocumentoMe(1120m, 8512m, 7.60m));

        Assert.Equal((3040m, 40m), (resultado.LibrosGtq, resultado.ResultadoGtq));
    }

    [Fact]
    public void PagoDeCierre_AbsorbeYResultadoNegativoEsPerdida()
    {
        var resultado = DiferencialCambiarioRules.Liquidar(false, 1, 950m, 7315m, new SaldoDocumentoMe(950m, 7220m, 7.60m));

        Assert.Equal((7220m, -95m), (resultado.LibrosGtq, resultado.ResultadoGtq));
    }

    [Fact]
    public void PagoParcial_LibrosPorLaTasaDelDocumento()
    {
        Assert.Equal(3610.00m, DiferencialCambiarioRules.LibrosDeAplicacion(475m, new SaldoDocumentoMe(950m, 7220m, 7.60m)));
    }

    [Fact]
    public void GuardaDeSaldoGtq_NoCierraEnCeroMientrasQuedeSaldoOriginal()
    {
        Assert.Equal(759.92m, DiferencialCambiarioRules.LibrosDeAplicacion(99.99m, new SaldoDocumentoMe(100m, 760m, 7.60m)));
        Assert.Equal(759.91m, DiferencialCambiarioRules.LibrosDeAplicacion(99.99m, new SaldoDocumentoMe(100m, 759.92m, 7.60m)));
        Assert.Equal(0m, DiferencialCambiarioRules.LibrosDeAplicacion(50m, new SaldoDocumentoMe(100m, 0m, 7.60m)));
    }

    [Fact]
    public void ValorEnCuentaMonedaExtranjera_MultiplicaYRedondea()
    {
        Assert.Equal(3080.00m, DiferencialCambiarioRules.ValorEnCuentaMonedaExtranjera(400m, 7.70m));
    }

    [Fact]
    public void SinDiferencial_NoGeneraResultado()
    {
        Assert.Equal(new AplicacionLiquidada(5, 100m, 100m, 100m, 0m), DiferencialCambiarioRules.SinDiferencial(5, 100m));
    }

    [Fact]
    public void Totalizar_SeparaGananciaYPerdida()
    {
        var aplicaciones = new[]
        {
            new AplicacionLiquidada(1, 0m, 0m, 0m, 40m),
            new AplicacionLiquidada(2, 0m, 0m, 0m, -72m),
            new AplicacionLiquidada(3, 0m, 0m, 0m, 10m),
            new AplicacionLiquidada(4, 0m, 0m, 0m, 0m),
        };

        Assert.Equal((50m, 72m), DiferencialCambiarioRules.Totalizar(aplicaciones));
    }
}
