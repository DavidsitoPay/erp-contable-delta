using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class RetencionAsientosTests
{
    private const int CuentaIva = 900;
    private const int CuentaIsr = 300;

    private static readonly ParametroImpuesto Iva = new(1, TipoImpuesto.IvaGeneral, 12m, true);

    private static DocumentoCxP Compra(string tipo = TiposDocumento.Factura) => new()
    {
        Numero = "P-1",
        TipoDocumento = tipo,
        Fecha = new DateOnly(2025, 3, 15),
        PeriodoId = 7,
        CuentaControlId = 100,
        TipoCambioAplicado = 1m,
        Lineas = new List<LineaDocumentoCxP> { new() { CuentaContableId = 200 } },
    };

    private static ResumenFiscal ResumenDe(decimal totalConIva) =>
        MotorFiscal.Calcular(new[] { new EntradaLinea(1m, totalConIva, Iva) });

    private static (int Cuenta, decimal Debito, decimal Credito) Linea(AsientoContable asiento, int indice) =>
        (asiento.Lineas[indice].CuentaId, asiento.Lineas[indice].Debito, asiento.Lineas[indice].Credito);

    [Fact]
    public void FacturaDeCompraConRetencion_AcreditaControlPorElNetoYLaRetencionAlFinal()
    {
        var asiento = AsientoFacturaBuilder.Construir(
            "CXP", Compra(), ResumenDe(11200m), LadoControl.Credito, 9, CuentaIva, new RetencionAsiento(CuentaIsr, 500m));

        Assert.Equal(4, asiento.Lineas.Count);
        Assert.Equal((100, 0m, 10700m), Linea(asiento, 0));
        Assert.Equal((200, 10000m, 0m), Linea(asiento, 1));
        Assert.Equal((CuentaIva, 1200m, 0m), Linea(asiento, 2));
        Assert.Equal((CuentaIsr, 0m, 500m), Linea(asiento, 3));
        Assert.Equal(11200m, asiento.Monto);
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void FacturaDeCompraSinRetencion_ConservaElAsientoOriginal()
    {
        var asiento = AsientoFacturaBuilder.Construir("CXP", Compra(), ResumenDe(11200m), LadoControl.Credito, 9, CuentaIva);

        Assert.Equal(3, asiento.Lineas.Count);
        Assert.Equal((100, 0m, 11200m), Linea(asiento, 0));
    }

    [Theory]
    [InlineData(LadoControl.Debito, TiposDocumento.Factura)]
    [InlineData(LadoControl.Credito, TiposDocumento.NotaCredito)]
    public void Retencion_SoloAplicaAFacturasDeCompra(LadoControl lado, string tipo)
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsientoFacturaBuilder.Construir("CXP", Compra(tipo), ResumenDe(11200m), lado, 9, CuentaIva, new RetencionAsiento(CuentaIsr, 500m)));
    }

    [Fact]
    public void CobroConRetenciones_DebitaBancoPorElNetoYLasRetencionesYAcreditaElTotal()
    {
        var contrapartidas = new[] { (CuentaId: 20, Monto: 33600m) };
        var retenciones = new[] { (CuentaId: 30, Monto: 900m) };

        var asiento = AsientoTesoreriaBuilder.ConstruirCobroConRetenciones(
            "TES-CXC", new DateOnly(2025, 3, 20), 7, 9, 10, contrapartidas, retenciones);

        Assert.Equal(3, asiento.Lineas.Count);
        Assert.Equal((10, 32700m, 0m), Linea(asiento, 0));
        Assert.Equal((20, 0m, 33600m), Linea(asiento, 1));
        Assert.Equal((30, 900m, 0m), Linea(asiento, 2));
        Assert.Equal(33600m, asiento.Monto);
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void CobroConRetenciones_NoAdmiteRetenerTodoElCobro()
    {
        var contrapartidas = new[] { (CuentaId: 20, Monto: 900m) };
        var retenciones = new[] { (CuentaId: 30, Monto: 900m) };

        Assert.Throws<InvalidOperationException>(() =>
            AsientoTesoreriaBuilder.ConstruirCobroConRetenciones("TES-CXC", new DateOnly(2025, 3, 20), 7, 9, 10, contrapartidas, retenciones));
    }

    [Theory]
    [InlineData("100", "0", true)]
    [InlineData("100", "99.99", true)]
    [InlineData("100", "100", false)]
    [InlineData("100", "150", false)]
    public void ValidarCobroNeto_ExigeUnNetoPositivoCuandoHayRetencion(string total, string retenido, bool valido)
    {
        var error = TesoreriaRules.ValidarCobroNeto(decimal.Parse(total), decimal.Parse(retenido));

        Assert.Equal(valido, error is null);
    }
}
