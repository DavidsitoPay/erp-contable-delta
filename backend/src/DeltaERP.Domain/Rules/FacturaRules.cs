using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class FacturaRules
{
    public static string? ValidarCabecera(string[] tiposDocumentoValidos, IDocumentoFactura documento)
    {
        if (!tiposDocumentoValidos.Contains(documento.TipoDocumento))
        {
            return $"Tipo de documento inválido. Debe ser uno de: {string.Join(", ", tiposDocumentoValidos)}.";
        }
        if (documento.Lineas.Count == 0)
        {
            return "La factura debe tener al menos una línea.";
        }
        return ValidarLineas(documento.Lineas) ?? ValidarOrigen(documento);
    }

    public static void PrepararNuevo(IDocumentoFactura documento, ResumenFiscal resumen)
    {
        documento.Id = 0;
        documento.Estado = "Vigente";
        documento.MontoTotal = resumen.Total;
        documento.MontoBase = resumen.Base;
        documento.MontoIva = resumen.Iva;
        documento.TipoCambioAplicado = 1m;
        documento.CalculoLegado = false;
        documento.DteUuid = documento.DteUuid == Guid.Empty ? null : documento.DteUuid;
        documento.DteSerie = LimpiarTexto(documento.DteSerie);
        documento.DteNumero = LimpiarTexto(documento.DteNumero);
        documento.DteFechaCertificacion = documento.DteFechaCertificacion?.ToUniversalTime();
        for (var i = 0; i < documento.Lineas.Count; i++)
        {
            var linea = documento.Lineas[i];
            var calculo = resumen.Lineas[i];
            linea.Id = 0;
            linea.TasaAplicada = calculo.TasaAplicada;
            linea.MontoLinea = calculo.MontoLinea;
            linea.MontoBase = calculo.Base;
            linea.MontoIva = calculo.Iva;
        }
    }

    private static string? LimpiarTexto(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? ValidarLineas(IReadOnlyList<ILineaFactura> lineas)
    {
        if (lineas.Any(l => l.Cantidad <= 0 || l.PrecioUnitario < 0))
        {
            return "Cada línea debe tener cantidad mayor a cero y precio unitario no negativo.";
        }
        if (lineas.Any(l => l.ImpuestoId <= 0))
        {
            return "Cada línea debe indicar un impuesto del catálogo (impuestoId).";
        }
        return lineas.Any(l => !TipoBienServicio.Validos.Contains(l.TipoBienServicio))
            ? "Cada línea debe indicar si es un BIEN o un SERVICIO."
            : null;
    }

    private static string? ValidarOrigen(IDocumentoFactura documento)
    {
        var esNotaCredito = documento.TipoDocumento == TiposDocumento.NotaCredito;
        if (esNotaCredito && documento.DocumentoOrigenId is null or <= 0)
        {
            return "La nota de crédito debe referenciar la factura de origen (documentoOrigenId).";
        }
        return !esNotaCredito && documento.DocumentoOrigenId is not null
            ? "Solo una nota de crédito puede referenciar un documento de origen."
            : null;
    }
}
