using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class TesoreriaRulesMonedaTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GTQ")]
    [InlineData("usd")]
    [InlineData(" USD ")]
    public void ValidarMonedaCuenta_GtqOUsdOVacia_EsValida(string? codigo)
    {
        Assert.Null(TesoreriaRules.ValidarMonedaCuenta(codigo));
    }

    [Fact]
    public void ValidarMonedaCuenta_Eur_NoDisponible()
    {
        Assert.Equal(
            "Las cuentas bancarias en EUR no están disponibles en esta versión.",
            TesoreriaRules.ValidarMonedaCuenta("EUR"));
    }

    [Fact]
    public void ValidarMonedaCuenta_OtraMoneda_Invalida()
    {
        Assert.Equal("La moneda de la cuenta bancaria debe ser GTQ o USD.", TesoreriaRules.ValidarMonedaCuenta("XYZ"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(100)]
    public void ValidarTransferenciaCruzada_MismaMonedaSinDestinoOIgual_EsValida(int? montoDestino)
    {
        Assert.Null(TesoreriaRules.ValidarTransferenciaCruzada(100m, montoDestino, true));
    }

    [Fact]
    public void ValidarTransferenciaCruzada_MismaMonedaConDestinoDistinto_Invalida()
    {
        Assert.Equal(
            "En una transferencia entre cuentas de la misma moneda el monto destino debe ser igual al monto.",
            TesoreriaRules.ValidarTransferenciaCruzada(100m, 99m, true));
    }

    [Fact]
    public void ValidarTransferenciaCruzada_DistintaMonedaSinDestino_Invalida()
    {
        Assert.Equal(
            "El monto destino es obligatorio cuando las cuentas son de distinta moneda.",
            TesoreriaRules.ValidarTransferenciaCruzada(100m, null, false));
    }

    [Theory]
    [InlineData(0, "El monto debe ser mayor a cero.")]
    [InlineData(-5, "El monto debe ser mayor a cero.")]
    [InlineData(1.005, "El monto no puede tener más de 2 decimales.")]
    public void ValidarTransferenciaCruzada_DistintaMonedaConDestinoInvalido_UsaMensajeDeMonto(double montoDestino, string mensajeEsperado)
    {
        Assert.Equal(mensajeEsperado, TesoreriaRules.ValidarTransferenciaCruzada(100m, (decimal)montoDestino, false));
    }

    [Fact]
    public void ValidarTransferenciaCruzada_DistintaMonedaValida_EsValida()
    {
        Assert.Null(TesoreriaRules.ValidarTransferenciaCruzada(7700m, 1000m, false));
    }
}
