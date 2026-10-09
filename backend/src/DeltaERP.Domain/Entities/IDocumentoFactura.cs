namespace DeltaERP.Domain.Entities;

// Contrato común de DocumentoCxC y DocumentoCxP para las reglas de facturación.
// Lineas y TerceroId se implementan de forma explícita para no añadir propiedades
// públicas que EF Core o System.Text.Json tengan que mapear.
public interface IDocumentoFactura
{
    int Id { get; set; }
    string Numero { get; }
    string TipoDocumento { get; }
    DateOnly Fecha { get; }
    DateOnly FechaVencimiento { get; }
    decimal MontoTotal { get; set; }
    decimal MontoBase { get; set; }
    decimal MontoIva { get; set; }
    decimal? TipoCambioAplicado { get; set; }
    bool CalculoLegado { get; set; }
    Guid? DteUuid { get; set; }
    string? DteUuidTexto { get; set; }
    string? DteSerie { get; set; }
    string? DteNumero { get; set; }
    DateTimeOffset? DteFechaCertificacion { get; set; }
    int? DocumentoOrigenId { get; }
    string Estado { get; set; }
    int? AsientoId { get; set; }
    int CuentaControlId { get; }
    int PeriodoId { get; }
    int TerceroId { get; }
    IReadOnlyList<ILineaFactura> Lineas { get; }
}
