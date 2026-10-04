using System.Text.Json.Serialization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

// Tercero lleva la clave que el frontend espera según el módulo (clienteId o
// proveedorId); [JsonExtensionData] la emite junto al resto de propiedades.
public record FacturaDetalle(
    int Id,
    string Numero,
    string TipoDocumento,
    DateOnly Fecha,
    DateOnly FechaVencimiento,
    decimal MontoTotal,
    decimal? TipoCambioAplicado,
    string Estado,
    int? AsientoId,
    decimal SaldoPendiente,
    List<LineaFacturaDetalle> Lineas)
{
    [JsonExtensionData]
    public Dictionary<string, object?> Tercero { get; init; } = new();

    public static FacturaDetalle Desde(
        IDocumentoFactura documento,
        decimal saldoPendiente,
        IReadOnlyDictionary<int, CuentaContable> cuentas,
        string claveTercero,
        int terceroId) =>
        new(
            documento.Id, documento.Numero, documento.TipoDocumento, documento.Fecha, documento.FechaVencimiento,
            documento.MontoTotal, documento.TipoCambioAplicado, documento.Estado, documento.AsientoId, saldoPendiente,
            LineaFacturaDetalle.Desde(documento.Lineas, cuentas))
        {
            Tercero = { [claveTercero] = terceroId },
        };
}
