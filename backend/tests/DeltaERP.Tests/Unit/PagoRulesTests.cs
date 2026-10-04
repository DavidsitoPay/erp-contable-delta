using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class PagoRulesTests
{
    private static readonly decimal[] MontosPrueba = { 10m, 0m };
    private static readonly decimal[] MontoValido = { 10m };

    private static DocumentoPagable Documento(int id, string numero = "F-1", int terceroId = 1, string estado = "Vigente", decimal saldo = 100m) =>
        new(id, numero, terceroId, estado, saldo);

    [Fact]
    public void SolicitudSinAplicaciones_EsInvalida()
    {
        Assert.Equal("El pago debe aplicarse al menos a una factura.", PagoRules.ValidarSolicitud(Array.Empty<decimal>(), "Efectivo"));
    }

    [Fact]
    public void SolicitudConMontoNoPositivo_EsInvalida()
    {
        Assert.Equal("El monto aplicado a cada factura debe ser mayor a cero.", PagoRules.ValidarSolicitud(MontosPrueba, "Efectivo"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void SolicitudSinMetodoDePago_EsInvalida(string? metodo)
    {
        Assert.Equal("El método de pago es obligatorio.", PagoRules.ValidarSolicitud(MontoValido, metodo));
    }

    [Fact]
    public void SolicitudCorrecta_NoDevuelveError()
    {
        Assert.Null(PagoRules.ValidarSolicitud(MontoValido, "Efectivo"));
    }

    [Fact]
    public void DocumentoInexistente_SeReportaPrimero()
    {
        var error = PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 5m), (2, 5m) }, new[] { Documento(1, terceroId: 9) });

        Assert.Equal("Las siguientes facturas no existen: 2.", error);
    }

    [Fact]
    public void DocumentoDeOtroTercero_UsaElNombreDelTerceroEnElMensaje()
    {
        var error = PagoRules.ValidarAplicaciones("proveedor", 1, new[] { (1, 5m) }, new[] { Documento(1, "F-7", terceroId: 2) });

        Assert.Equal("Las siguientes facturas no pertenecen al proveedor indicado: F-7.", error);
    }

    [Fact]
    public void DocumentoNoVigente_EsInvalido()
    {
        var error = PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 5m) }, new[] { Documento(1, "F-8", estado: "Anulado") });

        Assert.Equal("Las siguientes facturas no están vigentes: F-8.", error);
    }

    [Fact]
    public void AplicacionesRepetidasSeSumanAntesDeComparar_RN05()
    {
        var error = PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 60m), (1, 40.01m) }, new[] { Documento(1, "F-9", saldo: 100m) });

        Assert.Equal("RN-05: el monto aplicado excede el saldo pendiente de: F-9 (saldo: 100).", error);
    }

    [Fact]
    public void AplicacionDentroDelSaldo_NoDevuelveError()
    {
        Assert.Null(PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 60m), (1, 40m) }, new[] { Documento(1, saldo: 100m) }));
    }
}
