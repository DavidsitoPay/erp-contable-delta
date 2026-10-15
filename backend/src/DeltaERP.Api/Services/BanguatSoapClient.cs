using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using DeltaERP.Domain.Rules;
using Microsoft.Extensions.Options;

namespace DeltaERP.Api.Services;

public sealed class BanguatSoapClient : IBanguatClient
{
    private const string AccionSoap = "\"http://www.banguat.gob.gt/variables/ws/TipoCambioRangoMoneda\"";
    private const string FormatoFecha = "dd/MM/yyyy";
    private const string MensajeSinRespuesta = "Banguat no respondió.";

    private readonly HttpClient _http;
    private readonly BanguatOptions _opciones;

    public BanguatSoapClient(HttpClient http, IOptions<BanguatOptions> opciones)
    {
        _http = http;
        _opciones = opciones.Value;
    }

    public async Task<IReadOnlyList<CotizacionBanguat>> ObtenerRangoAsync(
        int codigoMoneda, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var sobre = ConstruirSobre(codigoMoneda, desde, hasta);
        for (var intento = 0; ; intento++)
        {
            try
            {
                return BanguatXml.Parsear(await EnviarAsync(sobre, ct));
            }
            catch (Exception ex) when (EsTransitorio(ex, ct))
            {
                if (intento >= _opciones.Reintentos)
                {
                    throw new BanguatNoDisponibleException(MensajeSinRespuesta, ex);
                }
                await Task.Delay(_opciones.EsperaReintentoMs, ct);
            }
        }
    }

    private async Task<string> EnviarAsync(string sobre, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, _http.BaseAddress ?? new Uri(_opciones.Endpoint))
        {
            Content = new StringContent(sobre, Encoding.UTF8, new MediaTypeHeaderValue("text/xml")),
        };
        solicitud.Headers.TryAddWithoutValidation("SOAPAction", AccionSoap);
        using var respuesta = await _http.SendAsync(solicitud, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        var estado = (int)respuesta.StatusCode;
        if (estado >= 500 && !cuerpo.Contains("Fault", StringComparison.Ordinal))
        {
            throw new HttpRequestException($"Banguat respondió {estado}.");
        }
        if (estado is >= 400 and < 500)
        {
            throw new BanguatNoDisponibleException($"Banguat respondió {estado}.");
        }
        return cuerpo;
    }

    private static bool EsTransitorio(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static string ConstruirSobre(int codigoMoneda, DateOnly desde, DateOnly hasta) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
        "<TipoCambioRangoMoneda xmlns=\"http://www.banguat.gob.gt/variables/ws/\">" +
        $"<fechainit>{desde.ToString(FormatoFecha, CultureInfo.InvariantCulture)}</fechainit>" +
        $"<fechafin>{hasta.ToString(FormatoFecha, CultureInfo.InvariantCulture)}</fechafin>" +
        $"<moneda>{codigoMoneda.ToString(CultureInfo.InvariantCulture)}</moneda>" +
        "</TipoCambioRangoMoneda></soap:Body></soap:Envelope>";
}
