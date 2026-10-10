using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ConfiguracionFiscalRulesTests
{
    private static DatosConfiguracionFiscal Valida() => new("1234567-9", "Delta S.A.", "UTILIDADES", false, null, 10, 11);

    [Fact]
    public void ConDatosCorrectos_NoDevuelveError()
    {
        Assert.Null(ConfiguracionFiscalRules.Validar(Valida()));
    }

    [Fact]
    public void SinNitNiCuentas_EsValida()
    {
        Assert.Null(ConfiguracionFiscalRules.Validar(new DatosConfiguracionFiscal(null, null, "SIMPLIFICADO", false, null, null, null)));
    }

    public static TheoryData<int, string> Invalidas => new()
    {
        { 0, "Régimen de ISR inválido. Debe ser uno de: UTILIDADES, SIMPLIFICADO." },
        { 1, "Régimen de ISR inválido. Debe ser uno de: UTILIDADES, SIMPLIFICADO." },
        { 2, "Si la empresa es agente de retención de IVA debe indicar el tipo de agente." },
        { 3, "Tipo de agente inválido. Debe ser uno de: EXPORTADOR_HABITUAL, SECTOR_PUBLICO, TARJETA_CREDITO, COMBUSTIBLE, CONTRIBUYENTE_ESPECIAL." },
        { 4, "Las cuentas de IVA débito y crédito deben ser distintas." },
        { 5, "El NIT admite hasta 30 caracteres y el nombre legal hasta 200." },
        { 6, "El NIT admite hasta 30 caracteres y el nombre legal hasta 200." },
        { 7, "El NIT de la empresa no es válido." },
        { 8, "El NIT de la empresa no es válido." },
    };

    private static DatosConfiguracionFiscal Mutacion(int caso)
    {
        var d = Valida();
        return caso switch
        {
            0 => d with { RegimenIsr = "OTRO" },
            1 => d with { RegimenIsr = null },
            2 => d with { AgenteRetencionIva = true, TipoAgenteIva = " " },
            3 => d with { AgenteRetencionIva = true, TipoAgenteIva = "OTRO" },
            4 => d with { CuentaIvaCreditoId = 10 },
            5 => d with { NitEmpresa = new string('1', 31) },
            6 => d with { NombreLegal = new string('N', 201) },
            7 => d with { NitEmpresa = "1234567-8" },
            _ => d with { NitEmpresa = "CF" },
        };
    }

    [Theory]
    [MemberData(nameof(Invalidas))]
    public void ConDatosInvalidos_DevuelveElMensajeDelCaso(int caso, string mensaje)
    {
        Assert.Equal(mensaje, ConfiguracionFiscalRules.Validar(Mutacion(caso)));
    }

    [Fact]
    public void AgenteConTipoValido_EsValido()
    {
        var datos = Valida() with { AgenteRetencionIva = true, TipoAgenteIva = "SECTOR_PUBLICO" };

        Assert.Null(ConfiguracionFiscalRules.Validar(datos));
    }

    [Theory]
    [InlineData(true, "SECTOR_PUBLICO", "SECTOR_PUBLICO")]
    [InlineData(false, "SECTOR_PUBLICO", null)]
    public void TipoAgenteNormalizado_DescartaElTipoSiNoEsAgente(bool agente, string tipo, string? esperado)
    {
        var datos = Valida() with { AgenteRetencionIva = agente, TipoAgenteIva = tipo };

        Assert.Equal(esperado, ConfiguracionFiscalRules.TipoAgenteNormalizado(datos));
    }
}
