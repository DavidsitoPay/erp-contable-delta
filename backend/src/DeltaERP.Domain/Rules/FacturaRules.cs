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
        if (documento.Lineas.Any(l => l.Cantidad <= 0 || l.PrecioUnitario < 0 || l.PorcentajeImpuesto < 0))
        {
            return "Cada línea debe tener cantidad mayor a cero, precio unitario y porcentaje de impuesto no negativos.";
        }
        return null;
    }

    public static void PrepararNuevo(IDocumentoFactura documento, decimal montoTotal)
    {
        documento.Id = 0;
        documento.Estado = "Vigente";
        documento.MontoTotal = montoTotal;
        foreach (var linea in documento.Lineas)
        {
            linea.Id = 0;
        }
    }
}
