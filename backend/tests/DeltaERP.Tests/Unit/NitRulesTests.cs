using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class NitRulesTests
{
    [Theory]
    [InlineData("1234567", "9")]
    [InlineData("999", "7")]
    [InlineData("6", "K")]
    [InlineData("1000000", "3")]
    public void CalcularDigito_AplicaModulo11(string cuerpo, string esperado)
    {
        Assert.Equal(esperado, NitRules.CalcularDigito(cuerpo));
    }

    [Theory]
    [InlineData("1234567-9", "1234567-9")]
    [InlineData(" 12345679 ", "1234567-9")]
    [InlineData("6-k", "6-K")]
    [InlineData("c/f", "CF")]
    [InlineData("cf", "CF")]
    [InlineData("1", "1")]
    public void Normalizar_DevuelveFormatoCanonico(string entrada, string esperado)
    {
        Assert.Equal(esperado, NitRules.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_ConTextoVacio_DevuelveNull(string? entrada)
    {
        Assert.Null(NitRules.Normalizar(entrada));
    }

    [Theory]
    [InlineData("1234567-9", false, true)]
    [InlineData("12345679", false, true)]
    [InlineData("6-K", false, true)]
    [InlineData("1234567-8", false, false)]
    [InlineData("12345A7-9", false, false)]
    [InlineData("1234567890123-5", false, false)]
    [InlineData("-9", false, false)]
    [InlineData("5", false, false)]
    [InlineData("", false, false)]
    [InlineData("CF", true, true)]
    [InlineData("C/F", true, true)]
    [InlineData("CF", false, false)]
    public void EsValido_VerificaDigitoYConsumidorFinal(string nit, bool permiteCf, bool esperado)
    {
        Assert.Equal(esperado, NitRules.EsValido(nit, permiteCf));
    }
}
