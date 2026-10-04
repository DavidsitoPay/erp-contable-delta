using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class AsientoFacturaBuilderTests
{
    private static DocumentoCxC Factura(params (decimal Cantidad, decimal Precio, decimal Impuesto)[] lineas) => new()
    {
        Numero = "F-1",
        Fecha = new DateOnly(2025, 3, 15),
        PeriodoId = 7,
        CuentaControlId = 100,
        TipoCambioAplicado = 7.75m,
        Lineas = lineas
            .Select((l, i) => new LineaDocumentoCxC
            {
                Cantidad = l.Cantidad,
                PrecioUnitario = l.Precio,
                PorcentajeImpuesto = l.Impuesto,
                CuentaContableId = 200 + i,
                CentroCostoId = i == 0 ? 5 : null,
            })
            .ToList(),
    };

    [Fact]
    public void MontoLinea_RedondeaAlejandoseDeCeroEnElPuntoMedio()
    {
        var linea = new LineaDocumentoCxC { Cantidad = 1m, PrecioUnitario = 10.005m, PorcentajeImpuesto = 0m };

        Assert.Equal(10.01m, AsientoFacturaBuilder.MontoLinea(linea));
    }

    [Fact]
    public void LadoDebito_DebitaLaCuentaDeControlYAcreditaCadaLinea()
    {
        var asiento = AsientoFacturaBuilder.Construir("CXC", Factura((2m, 100m, 12m), (1m, 50.50m, 0m)), LadoControl.Debito, 9);

        Assert.Equal("CXC-F-1", asiento.Numero);
        Assert.Equal("Confirmado", asiento.Estado);
        Assert.Equal(9, asiento.UsuarioId);
        Assert.Equal(7, asiento.PeriodoId);
        Assert.Equal(274.50m, asiento.Monto);
        Assert.Equal(3, asiento.Lineas.Count);
        Assert.Equal((100, 274.50m, 0m), (asiento.Lineas[0].CuentaId, asiento.Lineas[0].Debito, asiento.Lineas[0].Credito));
        Assert.Equal((200, 0m, 224m, (int?)5), (asiento.Lineas[1].CuentaId, asiento.Lineas[1].Debito, asiento.Lineas[1].Credito, asiento.Lineas[1].CentroCostoId));
        Assert.Equal((201, 0m, 50.50m, (int?)null), (asiento.Lineas[2].CuentaId, asiento.Lineas[2].Debito, asiento.Lineas[2].Credito, asiento.Lineas[2].CentroCostoId));
    }

    [Fact]
    public void LadoCredito_AcreditaLaCuentaDeControlYDebitaCadaLinea()
    {
        var asiento = AsientoFacturaBuilder.Construir("CXP", Factura((1m, 80m, 0m), (1m, 20m, 0m)), LadoControl.Credito, 9);

        Assert.Equal((0m, 100m), (asiento.Lineas[0].Debito, asiento.Lineas[0].Credito));
        Assert.Equal((80m, 0m), (asiento.Lineas[1].Debito, asiento.Lineas[1].Credito));
        Assert.Equal((20m, 0m), (asiento.Lineas[2].Debito, asiento.Lineas[2].Credito));
    }

    [Theory]
    [InlineData(LadoControl.Debito)]
    [InlineData(LadoControl.Credito)]
    public void AsientoGenerado_CumplePartidaDobleConRedondeoPorLinea(LadoControl lado)
    {
        var asiento = AsientoFacturaBuilder.Construir("CXC", Factura((3m, 33.333m, 12m), (1.5m, 10.005m, 0m)), lado, 9);

        Assert.True(PartidaDobleValidator.EsValido(asiento, out var error), error);
    }
}
