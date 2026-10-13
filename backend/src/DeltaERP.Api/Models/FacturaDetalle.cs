using System.Text.Json.Serialization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

// Datos auxiliares para armar el detalle: saldo, catálogos de cuentas e impuestos
// y el documento de origen cuando es una nota de crédito.
public sealed record ReferenciasDetalle(
    decimal SaldoPendiente,
    IReadOnlyDictionary<int, CuentaContable> Cuentas,
    IReadOnlyDictionary<int, Impuesto> Impuestos,
    IDocumentoFactura? Origen,
    Retencion? RetencionIsr = null);

// Tercero lleva la clave que el frontend espera según el módulo (clienteId o
// proveedorId); [JsonExtensionData] la emite junto al resto de propiedades.
public record FacturaDetalle(
    int Id,
    string Numero,
    string TipoDocumento,
    DateOnly Fecha,
    DateOnly FechaVencimiento,
    decimal MontoTotal,
    decimal MontoBase,
    decimal MontoIva,
    decimal? TipoCambioAplicado,
    bool CalculoLegado,
    Guid? DteUuid,
    string? DteSerie,
    string? DteNumero,
    DateTimeOffset? DteFechaCertificacion,
    int? DocumentoOrigenId,
    Guid? DteUuidOrigen,
    string? DteSerieOrigen,
    string? DteNumeroOrigen,
    string Estado,
    int? AsientoId,
    decimal SaldoPendiente,
    List<LineaFacturaDetalle> Lineas)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MontoRetencionIsr { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RevisionRetencion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RevisionMotivo { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetencionIsrRespuesta? RetencionIsr { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Advertencias { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Tercero { get; init; } = new();

    public static FacturaDetalle Desde(IDocumentoFactura documento, ReferenciasDetalle referencias, string claveTercero, int terceroId)
    {
        var detalle = Construir(documento, referencias, claveTercero, terceroId);
        return documento is DocumentoCxP compra ? detalle.ConRetencion(compra, referencias.RetencionIsr) : detalle;
    }

    private FacturaDetalle ConRetencion(DocumentoCxP compra, Retencion? retencion) => this with
    {
        MontoRetencionIsr = retencion?.MontoRetenido ?? 0m,
        RevisionRetencion = compra.RevisionRetencion,
        RevisionMotivo = compra.RevisionMotivo,
        RetencionIsr = retencion is null ? null : RetencionIsrRespuesta.Desde(retencion, compra.MontoTotal),
    };

    private static FacturaDetalle Construir(IDocumentoFactura documento, ReferenciasDetalle referencias, string claveTercero, int terceroId) =>
        new(
            documento.Id, documento.Numero, documento.TipoDocumento, documento.Fecha, documento.FechaVencimiento,
            documento.MontoTotal, documento.MontoBase, documento.MontoIva, documento.TipoCambioAplicado, documento.CalculoLegado,
            documento.DteUuid, documento.DteSerie, documento.DteNumero, documento.DteFechaCertificacion,
            documento.DocumentoOrigenId, referencias.Origen?.DteUuid, referencias.Origen?.DteSerie, referencias.Origen?.DteNumero,
            documento.Estado, documento.AsientoId, referencias.SaldoPendiente,
            LineaFacturaDetalle.Desde(documento.Lineas, referencias.Cuentas, referencias.Impuestos))
        {
            Tercero = { [claveTercero] = terceroId },
        };
}
