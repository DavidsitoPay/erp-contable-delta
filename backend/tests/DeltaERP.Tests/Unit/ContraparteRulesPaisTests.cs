using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ContraparteRulesPaisTests
{
    private static Contraparte Con(bool residente, string? pais) =>
        new() { Tipo = "Proveedor", Nombre = "P", EsResidente = residente, Pais = pais };

    [Theory]
    [InlineData(false, "US", true)]
    [InlineData(false, " us ", true)]
    [InlineData(false, null, false)]
    [InlineData(false, "USA", false)]
    [InlineData(false, "U1", false)]
    [InlineData(true, null, true)]
    [InlineData(true, "ZZZ", true)]
    public void ValidarPais_ExigeAlfa2SoloAlNoResidente(bool residente, string? pais, bool valido) =>
        Assert.Equal(valido, ContraparteRules.ValidarPais(Con(residente, pais)) is null);

    [Fact]
    public void PaisNormalizado_EsMayusculaSoloParaNoResidente()
    {
        Assert.Equal("GT", ContraparteRules.PaisNormalizado(Con(false, " gt ")));
        Assert.Null(ContraparteRules.PaisNormalizado(Con(true, "GT")));
    }
}
