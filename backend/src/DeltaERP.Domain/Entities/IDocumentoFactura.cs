namespace DeltaERP.Domain.Entities;

// Contrato común de DocumentoCxC y DocumentoCxP para las reglas de facturación.
// Lineas se implementa de forma explícita para no añadir una propiedad pública
// que EF Core o System.Text.Json tengan que mapear.
public interface IDocumentoFactura
{
    int Id { get; set; }
    string Numero { get; }
    string TipoDocumento { get; }
    DateOnly Fecha { get; }
    DateOnly FechaVencimiento { get; }
    decimal MontoTotal { get; set; }
    decimal? TipoCambioAplicado { get; }
    string Estado { get; set; }
    int? AsientoId { get; set; }
    int CuentaControlId { get; }
    int PeriodoId { get; }
    IReadOnlyList<ILineaFactura> Lineas { get; }
}
