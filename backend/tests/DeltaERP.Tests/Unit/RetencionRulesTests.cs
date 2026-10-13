using System.Globalization;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class RetencionRulesTests
{
    private static readonly ParametroRetencionCalculo Isr = new(1, "ISR_PROVEEDOR_ROS", 5m, 30000m, 7m, 2500m, "BASE");
    private static readonly ParametroRetencionCalculo IvaPublico = new(2, "IVA_CLIENTE_SECTOR_PUBLICO", 25m, null, null, 30000m, "TOTAL");
    private static readonly ParametroRetencionCalculo IvaPrivado = new(3, "IVA_CLIENTE_AGENTE_PRIVADO", 15m, null, null, 2500m, "TOTAL");
    private static readonly EvaluacionIsr Retenida = new(EstadoRetencionIsr.Retenida, null, 10000m, 500m);
    private static readonly EvaluacionIsr Pendiente = new(EstadoRetencionIsr.PendienteRevision, "Falta el régimen.", 10000m, 0m);
    private static readonly EvaluacionIsr NoAplica = new(EstadoRetencionIsr.NoAplica, "Utilidades.", 10000m, 0m);

    private static decimal D(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private static Contraparte Proveedor(
        string regimenIsr = Contraparte.RegimenIsrSimplificado, string regimenIva = Contraparte.RegimenIvaGeneral,
        bool residente = true, string? fundamento = null) => new()
    {
        Tipo = "Proveedor",
        Nombre = "Proveedor X",
        RegimenIsr = regimenIsr,
        RegimenIva = regimenIva,
        EsResidente = residente,
        IsrFundamento = fundamento,
    };

    [Theory]
    [InlineData("2500.00", "125.00")]
    [InlineData("2500.01", "125.00")]
    [InlineData("30000.00", "1500.00")]
    [InlineData("30100.00", "1507.00")]
    [InlineData("33333.33", "1733.33")]
    [InlineData("40000.00", "2200.00")]
    [InlineData("100000.00", "6400.00")]
    public void CalcularEscala_AplicaElTramoYElExcedente(string baseSinIva, string esperado)
    {
        Assert.Equal(D(esperado), RetencionRules.CalcularEscala(D(baseSinIva), Isr));
    }

    [Fact]
    public void CalcularEscala_SinTramo_AplicaLaTasaBaseATodaLaBase()
    {
        var sinTramo = Isr with { TramoLimite = null, TasaExcedente = null };

        Assert.Equal(500m, RetencionRules.CalcularEscala(10000m, sinTramo));
    }

    [Theory]
    [InlineData("2499.99", EstadoRetencionIsr.NoAplica, "0")]
    [InlineData("2500.00", EstadoRetencionIsr.Retenida, "125.00")]
    [InlineData("40000.00", EstadoRetencionIsr.Retenida, "2200.00")]
    public void EvaluarIsr_ProveedorSimplificado_AplicaElUmbralDeBaseSinIva(string baseSinIva, EstadoRetencionIsr estado, string retencion)
    {
        var evaluacion = RetencionRules.EvaluarIsrProveedor(Proveedor(), TiposDocumento.Factura, D(baseSinIva), Isr);

        Assert.Equal((estado, D(retencion)), (evaluacion.Estado, evaluacion.Retencion));
    }

    [Theory]
    [InlineData(TiposDocumento.NotaCredito)]
    [InlineData(TiposDocumento.NotaDebito)]
    public void EvaluarIsr_NotasDeCreditoYDebito_NoRetienen(string tipoDocumento)
    {
        var evaluacion = RetencionRules.EvaluarIsrProveedor(Proveedor(), tipoDocumento, 10000m, Isr);

        Assert.Equal((EstadoRetencionIsr.NoAplica, 0m), (evaluacion.Estado, evaluacion.Retencion));
    }

    [Theory]
    [InlineData(Contraparte.RegimenIsrUtilidades, null, EstadoRetencionIsr.NoAplica, "utilidades")]
    [InlineData(Contraparte.RegimenIsrPagoDirecto, "Resolución 123", EstadoRetencionIsr.NoAplica, "Resolución 123")]
    [InlineData(Contraparte.RegimenIsrExento, "Art. 8", EstadoRetencionIsr.NoAplica, "Art. 8")]
    [InlineData(Contraparte.RegimenIsrNoDefinido, null, EstadoRetencionIsr.PendienteRevision, "no tiene régimen de ISR definido")]
    public void EvaluarIsr_SegunElRegimenDelProveedor(string regimen, string? fundamento, EstadoRetencionIsr estado, string fragmento)
    {
        var evaluacion = RetencionRules.EvaluarIsrProveedor(Proveedor(regimen, fundamento: fundamento), TiposDocumento.Factura, 10000m, null);

        Assert.Equal((estado, 0m), (evaluacion.Estado, evaluacion.Retencion));
        Assert.Contains(fragmento, evaluacion.Motivo);
    }

    [Fact]
    public void EvaluarIsr_ProveedorNoResidente_QuedaPendienteDeRevision()
    {
        var evaluacion = RetencionRules.EvaluarIsrProveedor(Proveedor(residente: false), TiposDocumento.Factura, 10000m, Isr);

        Assert.Equal(EstadoRetencionIsr.PendienteRevision, evaluacion.Estado);
        Assert.Contains("no residente", evaluacion.Motivo);
    }

    [Fact]
    public void EvaluarIsr_PequenoContribuyente_NoRetiene()
    {
        var proveedor = Proveedor(regimenIva: Contraparte.RegimenIvaPequenoContribuyente);

        var evaluacion = RetencionRules.EvaluarIsrProveedor(proveedor, TiposDocumento.Factura, 10000m, Isr);

        Assert.Equal(EstadoRetencionIsr.NoAplica, evaluacion.Estado);
    }

    [Fact]
    public void EvaluarIsr_SimplificadoSinParametro_LanzaExcepcion()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RetencionRules.EvaluarIsrProveedor(Proveedor(), TiposDocumento.Factura, 10000m, null));
    }

    [Theory]
    [InlineData(Contraparte.RegimenIsrSimplificado, Contraparte.RegimenIvaGeneral, true, TiposDocumento.Factura, true)]
    [InlineData(Contraparte.RegimenIsrSimplificado, Contraparte.RegimenIvaGeneral, true, TiposDocumento.NotaCredito, false)]
    [InlineData(Contraparte.RegimenIsrSimplificado, Contraparte.RegimenIvaGeneral, false, TiposDocumento.Factura, false)]
    [InlineData(Contraparte.RegimenIsrSimplificado, Contraparte.RegimenIvaPequenoContribuyente, true, TiposDocumento.Factura, false)]
    [InlineData(Contraparte.RegimenIsrUtilidades, Contraparte.RegimenIvaGeneral, true, TiposDocumento.Factura, false)]
    public void RequiereParametroIsr_SoloParaSimplificadoResidenteQueNoEsPequeno(
        string regimenIsr, string regimenIva, bool residente, string tipoDocumento, bool esperado)
    {
        Assert.Equal(esperado, RetencionRules.RequiereParametroIsr(Proveedor(regimenIsr, regimenIva, residente), tipoDocumento));
    }

    [Fact]
    public void AplicarAjuste_SinAjuste_ConservaElResultadoDelSistema()
    {
        Assert.Equal(
            (500m, null, "NINGUNA"),
            Desglosar(RetencionRules.AplicarAjusteIsr(Retenida, null, 11200m)));
        Assert.Equal((0m, null, "PENDIENTE"), Desglosar(RetencionRules.AplicarAjusteIsr(Pendiente, null, 11200m)));
        Assert.Equal((0m, null, "NINGUNA"), Desglosar(RetencionRules.AplicarAjusteIsr(NoAplica, null, 11200m)));
    }

    [Theory]
    [InlineData("300", "Acuerdo", "Acuerdo")]
    [InlineData("500", "Sin cambio", null)]
    [InlineData("0", "Excepción", "Excepción")]
    public void AplicarAjuste_EnRetenida_GuardaElMotivoSoloSiCambiaElMonto(string monto, string motivo, string? motivoAjuste)
    {
        var resultado = RetencionRules.AplicarAjusteIsr(Retenida, new AjusteIsr(D(monto), $" {motivo} "), 11200m);

        Assert.Equal((D(monto), motivoAjuste, "NINGUNA"), Desglosar(resultado));
        Assert.Null(resultado.Error);
    }

    [Theory]
    [InlineData("250", "Contador confirma")]
    [InlineData("0", "Sin retención")]
    public void AplicarAjuste_EnPendiente_ResuelveLaRevisionConElMotivo(string monto, string motivo)
    {
        var resultado = RetencionRules.AplicarAjusteIsr(Pendiente, new AjusteIsr(D(monto), motivo), 11200m);

        Assert.Equal((D(monto), motivo, "RESUELTA"), Desglosar(resultado));
        Assert.Equal(motivo, resultado.RevisionMotivo);
    }

    [Theory]
    [InlineData("100", null, false)]
    [InlineData("100", "  ", false)]
    [InlineData("-1", "Motivo", false)]
    [InlineData("100.005", "Motivo", false)]
    [InlineData("11200", "Motivo", false)]
    public void AplicarAjuste_ConDatosInvalidos_Devuelve400(string monto, string? motivo, bool esConflicto)
    {
        var resultado = RetencionRules.AplicarAjusteIsr(Retenida, new AjusteIsr(D(monto), motivo), 11200m);

        Assert.Equal(esConflicto, resultado.Error?.EsConflicto);
    }

    [Fact]
    public void AplicarAjuste_ConMotivoDemasiadoLargo_Devuelve400()
    {
        var resultado = RetencionRules.AplicarAjusteIsr(Retenida, new AjusteIsr(100m, new string('m', 301)), 11200m);

        Assert.Equal("El motivo del ajuste admite hasta 300 caracteres.", resultado.Error?.Mensaje);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("100", true)]
    public void AplicarAjuste_EnNoAplica_NoPermiteImponerRetencion(string monto, bool esConflicto)
    {
        var resultado = RetencionRules.AplicarAjusteIsr(NoAplica, new AjusteIsr(D(monto), "Motivo"), 11200m);

        Assert.Equal(esConflicto, resultado.Error?.EsConflicto);
    }

    [Theory]
    [InlineData("3600", "30000", "33600", 2, "900.00")]
    [InlineData("3600", "29999.99", "29999.99", 2, "0")]
    [InlineData("3600", "30000", "33600", 3, "540.00")]
    [InlineData("2400", "30000", "33600", 2, "600.00")]
    [InlineData("12", "100", "112", 3, "0")]
    public void SugerirIvaCliente_AplicaLaTasaSobreElIvaYElUmbralDelTotal(string iva, string baseFactura, string total, int parametro, string esperado)
    {
        var calculo = parametro == 2 ? IvaPublico : IvaPrivado;

        Assert.Equal(D(esperado), RetencionRules.SugerirIvaCliente(D(iva), D(baseFactura), D(total), calculo));
    }

    [Fact]
    public void SugerirIvaCliente_ConUmbralSobreBase_ComparaContraLaBase()
    {
        var sobreBase = IvaPrivado with { UmbralSobre = "BASE" };

        Assert.Equal(0m, RetencionRules.SugerirIvaCliente(300m, 2400m, 2700m, sobreBase));
        Assert.Equal(45m, RetencionRules.SugerirIvaCliente(300m, 2500m, 2800m, sobreBase));
    }

    [Theory]
    [InlineData(true, Contraparte.AgenteSectorPublico, ParametroRetencion.CodigoIvaSectorPublico)]
    [InlineData(true, Contraparte.AgentePrivado, ParametroRetencion.CodigoIvaAgentePrivado)]
    [InlineData(false, null, ParametroRetencion.CodigoIvaAgentePrivado)]
    public void CodigoParametroIvaCliente_SegunElTipoDeAgente(bool agente, string? tipo, string esperado)
    {
        var cliente = new Contraparte { EsAgenteRetencionIva = agente, TipoAgenteIva = tipo };

        Assert.Equal(esperado, RetencionRules.CodigoParametroIvaCliente(cliente));
    }

    [Fact]
    public void ValidarIvaCliente_ConMontoDentroDelTope_NoDevuelveError()
    {
        Assert.Null(RetencionRules.ValidarIvaCliente(900m, 33600m, 3600m));
    }

    [Theory]
    [InlineData("0", "33600", "3600", "mayor a cero", false)]
    [InlineData("900.005", "33600", "3600", "2 decimales", false)]
    [InlineData("3600.01", "33600", "3600", "excede el IVA retenible", true)]
    [InlineData("900", "500", "3600", "excede el monto aplicado", true)]
    public void ValidarIvaCliente_ValidaMontoTopeYMontoAplicado(string monto, string aplicado, string retenible, string fragmento, bool esConflicto)
    {
        var error = RetencionRules.ValidarIvaCliente(D(monto), D(aplicado), D(retenible));

        Assert.Contains(fragmento, error?.Mensaje);
        Assert.Equal(esConflicto, error?.EsConflicto);
    }

    [Theory]
    [InlineData("40000", "2200", "5.5000")]
    [InlineData("0", "0", "0")]
    public void TasaEfectiva_EsElMontoSobreLaBase(string baseRetencion, string monto, string esperado)
    {
        Assert.Equal(D(esperado), RetencionRules.TasaEfectiva(D(baseRetencion), D(monto)));
    }

    [Theory]
    [InlineData("ISR_ROS", "2026-03-15", "2026-04-05", 5, null, "PENDIENTE")]
    [InlineData("ISR_ROS", "2026-03-15", "2026-04-06", 5, null, "VENCIDA")]
    [InlineData("ISR_ROS", "2026-03-15", "2026-09-01", null, null, "PENDIENTE")]
    [InlineData("ISR_ROS", "2026-03-15", "2026-09-01", 5, "EMITIDA", "EMITIDA")]
    [InlineData("ISR_ROS", "2026-03-15", "2026-09-01", 5, "ANULADA", "ANULADA")]
    [InlineData("IVA_CLIENTE", "2026-03-15", "2026-09-01", 5, null, "PENDIENTE")]
    public void EstadoConstancia_SegunElPlazoDelMesSiguiente(
        string concepto, string fechaDocumento, string hoy, int? diaLimite, string? estadoEmitida, string esperado)
    {
        var estado = RetencionRules.EstadoConstancia(concepto, DateOnly.Parse(fechaDocumento, CultureInfo.InvariantCulture), DateOnly.Parse(hoy, CultureInfo.InvariantCulture), diaLimite, estadoEmitida);

        Assert.Equal(esperado, estado);
    }

    [Theory]
    [InlineData("ISR_ROS", "2026-03-15", "2026-03-15", 0)]
    [InlineData("ISR_ROS", "2026-03-15", "2026-04-01", 1)]
    [InlineData("IVA_CLIENTE", "2026-03-15", "2026-04-01", 0)]
    public void AdvertenciasConstancia_SoloAvisaEnIsrConFechaDistintaALaFactura(string concepto, string fechaDocumento, string fechaConstancia, int cantidad)
    {
        var advertencias = RetencionRules.AdvertenciasConstancia(concepto, DateOnly.Parse(fechaDocumento, CultureInfo.InvariantCulture), DateOnly.Parse(fechaConstancia, CultureInfo.InvariantCulture));

        Assert.Equal(cantidad, advertencias.Count);
    }

    [Fact]
    public void Quetzales_FormateaConSeparadorDeMilesYDosDecimales()
    {
        Assert.Equal("Q2,500.00", RetencionRules.Quetzales(2500m));
    }

    [Fact]
    public void ComoCalculo_CopiaLosDatosDelParametro()
    {
        var parametro = new ParametroRetencion
        {
            Id = 7, Codigo = "X", TasaBase = 5m, TramoLimite = 30000m, TasaExcedente = 7m, UmbralMinimo = 2500m, UmbralSobre = "BASE",
        };

        Assert.Equal(new ParametroRetencionCalculo(7, "X", 5m, 30000m, 7m, 2500m, "BASE"), RetencionRules.ComoCalculo(parametro));
    }

    private static (decimal Monto, string? MotivoAjuste, string Revision) Desglosar(ResultadoAjusteIsr r) => (r.Monto, r.MotivoAjuste, r.Revision);
}
