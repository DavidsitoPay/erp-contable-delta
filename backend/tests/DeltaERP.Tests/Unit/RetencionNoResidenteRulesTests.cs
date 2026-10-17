using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class RetencionNoResidenteRulesTests
{
    private static Contraparte Nr(string regimen = "SIMPLIFICADO", string? tipoRenta = null, bool residente = false) =>
        new() { Tipo = "Proveedor", Nombre = "NR", EsResidente = residente, RegimenIsr = regimen, TipoRentaNrCodigo = tipoRenta };

    [Theory]
    [InlineData(false, "SIMPLIFICADO", true)]
    [InlineData(true, "SIMPLIFICADO", false)]
    [InlineData(false, "EXENTO", false)]
    public void Aplica_SoloParaNoResidentesNoExentos(bool residente, string regimen, bool esperado) =>
        Assert.Equal(esperado, RetencionNoResidenteRules.Aplica(Nr(regimen, residente: residente)));

    [Theory]
    [InlineData(null, "NR_A", "NR_A")]
    [InlineData(" nr_b ", "NR_A", "NR_B")]
    [InlineData("", null, null)]
    public void CodigoRenta_UsaElElegidoOElDelProveedor(string? elegido, string? delProveedor, string? esperado) =>
        Assert.Equal(esperado, RetencionNoResidenteRules.CodigoRenta(Nr(tipoRenta: delProveedor), elegido));

    [Fact]
    public void BaseYRetencion_RedondeanADosDecimales()
    {
        var baseGtq = RetencionNoResidenteRules.BaseGtq(33.33m, 7.64157m);

        Assert.Equal((254.69m, 38.20m), (baseGtq, RetencionNoResidenteRules.Retencion(baseGtq, 15m)));
    }

    [Fact]
    public void EvaluarIsr_NoResidenteConTipoDeRentaOExento_NoAplicaYSinTipoQuedaPendiente()
    {
        var conCodigo = RetencionRules.EvaluarIsrProveedor(Nr(tipoRenta: "NR_REGALIAS"), TiposDocumento.Factura, 1000m, null);
        var exento = RetencionRules.EvaluarIsrProveedor(Nr("EXENTO"), TiposDocumento.Factura, 1000m, null);
        var sinCodigo = RetencionRules.EvaluarIsrProveedor(Nr(), TiposDocumento.Factura, 1000m, null);

        Assert.Equal(
            (EstadoRetencionIsr.NoAplica, EstadoRetencionIsr.NoAplica, EstadoRetencionIsr.PendienteRevision),
            (conCodigo.Estado, exento.Estado, sinCodigo.Estado));
        Assert.Equal(RetencionRules.EstadoNoAplica, RetencionRules.EstadoConstancia(Retencion.ConceptoIsrNoResidente, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), null, null));
    }

    [Theory]
    [InlineData("SIMPLIFICADO", null, true)]
    [InlineData("SIMPLIFICADO", "NR_REGALIAS", false)]
    [InlineData("EXENTO", null, false)]
    public void ValidarTipoRentaNr_ExigeCodigoOExencion(string regimen, string? codigo, bool hayError)
    {
        Assert.Equal(hayError, ContraparteRules.ValidarTipoRentaNr(Nr(regimen, codigo), "Proveedor") is not null);
        Assert.Null(ContraparteRules.ValidarTipoRentaNr(Nr(regimen, codigo), "Cliente"));
    }

    [Fact]
    public void TipoRentaNrNormalizado_DescartaElCodigoDeResidentesYNormalizaElDeNoResidentes()
    {
        Assert.Equal(
            ("NR_X", null, null),
            (ContraparteRules.TipoRentaNrNormalizado(Nr(tipoRenta: " nr_x "), "Proveedor"),
                ContraparteRules.TipoRentaNrNormalizado(Nr(tipoRenta: "NR_X", residente: true), "Proveedor"),
                ContraparteRules.TipoRentaNrNormalizado(Nr(tipoRenta: "NR_X"), "Cliente")));
    }

    [Fact]
    public void ParametroIsrNoResidente_AceptaTasaSinTramoNiDiaLimite()
    {
        Assert.Contains(ParametroRetencion.ConceptoIsrNoResidente, ParametroRetencion.ConceptosValidos);
    }
}
