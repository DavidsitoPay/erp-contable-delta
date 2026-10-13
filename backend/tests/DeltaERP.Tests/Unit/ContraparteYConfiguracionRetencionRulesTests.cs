using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ContraparteYConfiguracionRetencionRulesTests
{
    private static Contraparte Proveedor(string regimenIsr, string? fundamento = null, bool agente = false, string? tipoAgente = null) => new()
    {
        Nit = "1234567-9",
        RegimenIsr = regimenIsr,
        IsrFundamento = fundamento,
        EsAgenteRetencionIva = agente,
        TipoAgenteIva = tipoAgente,
    };

    [Theory]
    [InlineData(Contraparte.RegimenIsrExento, null)]
    [InlineData(Contraparte.RegimenIsrPagoDirecto, "  ")]
    public void ExentoOPagoDirecto_ExigenFundamento(string regimen, string? fundamento)
    {
        Assert.Equal(
            "El régimen de ISR EXENTO o PAGO_DIRECTO exige el fundamento legal o el número de resolución.",
            ContraparteRules.ValidarFiscal(Proveedor(regimen, fundamento), "Proveedor"));
    }

    [Theory]
    [InlineData(Contraparte.RegimenIsrExento, "Art. 8")]
    [InlineData(Contraparte.RegimenIsrPagoDirecto, "Resolución 123")]
    [InlineData(Contraparte.RegimenIsrNoDefinido, null)]
    [InlineData(Contraparte.RegimenIsrSimplificado, null)]
    public void RegimenesIsr_ConLosDatosRequeridos_SonValidos(string regimen, string? fundamento)
    {
        Assert.Null(ContraparteRules.ValidarFiscal(Proveedor(regimen, fundamento), "Proveedor"));
    }

    [Fact]
    public void Fundamento_ConMasDe200Caracteres_SeRechaza()
    {
        var error = ContraparteRules.ValidarFiscal(Proveedor(Contraparte.RegimenIsrExento, new string('f', 201)), "Proveedor");

        Assert.Equal("El fundamento del régimen de ISR admite hasta 200 caracteres.", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("OTRO")]
    public void Agente_ExigeUnTipoValido(string? tipo)
    {
        var error = ContraparteRules.ValidarFiscal(Proveedor(Contraparte.RegimenIsrNoDefinido, agente: true, tipoAgente: tipo), "Cliente");

        Assert.Equal("Si es agente de retención de IVA debe indicar el tipo de agente: SECTOR_PUBLICO, AGENTE_PRIVADO.", error);
    }

    [Fact]
    public void Agente_ConTipoValido_EsValido()
    {
        var contraparte = Proveedor(Contraparte.RegimenIsrNoDefinido, agente: true, tipoAgente: Contraparte.AgenteSectorPublico);

        Assert.Null(ContraparteRules.ValidarFiscal(contraparte, "Cliente"));
    }

    [Theory]
    [InlineData(Contraparte.RegimenIsrExento, " Art. 8 ", "Art. 8")]
    [InlineData(Contraparte.RegimenIsrUtilidades, "Art. 8", null)]
    public void FundamentoNormalizado_SoloSeConservaSiElRegimenLoExige(string regimen, string fundamento, string? esperado)
    {
        Assert.Equal(esperado, ContraparteRules.FundamentoNormalizado(Proveedor(regimen, fundamento)));
    }

    [Theory]
    [InlineData(true, "SECTOR_PUBLICO", "SECTOR_PUBLICO")]
    [InlineData(false, "SECTOR_PUBLICO", null)]
    public void TipoAgenteNormalizado_DescartaElTipoSiNoEsAgente(bool agente, string tipo, string? esperado)
    {
        Assert.Equal(esperado, ContraparteRules.TipoAgenteNormalizado(Proveedor("NO_DEFINIDO", agente: agente, tipoAgente: tipo)));
    }

    [Theory]
    [InlineData(10, 11, 10, null)]
    [InlineData(10, 11, null, 10)]
    [InlineData(10, 11, 12, 12)]
    [InlineData(10, 11, 12, 11)]
    public void CuentasDeRetencion_NoPuedenRepetirseNiCoincidirConLasDeIva(int debito, int credito, int? isr, int? ivaClientes)
    {
        var datos = new DatosConfiguracionFiscal("1234567-9", "Delta S.A.", "UTILIDADES", false, null, debito, credito, isr, ivaClientes);

        Assert.StartsWith("Las cuentas de IVA y de retención", ConfiguracionFiscalRules.Validar(datos));
    }

    [Fact]
    public void CuentasDeRetencion_Distintas_SonValidas()
    {
        var datos = new DatosConfiguracionFiscal("1234567-9", "Delta S.A.", "UTILIDADES", false, null, 10, 11, 12, 13);

        Assert.Null(ConfiguracionFiscalRules.Validar(datos));
    }
}
