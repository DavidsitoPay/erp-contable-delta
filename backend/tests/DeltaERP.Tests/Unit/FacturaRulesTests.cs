using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class FacturaRulesTests
{
    private static DocumentoCxC Documento(string tipo = "Factura", params LineaDocumentoCxC[] lineas) => new()
    {
        TipoDocumento = tipo,
        Lineas = lineas.ToList(),
    };

    private static LineaDocumentoCxC Linea(decimal cantidad = 1m, decimal precio = 10m, decimal impuesto = 0m) =>
        new() { Cantidad = cantidad, PrecioUnitario = precio, PorcentajeImpuesto = impuesto };

    [Fact]
    public void TipoDeDocumentoInvalido_ListaLosTiposPermitidos()
    {
        var error = FacturaRules.ValidarCabecera(DocumentoCxC.TiposDocumentoValidos, Documento("Otro", Linea()));

        Assert.Equal("Tipo de documento inválido. Debe ser uno de: Factura, NotaCredito, NotaDebito.", error);
    }

    [Fact]
    public void SinLineas_EsInvalido()
    {
        var error = FacturaRules.ValidarCabecera(DocumentoCxC.TiposDocumentoValidos, Documento());

        Assert.Equal("La factura debe tener al menos una línea.", error);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 10, -1)]
    public void LineaConValoresFueraDeRango_EsInvalida(int cantidad, int precio, int impuesto)
    {
        var error = FacturaRules.ValidarCabecera(DocumentoCxC.TiposDocumentoValidos, Documento("Factura", Linea(cantidad, precio, impuesto)));

        Assert.StartsWith("Cada línea debe tener cantidad mayor a cero", error);
    }

    [Fact]
    public void CabeceraCorrecta_NoDevuelveError()
    {
        Assert.Null(FacturaRules.ValidarCabecera(DocumentoCxC.TiposDocumentoValidos, Documento("NotaCredito", Linea(), Linea(2m, 0m, 12m))));
    }

    [Fact]
    public void PrepararNuevo_ReiniciaIdsYAsignaEstadoVigenteYMonto()
    {
        var documento = Documento("Factura", Linea());
        documento.Id = 99;
        documento.Estado = "Anulado";
        documento.Lineas[0].Id = 5;

        FacturaRules.PrepararNuevo(documento, 123.45m);

        Assert.Equal((0, "Vigente", 123.45m, 0), (documento.Id, documento.Estado, documento.MontoTotal, documento.Lineas[0].Id));
    }
}
