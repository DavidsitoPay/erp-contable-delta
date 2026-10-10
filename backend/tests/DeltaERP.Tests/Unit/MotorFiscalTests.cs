using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class MotorFiscalTests
{
    private static readonly ParametroImpuesto Iva = new(1, TipoImpuesto.IvaGeneral, 12m, true);
    private static readonly ParametroImpuesto IvaSinCredito = new(2, TipoImpuesto.IvaGeneral, 12m, false);
    private static readonly ParametroImpuesto Exento = new(3, TipoImpuesto.Exento, 0m, false);
    private static readonly ParametroImpuesto NoAfecto = new(4, TipoImpuesto.NoAfecto, 0m, false);
    private static readonly ParametroImpuesto Pequeno = new(5, TipoImpuesto.PequenoContribuyente, 5m, false);
    private static readonly ParametroImpuesto Historico = new(6, TipoImpuesto.Legado, 7m, false);

    public static TheoryData<decimal, decimal, decimal, decimal, decimal> VectoresIvaIncluido => new()
    {
        { 1m, 112m, 112m, 12m, 100m },
        { 1m, 100m, 100m, 10.71m, 89.29m },
        { 1m, 274.50m, 274.50m, 29.41m, 245.09m },
        { 2m, 100m, 200m, 21.43m, 178.57m },
        { 3m, 33.333m, 100m, 10.71m, 89.29m },
        { 1.5m, 10.005m, 15.01m, 1.61m, 13.40m },
    };

    [Theory]
    [MemberData(nameof(VectoresIvaIncluido))]
    public void IvaGeneral_ExtraeElIvaDelPrecioConIvaIncluido(decimal cantidad, decimal precio, decimal total, decimal iva, decimal @base)
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(cantidad, precio, Iva));

        Assert.Equal((total, iva, @base, 12m, true), (resultado.MontoLinea, resultado.Iva, resultado.Base, resultado.TasaAplicada, resultado.IvaAcreditable));
    }

    [Theory]
    [InlineData(TipoImpuesto.Exento)]
    [InlineData(TipoImpuesto.NoAfecto)]
    [InlineData(TipoImpuesto.PequenoContribuyente)]
    public void ImpuestosSinIva_NoSeparanIvaYTodoElValorEsBase(TipoImpuesto tipo)
    {
        var parametro = new ParametroImpuesto(9, tipo, tipo == TipoImpuesto.PequenoContribuyente ? 5m : 0m, false);

        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(1m, 50.50m, parametro));

        Assert.Equal((50.50m, 0m, 50.50m, false), (resultado.MontoLinea, resultado.Iva, resultado.Base, resultado.IvaAcreditable));
    }

    [Fact]
    public void IvaGeneralSinCredito_CalculaIvaPeroNoEsAcreditable()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(2m, 100m, IvaSinCredito));

        Assert.Equal((21.43m, 178.57m, false), (resultado.Iva, resultado.Base, resultado.IvaAcreditable));
    }

    [Fact]
    public void ImpuestoHistorico_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MotorFiscal.CalcularLinea(new EntradaLinea(1m, 10m, Historico)));

        Assert.Contains("históricos", ex.Message);
    }

    [Fact]
    public void TipoCambio_ConvierteElTotalDeLineaYRedondea()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(1m, 10.005m, Iva), 7.75m);

        Assert.Equal(77.58m, resultado.MontoLinea);
        Assert.Equal(resultado.MontoLinea, resultado.Base + resultado.Iva);
    }

    [Fact]
    public void Calcular_MezclaDeLineas_SumaLineasYSoloElIvaAcreditable()
    {
        var resumen = MotorFiscal.Calcular(new[]
        {
            new EntradaLinea(2m, 100m, Iva, TipoBienServicio.Bien),
            new EntradaLinea(1m, 50.50m, Exento),
            new EntradaLinea(1m, 112m, IvaSinCredito),
        });

        Assert.Equal((362.50m, 329.07m, 33.43m, 21.43m), (resumen.Total, resumen.Base, resumen.Iva, resumen.IvaAcreditable));
        Assert.Equal(resumen.Total, resumen.Base + resumen.Iva);
        Assert.Equal(TipoBienServicio.Bien, resumen.Lineas[0].TipoBienServicio);
        Assert.Equal(TipoBienServicio.Servicio, resumen.Lineas[1].TipoBienServicio);
    }

    [Fact]
    public void Calcular_ConDosLineasDeIva_ElTotalEsLaSumaDeLineasRedondeadas()
    {
        var resumen = MotorFiscal.Calcular(new[] { new EntradaLinea(1m, 100m, Iva), new EntradaLinea(1m, 100m, Iva) });

        Assert.Equal((200m, 21.42m, 178.58m), (resumen.Total, resumen.Iva, resumen.Base));
    }

    [Theory]
    [InlineData(TipoImpuesto.Exento, TipoBienServicio.Bien, ColumnaLibro.Exento)]
    [InlineData(TipoImpuesto.NoAfecto, TipoBienServicio.Servicio, ColumnaLibro.Exento)]
    [InlineData(TipoImpuesto.IvaGeneral, TipoBienServicio.Bien, ColumnaLibro.BaseBien)]
    [InlineData(TipoImpuesto.IvaGeneral, TipoBienServicio.Servicio, ColumnaLibro.BaseServicio)]
    [InlineData(TipoImpuesto.PequenoContribuyente, TipoBienServicio.Bien, ColumnaLibro.BaseBien)]
    [InlineData(TipoImpuesto.Legado, TipoBienServicio.Servicio, ColumnaLibro.BaseServicio)]
    public void ClasificarParaLibro_UbicaLaBaseSegunTipoDeImpuestoYDeLinea(TipoImpuesto tipo, string tipoBienServicio, ColumnaLibro esperada)
    {
        var columna = MotorFiscal.ClasificarParaLibro(new ParametroImpuesto(1, tipo, 5m, true), tipoBienServicio);

        Assert.Equal(esperada, columna);
    }

    [Fact]
    public void DistribuirParaLibro_VentaGravada_SeparaBaseEIva()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(2m, 100m, Iva));

        var importes = MotorFiscal.DistribuirParaLibro(Iva, resultado, esVenta: true);

        Assert.Equal(new ImportesLibro(0m, 178.57m, 0m, 21.43m), importes);
    }

    [Fact]
    public void DistribuirParaLibro_CompraConCredito_SeparaIvaYBaseDeBienes()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(2m, 100m, Iva, TipoBienServicio.Bien));

        var importes = MotorFiscal.DistribuirParaLibro(Iva, resultado, esVenta: false);

        Assert.Equal(new ImportesLibro(178.57m, 0m, 0m, 21.43m), importes);
    }

    [Fact]
    public void DistribuirParaLibro_CompraSinCredito_EnviaTodoElValorALaBase()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(2m, 100m, IvaSinCredito, TipoBienServicio.Bien));

        var importes = MotorFiscal.DistribuirParaLibro(IvaSinCredito, resultado, esVenta: false);

        Assert.Equal(new ImportesLibro(200m, 0m, 0m, 0m), importes);
    }

    [Fact]
    public void DistribuirParaLibro_PequenoContribuyente_EsBaseSinIva()
    {
        var resultado = MotorFiscal.CalcularLinea(new EntradaLinea(1m, 100m, Pequeno));

        var importes = MotorFiscal.DistribuirParaLibro(Pequeno, resultado, esVenta: false);

        Assert.Equal(new ImportesLibro(0m, 100m, 0m, 0m), importes);
    }

    [Fact]
    public void DistribuirParaLibro_ExentoYNoAfecto_VanALaColumnaExento()
    {
        var exento = MotorFiscal.DistribuirParaLibro(Exento, MotorFiscal.CalcularLinea(new EntradaLinea(1m, 50.50m, Exento)), esVenta: true);
        var noAfecto = MotorFiscal.DistribuirParaLibro(NoAfecto, MotorFiscal.CalcularLinea(new EntradaLinea(1m, 10m, NoAfecto)), esVenta: false);

        Assert.Equal(new ImportesLibro(0m, 0m, 50.50m, 0m), exento);
        Assert.Equal(new ImportesLibro(0m, 0m, 10m, 0m), noAfecto);
    }

    [Theory]
    [InlineData(true, 12.0)]
    [InlineData(false, 0.0)]
    public void DistribuirParaLibro_LineaHistoricaGravada_SoloSeparaIvaEnVentas(bool esVenta, double ivaEsperado)
    {
        var resultado = new ResultadoLinea(112m, 100m, 12m, 12m, false, TipoBienServicio.Servicio);

        var importes = MotorFiscal.DistribuirParaLibro(new ParametroImpuesto(7, TipoImpuesto.Legado, 12m, false), resultado, esVenta);

        Assert.Equal((decimal)ivaEsperado, importes.Iva);
        Assert.Equal(112m, importes.BaseServicios + importes.Iva);
    }
}
