using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class PartidaDobleValidatorTests
{
    private static AsientoContable Asiento(params (decimal Debito, decimal Credito)[] lineas) => new()
    {
        Lineas = lineas.Select(l => new LineaAsiento { Debito = l.Debito, Credito = l.Credito }).ToList(),
    };

    [Fact]
    public void AsientoBalanceadoDeDosLineas_EsValido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((100m, 0m), (0m, 100m)), out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void AsientoBalanceadoConVariasLineas_EsValido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((100m, 0m), (50m, 0m), (0m, 150m)), out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void DecimalesExactos_NoGeneranFalsoDescuadre()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((0.1m, 0m), (0.2m, 0m), (0m, 0.3m)), out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void DosLineasEnCero_SeConsideranBalanceadas()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((0m, 0m), (0m, 0m)), out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void SinLineas_EsInvalido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento(), out var error);

        Assert.False(ok);
        Assert.Contains("al menos dos líneas", error);
    }

    [Fact]
    public void UnaSolaLinea_EsInvalido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((100m, 0m)), out var error);

        Assert.False(ok);
        Assert.Contains("al menos dos líneas", error);
    }

    [Fact]
    public void LineasNulas_EsInvalido()
    {
        var asiento = new AsientoContable { Lineas = null! };

        var ok = PartidaDobleValidator.EsValido(asiento, out var error);

        Assert.False(ok);
        Assert.Contains("al menos dos líneas", error);
    }

    [Fact]
    public void MontoNegativoEnSegundaLinea_EsInvalidoYIndicaLaLinea()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((100m, 0m), (0m, -100m)), out var error);

        Assert.False(ok);
        Assert.Contains("La línea 2", error);
        Assert.Contains("negativo", error);
    }

    [Fact]
    public void DebitoYCreditoEnLaMismaLinea_EsInvalido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((50m, 50m), (0m, 0m)), out var error);

        Assert.False(ok);
        Assert.Contains("La línea 1", error);
        Assert.Contains("simultáneamente", error);
    }

    [Fact]
    public void DebitosDistintosDeCreditos_EsInvalido()
    {
        var ok = PartidaDobleValidator.EsValido(Asiento((100m, 0m), (0m, 99.99m)), out var error);

        Assert.False(ok);
        Assert.Contains("partida doble", error);
    }
}
