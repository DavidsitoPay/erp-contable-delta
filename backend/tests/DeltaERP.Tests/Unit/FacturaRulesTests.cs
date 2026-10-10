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

    private static LineaDocumentoCxC Linea(decimal cantidad = 1m, decimal precio = 10m, int impuestoId = 1, string tipoBienServicio = "SERVICIO") =>
        new() { Cantidad = cantidad, PrecioUnitario = precio, ImpuestoId = impuestoId, TipoBienServicio = tipoBienServicio };

    private static string? Validar(DocumentoCxC documento) =>
        FacturaRules.ValidarCabecera(DocumentoCxC.TiposDocumentoValidos, documento);

    [Fact]
    public void TipoDeDocumentoInvalido_ListaLosTiposPermitidos()
    {
        Assert.Equal("Tipo de documento inválido. Debe ser uno de: Factura, NotaCredito, NotaDebito.", Validar(Documento("Otro", Linea())));
    }

    [Fact]
    public void SinLineas_EsInvalido()
    {
        Assert.Equal("La factura debe tener al menos una línea.", Validar(Documento()));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, -1)]
    public void LineaConValoresFueraDeRango_EsInvalida(int cantidad, int precio)
    {
        var error = Validar(Documento("Factura", Linea(cantidad, precio)));

        Assert.Equal("Cada línea debe tener cantidad mayor a cero y precio unitario no negativo.", error);
    }

    [Fact]
    public void LineaSinImpuesto_EsInvalida()
    {
        Assert.Equal("Cada línea debe indicar un impuesto del catálogo (impuestoId).", Validar(Documento("Factura", Linea(impuestoId: 0))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("OTRO")]
    public void LineaSinTipoBienServicioValido_EsInvalida(string tipoBienServicio)
    {
        var error = Validar(Documento("Factura", Linea(tipoBienServicio: tipoBienServicio)));

        Assert.Equal("Cada línea debe indicar si es un BIEN o un SERVICIO.", error);
    }

    [Fact]
    public void NotaDeCreditoSinOrigen_EsInvalida()
    {
        var error = Validar(Documento("NotaCredito", Linea()));

        Assert.Equal("La nota de crédito debe referenciar la factura de origen (documentoOrigenId).", error);
    }

    [Fact]
    public void FacturaConOrigen_EsInvalida()
    {
        var documento = Documento("Factura", Linea());
        documento.DocumentoOrigenId = 5;

        Assert.Equal("Solo una nota de crédito puede referenciar un documento de origen.", Validar(documento));
    }

    [Fact]
    public void CabeceraCorrecta_NoDevuelveError()
    {
        var notaCredito = Documento("NotaCredito", Linea(), Linea(2m, 0m, 2, "BIEN"));
        notaCredito.DocumentoOrigenId = 5;

        Assert.Null(Validar(notaCredito));
        Assert.Null(Validar(Documento("NotaDebito", Linea())));
    }

    [Fact]
    public void PrepararNuevo_AsignaTotalesEstadoYMontosPorLinea()
    {
        var documento = Documento("Factura", Linea(2m, 100m), Linea(1m, 50.50m));
        documento.Id = 99;
        documento.Estado = "Anulado";
        documento.CalculoLegado = true;
        documento.TipoCambioAplicado = 7.75m;
        documento.Lineas[0].Id = 5;
        var resumen = MotorFiscal.Calcular(new[]
        {
            new EntradaLinea(2m, 100m, new ParametroImpuesto(1, TipoImpuesto.IvaGeneral, 12m, true)),
            new EntradaLinea(1m, 50.50m, new ParametroImpuesto(2, TipoImpuesto.Exento, 0m, false)),
        });

        FacturaRules.PrepararNuevo(documento, resumen);

        Assert.Equal((0, "Vigente", 250.50m, 229.07m, 21.43m, false, 1m),
            (documento.Id, documento.Estado, documento.MontoTotal, documento.MontoBase, documento.MontoIva, documento.CalculoLegado, documento.TipoCambioAplicado));
        Assert.Equal((0, 12m, 200m, 178.57m, 21.43m),
            (documento.Lineas[0].Id, documento.Lineas[0].TasaAplicada, documento.Lineas[0].MontoLinea, documento.Lineas[0].MontoBase, documento.Lineas[0].MontoIva));
        Assert.Equal((0m, 50.50m, 50.50m, 0m),
            (documento.Lineas[1].TasaAplicada, documento.Lineas[1].MontoLinea, documento.Lineas[1].MontoBase, documento.Lineas[1].MontoIva));
    }

    [Fact]
    public void PrepararNuevo_NormalizaLosDatosDelDte()
    {
        var documento = Documento("Factura", Linea());
        documento.DteUuid = Guid.Empty;
        documento.DteSerie = "  ";
        documento.DteNumero = " 123 ";
        documento.DteFechaCertificacion = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.FromHours(-6));
        var resumen = MotorFiscal.Calcular(new[] { new EntradaLinea(1m, 10m, new ParametroImpuesto(2, TipoImpuesto.Exento, 0m, false)) });

        FacturaRules.PrepararNuevo(documento, resumen);

        Assert.Equal((null, null, "123"), (documento.DteUuid, documento.DteSerie, documento.DteNumero));
        Assert.Equal(TimeSpan.Zero, documento.DteFechaCertificacion?.Offset);
    }

    [Fact]
    public void PrepararNuevo_ConservaUnUuidValido()
    {
        var uuid = Guid.NewGuid();
        var documento = Documento("Factura", Linea());
        documento.DteUuid = uuid;
        var resumen = MotorFiscal.Calcular(new[] { new EntradaLinea(1m, 10m, new ParametroImpuesto(2, TipoImpuesto.Exento, 0m, false)) });

        FacturaRules.PrepararNuevo(documento, resumen);

        Assert.Equal(uuid, documento.DteUuid);
    }
}
