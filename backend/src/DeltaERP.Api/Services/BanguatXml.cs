using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

public static class BanguatXml
{
    private const string MensajeFormato = "Respuesta de Banguat con formato inesperado.";
    private const string FormatoFecha = "dd/MM/yyyy";

    public static IReadOnlyList<CotizacionBanguat> Parsear(string xml)
    {
        var documento = Cargar(xml);
        var fault = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
        {
            var mensaje = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
            throw new BanguatNoDisponibleException(string.IsNullOrWhiteSpace(mensaje) ? "Banguat respondió con un error." : mensaje.Trim());
        }
        return documento.Descendants()
            .Where(e => e.Name.LocalName == "Var")
            .Select(ConvertirFila)
            .OrderBy(f => f.Fecha)
            .ToList();
    }

    private static XDocument Cargar(string xml)
    {
        var configuracion = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var texto = new StringReader(xml);
            using var lector = XmlReader.Create(texto, configuracion);
            return XDocument.Load(lector);
        }
        catch (XmlException ex)
        {
            throw new BanguatNoDisponibleException(MensajeFormato, ex);
        }
    }

    private static CotizacionBanguat ConvertirFila(XElement fila)
    {
        var fecha = Texto(fila, "fecha");
        if (!DateOnly.TryParseExact(fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
        {
            throw new BanguatNoDisponibleException(MensajeFormato);
        }
        return new CotizacionBanguat(dia, Decimal(fila, "venta"), Decimal(fila, "compra"));
    }

    private static string? Texto(XElement fila, string nombre) =>
        fila.Elements().FirstOrDefault(e => e.Name.LocalName == nombre)?.Value.Trim();

    private static decimal Decimal(XElement fila, string nombre)
    {
        var texto = Texto(fila, nombre);
        if (texto is not null && texto.Contains(',') && !texto.Contains('.'))
        {
            texto = texto.Replace(',', '.');
        }
        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) || valor <= 0)
        {
            throw new BanguatNoDisponibleException(MensajeFormato);
        }
        return valor;
    }
}
