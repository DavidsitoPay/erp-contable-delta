using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ContraparteRulesTests
{
    private static Contraparte Datos(string? nit, string regimenIva = "GENERAL", string regimenIsr = "UTILIDADES") =>
        new() { Nit = nit, RegimenIva = regimenIva, RegimenIsr = regimenIsr };

    [Theory]
    [InlineData("Cliente", null)]
    [InlineData("Cliente", "")]
    [InlineData("Cliente", "CF")]
    [InlineData("Cliente", "1234567-9")]
    [InlineData("Proveedor", "1234567-9")]
    [InlineData("Proveedor", "6-K")]
    public void NitAceptado_SegunElTipo(string tipo, string? nit)
    {
        Assert.Null(ContraparteRules.ValidarFiscal(Datos(nit), tipo));
    }

    [Theory]
    [InlineData("Proveedor", null, "El NIT del proveedor es obligatorio.")]
    [InlineData("Proveedor", " ", "El NIT del proveedor es obligatorio.")]
    [InlineData("Proveedor", "CF", "CF solo es válido para clientes.")]
    [InlineData("Proveedor", "1234567-8", "El NIT no es válido (dígito verificador incorrecto).")]
    [InlineData("Cliente", "9999", "El NIT no es válido (dígito verificador incorrecto).")]
    public void NitRechazado_SegunElTipo(string tipo, string? nit, string mensaje)
    {
        Assert.Equal(mensaje, ContraparteRules.ValidarFiscal(Datos(nit), tipo));
    }

    [Fact]
    public void RegimenDeIvaInvalido_SeRechazaAntesQueElNit()
    {
        var error = ContraparteRules.ValidarFiscal(Datos("0000", regimenIva: "OTRO"), "Cliente");

        Assert.Equal("Régimen de IVA inválido. Debe ser uno de: GENERAL, PEQUENO_CONTRIBUYENTE, EXENTO.", error);
    }

    [Fact]
    public void RegimenDeIsrInvalido_SeRechaza()
    {
        var error = ContraparteRules.ValidarFiscal(Datos(null, regimenIsr: "OTRO"), "Cliente");

        Assert.Equal("Régimen de ISR inválido. Debe ser uno de: UTILIDADES, SIMPLIFICADO.", error);
    }
}
