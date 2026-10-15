using DeltaERP.Api.Services;

namespace DeltaERP.Tests.Unit;

public class BanguatXmlTests
{
    private const string FormatoInesperado = "Respuesta de Banguat con formato inesperado.";

    private static string Fila(string fecha = "04/10/2026", string venta = "7.64136", string compra = "7.64136") =>
        $"<Var><moneda>2</moneda><fecha>{fecha}</fecha><venta>{venta}</venta><compra>{compra}</compra></Var>";

    private static string Sobre(params string[] filas) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
        "<TipoCambioRangoMonedaResponse xmlns=\"http://www.banguat.gob.gt/variables/ws/\">" +
        "<TipoCambioRangoMonedaResult>" +
        $"<Vars>{string.Concat(filas)}</Vars><TotalItems>{filas.Length}</TotalItems>" +
        "</TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>";

    [Fact]
    public void Parsear_RespuestaUsd_DevuelveLasFilasOrdenadasPorFecha()
    {
        var xml = Sobre(Fila("04/10/2026"), Fila("03/10/2026"));

        var filas = BanguatXml.Parsear(xml);

        Assert.Equal(2, filas.Count);
        Assert.Equal(new DateOnly(2026, 10, 3), filas[0].Fecha);
        Assert.Equal(new DateOnly(2026, 10, 4), filas[1].Fecha);
        Assert.Equal(7.64136m, filas[0].Venta);
        Assert.Equal(7.64136m, filas[0].Compra);
    }

    [Fact]
    public void Parsear_RespuestaEur_ConservaVentaYCompraDistintas()
    {
        var filas = BanguatXml.Parsear(Sobre(Fila("02/10/2026", "1.1275", "1.1274")));

        Assert.Equal(1.1275m, filas[0].Venta);
        Assert.Equal(1.1274m, filas[0].Compra);
    }

    [Fact]
    public void Parsear_SinFilas_DevuelveListaVacia()
    {
        Assert.Empty(BanguatXml.Parsear(Sobre()));
    }

    [Fact]
    public void Parsear_Fault_LanzaConElFaultstring()
    {
        const string fault =
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault>" +
            "<faultcode>soap:Server</faultcode><faultstring>Servidor ocupado</faultstring>" +
            "</soap:Fault></soap:Body></soap:Envelope>";

        var ex = Assert.Throws<BanguatNoDisponibleException>(() => BanguatXml.Parsear(fault));

        Assert.Equal("Servidor ocupado", ex.Message);
    }

    [Fact]
    public void Parsear_XmlInvalido_Lanza()
    {
        var ex = Assert.Throws<BanguatNoDisponibleException>(() => BanguatXml.Parsear("<no es xml"));

        Assert.Equal(FormatoInesperado, ex.Message);
    }

    [Fact]
    public void Parsear_ConDtd_Lanza()
    {
        const string conDtd =
            "<!DOCTYPE foo [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><foo>&x;</foo>";

        Assert.Throws<BanguatNoDisponibleException>(() => BanguatXml.Parsear(conDtd));
    }

    [Theory]
    [InlineData("venta", "0")]
    [InlineData("venta", "-1")]
    [InlineData("venta", "abc")]
    [InlineData("venta", "")]
    [InlineData("compra", "0")]
    public void Parsear_ValorNoPositivoONoNumerico_Lanza(string campo, string valor)
    {
        var xml = campo == "venta" ? Sobre(Fila(venta: valor)) : Sobre(Fila(compra: valor));

        var ex = Assert.Throws<BanguatNoDisponibleException>(() => BanguatXml.Parsear(xml));

        Assert.Equal(FormatoInesperado, ex.Message);
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("2026-10-03")]
    public void Parsear_FechaInvalida_Lanza(string fecha)
    {
        var ex = Assert.Throws<BanguatNoDisponibleException>(() => BanguatXml.Parsear(Sobre(Fila(fecha: fecha))));

        Assert.Equal(FormatoInesperado, ex.Message);
    }

    [Fact]
    public void Parsear_ComaDecimal_SeNormaliza()
    {
        var filas = BanguatXml.Parsear(Sobre(Fila(venta: "7,64136", compra: "7,64136")));

        Assert.Equal(7.64136m, filas[0].Venta);
        Assert.Equal(7.64136m, filas[0].Compra);
    }
}
