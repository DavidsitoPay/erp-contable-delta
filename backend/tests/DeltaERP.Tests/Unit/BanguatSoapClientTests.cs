using System.Net;
using System.Text;
using DeltaERP.Api.Services;
using Microsoft.Extensions.Options;

namespace DeltaERP.Tests.Unit;

public class BanguatSoapClientTests
{
    private const string AccionEsperada = "\"http://www.banguat.gob.gt/variables/ws/TipoCambioRangoMoneda\"";

    private const string RespuestaVacia =
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
        "<TipoCambioRangoMonedaResponse xmlns=\"http://www.banguat.gob.gt/variables/ws/\"><TipoCambioRangoMonedaResult>" +
        "<Vars /><TotalItems>0</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>";

    private const string RespuestaConUnaFila =
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
        "<TipoCambioRangoMonedaResponse xmlns=\"http://www.banguat.gob.gt/variables/ws/\"><TipoCambioRangoMonedaResult>" +
        "<Vars><Var><moneda>2</moneda><fecha>02/10/2026</fecha><venta>7.64136</venta><compra>7.64136</compra></Var></Vars>" +
        "<TotalItems>1</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>";

    private const string Fault =
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault>" +
        "<faultstring>Servidor ocupado</faultstring></soap:Fault></soap:Body></soap:Envelope>";

    private static readonly DateOnly Desde = new(2026, 10, 2);
    private static readonly DateOnly Hasta = new(2026, 10, 12);

    [Fact]
    public async Task Solicitud_EnviaSoapConFechasMonedaYSoapAction()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.OK, RespuestaVacia)));

        await Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta);

        Assert.Equal(HttpMethod.Post, manejador.UltimoMetodo);
        Assert.Equal("text/xml", manejador.UltimoContentType);
        Assert.Equal(AccionEsperada, manejador.UltimoSoapAction);
        Assert.Contains("<fechainit>02/10/2026</fechainit>", manejador.UltimoCuerpo);
        Assert.Contains("<fechafin>12/10/2026</fechafin>", manejador.UltimoCuerpo);
        Assert.Contains("<moneda>2</moneda>", manejador.UltimoCuerpo);
    }

    [Fact]
    public async Task RespuestaValida_DevuelveLasCotizaciones()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.OK, RespuestaConUnaFila)));

        var filas = await Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta);

        Assert.Single(filas);
    }

    [Fact]
    public async Task Fault_ConHttp500_NoSeReintenta()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.InternalServerError, Fault)));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() => Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(1, manejador.Llamadas);
    }

    [Fact]
    public async Task Error500SinFault_ReintentaUnaVezYLanza()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.InternalServerError, "boom")));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() => Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(2, manejador.Llamadas);
    }

    [Fact]
    public async Task Error500YLuego200_Recupera()
    {
        var manejador = new ManejadorFalso(intento => Task.FromResult(intento == 1
            ? Respuesta(HttpStatusCode.InternalServerError, "boom")
            : Respuesta(HttpStatusCode.OK, RespuestaConUnaFila)));

        var filas = await Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta);

        Assert.Equal(2, manejador.Llamadas);
        Assert.Single(filas);
    }

    [Fact]
    public async Task TimeoutDelHttpClient_ReintentaYLanza()
    {
        var manejador = new ManejadorFalso(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() => Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(2, manejador.Llamadas);
    }

    [Fact]
    public async Task CancelacionDelLlamador_NoSeReintenta()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.OK, RespuestaVacia)));
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta, cancelacion.Token));

        Assert.True(manejador.Llamadas <= 1);
    }

    [Fact]
    public async Task XmlInvalido_NoSeReintenta()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.OK, "<no es xml")));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() => Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(1, manejador.Llamadas);
    }

    [Fact]
    public async Task Respuesta404_NoSeReintenta()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.NotFound, "no existe")));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() => Cliente(manejador).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(1, manejador.Llamadas);
    }

    [Fact]
    public async Task SinReintentosConfigurados_UnSoloIntento()
    {
        var manejador = new ManejadorFalso(_ => Task.FromResult(Respuesta(HttpStatusCode.InternalServerError, "boom")));

        await Assert.ThrowsAsync<BanguatNoDisponibleException>(() =>
            Cliente(manejador, reintentos: 0).ObtenerRangoAsync(2, Desde, Hasta));

        Assert.Equal(1, manejador.Llamadas);
    }

    private static BanguatSoapClient Cliente(ManejadorFalso manejador, int reintentos = 1) =>
        new(
            new HttpClient(manejador) { BaseAddress = new Uri("http://prueba/TipoCambio.asmx") },
            Options.Create(new BanguatOptions { Reintentos = reintentos, EsperaReintentoMs = 0 }));

    private static HttpResponseMessage Respuesta(HttpStatusCode estado, string cuerpo) =>
        new(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "text/xml") };

    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly Func<int, Task<HttpResponseMessage>> _respuesta;

        public ManejadorFalso(Func<int, Task<HttpResponseMessage>> respuesta)
        {
            _respuesta = respuesta;
        }

        public int Llamadas { get; private set; }
        public string? UltimoCuerpo { get; private set; }
        public string? UltimoSoapAction { get; private set; }
        public HttpMethod? UltimoMetodo { get; private set; }
        public string? UltimoContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            UltimoMetodo = request.Method;
            UltimoSoapAction = request.Headers.TryGetValues("SOAPAction", out var valores) ? valores.Single() : null;
            UltimoContentType = request.Content?.Headers.ContentType?.MediaType;
            UltimoCuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return await _respuesta(Llamadas);
        }
    }
}
