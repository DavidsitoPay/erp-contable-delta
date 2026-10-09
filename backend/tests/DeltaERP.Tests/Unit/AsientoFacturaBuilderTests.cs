using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class AsientoFacturaBuilderTests
{
    private const int CuentaIva = 900;

    private static readonly ParametroImpuesto Iva = new(1, TipoImpuesto.IvaGeneral, 12m, true);
    private static readonly ParametroImpuesto IvaSinCredito = new(2, TipoImpuesto.IvaGeneral, 12m, false);
    private static readonly ParametroImpuesto Exento = new(3, TipoImpuesto.Exento, 0m, false);

    private static DocumentoCxC Documento(int cantidadLineas, string tipo = TiposDocumento.Factura) => new()
    {
        Numero = "F-1",
        TipoDocumento = tipo,
        Fecha = new DateOnly(2025, 3, 15),
        PeriodoId = 7,
        CuentaControlId = 100,
        TipoCambioAplicado = 1m,
        Lineas = Enumerable.Range(0, cantidadLineas)
            .Select(i => new LineaDocumentoCxC { CuentaContableId = 200 + i, CentroCostoId = i == 0 ? 5 : null })
            .ToList(),
    };

    private static ResumenFiscal Resumen(params (decimal Cantidad, decimal Precio, ParametroImpuesto Impuesto)[] lineas) =>
        MotorFiscal.Calcular(lineas.Select(l => new EntradaLinea(l.Cantidad, l.Precio, l.Impuesto)));

    private static (int Cuenta, decimal Debito, decimal Credito) Linea(AsientoContable asiento, int indice) =>
        (asiento.Lineas[indice].CuentaId, asiento.Lineas[indice].Debito, asiento.Lineas[indice].Credito);

    [Fact]
    public void CxCConIva_DebitaControlYAcreditaIngresoEIvaDebito()
    {
        var resumen = Resumen((2m, 100m, Iva), (1m, 50.50m, Exento));

        var asiento = AsientoFacturaBuilder.Construir("CXC", Documento(2), resumen, LadoControl.Debito, 9, CuentaIva);

        Assert.Equal("CXC-F-1", asiento.Numero);
        Assert.Equal(("Confirmado", 9, 7, 250.50m), (asiento.Estado, asiento.UsuarioId, asiento.PeriodoId, asiento.Monto));
        Assert.Equal(4, asiento.Lineas.Count);
        Assert.Equal((100, 250.50m, 0m), Linea(asiento, 0));
        Assert.Equal((200, 0m, 178.57m), Linea(asiento, 1));
        Assert.Equal((201, 0m, 50.50m), Linea(asiento, 2));
        Assert.Equal((CuentaIva, 0m, 21.43m), Linea(asiento, 3));
        Assert.Equal((5, null), (asiento.Lineas[1].CentroCostoId, asiento.Lineas[2].CentroCostoId));
    }

    [Fact]
    public void CxCExento_NoGeneraLineaDeIvaYNoRequiereCuenta()
    {
        var asiento = AsientoFacturaBuilder.Construir("CXC", Documento(1), Resumen((1m, 100m, Exento)), LadoControl.Debito, 9, null);

        Assert.Equal(2, asiento.Lineas.Count);
        Assert.Equal((200, 0m, 100m), Linea(asiento, 1));
    }

    [Fact]
    public void CxPConCredito_DebitaGastoEIvaCreditoYAcreditaControl()
    {
        var resumen = Resumen((2m, 100m, Iva), (1m, 50.50m, Exento));

        var asiento = AsientoFacturaBuilder.Construir("CXP", Documento(2), resumen, LadoControl.Credito, 9, CuentaIva);

        Assert.Equal((100, 0m, 250.50m), Linea(asiento, 0));
        Assert.Equal((200, 178.57m, 0m), Linea(asiento, 1));
        Assert.Equal((201, 50.50m, 0m), Linea(asiento, 2));
        Assert.Equal((CuentaIva, 21.43m, 0m), Linea(asiento, 3));
    }

    [Fact]
    public void CxPSinCredito_LlevaElIvaAlGastoYNoGeneraLineaDeIva()
    {
        var asiento = AsientoFacturaBuilder.Construir("CXP", Documento(1), Resumen((2m, 100m, IvaSinCredito)), LadoControl.Credito, 9, null);

        Assert.Equal(2, asiento.Lineas.Count);
        Assert.Equal((100, 0m, 200m), Linea(asiento, 0));
        Assert.Equal((200, 200m, 0m), Linea(asiento, 1));
    }

    [Fact]
    public void CxPMixto_SoloElIvaAcreditableVaALineaDeIva()
    {
        var resumen = Resumen((1m, 112m, Iva), (1m, 112m, IvaSinCredito));

        var asiento = AsientoFacturaBuilder.Construir("CXP", Documento(2), resumen, LadoControl.Credito, 9, CuentaIva);

        Assert.Equal((200, 100m, 0m), Linea(asiento, 1));
        Assert.Equal((201, 112m, 0m), Linea(asiento, 2));
        Assert.Equal((CuentaIva, 12m, 0m), Linea(asiento, 3));
    }

    [Fact]
    public void NotaDeCreditoCxC_InvierteLosLadosDelAsiento()
    {
        var resumen = Resumen((2m, 100m, Iva));

        var asiento = AsientoFacturaBuilder.Construir("CXC", Documento(1, TiposDocumento.NotaCredito), resumen, LadoControl.Debito, 9, CuentaIva);

        Assert.Equal((100, 0m, 200m), Linea(asiento, 0));
        Assert.Equal((200, 178.57m, 0m), Linea(asiento, 1));
        Assert.Equal((CuentaIva, 21.43m, 0m), Linea(asiento, 2));
    }

    [Fact]
    public void NotaDeCreditoCxP_InvierteLosLadosDelAsiento()
    {
        var resumen = Resumen((2m, 100m, Iva));

        var asiento = AsientoFacturaBuilder.Construir("CXP", Documento(1, TiposDocumento.NotaCredito), resumen, LadoControl.Credito, 9, CuentaIva);

        Assert.Equal((100, 200m, 0m), Linea(asiento, 0));
        Assert.Equal((200, 0m, 178.57m), Linea(asiento, 1));
        Assert.Equal((CuentaIva, 0m, 21.43m), Linea(asiento, 2));
    }

    [Fact]
    public void ConIvaYSinCuentaDeIva_LanzaInvalidOperationException()
    {
        var resumen = Resumen((1m, 112m, Iva));

        var ex = Assert.Throws<InvalidOperationException>(
            () => AsientoFacturaBuilder.Construir("CXC", Documento(1), resumen, LadoControl.Debito, 9, null));

        Assert.Contains("cuenta de IVA", ex.Message);
    }

    [Theory]
    [InlineData(LadoControl.Debito, TiposDocumento.Factura)]
    [InlineData(LadoControl.Credito, TiposDocumento.Factura)]
    [InlineData(LadoControl.Debito, TiposDocumento.NotaCredito)]
    [InlineData(LadoControl.Credito, TiposDocumento.NotaCredito)]
    [InlineData(LadoControl.Credito, TiposDocumento.NotaDebito)]
    public void AsientoGenerado_CumplePartidaDobleConRedondeoPorLinea(LadoControl lado, string tipo)
    {
        var resumen = Resumen((3m, 33.333m, Iva), (1.5m, 10.005m, IvaSinCredito), (1m, 7.77m, Exento));

        var asiento = AsientoFacturaBuilder.Construir("CXC", Documento(3, tipo), resumen, lado, 9, CuentaIva);

        Assert.True(PartidaDobleValidator.EsValido(asiento, out var error), error);
    }
}
