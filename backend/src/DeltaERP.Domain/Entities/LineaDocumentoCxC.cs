namespace DeltaERP.Domain.Entities;

public class LineaDocumentoCxC : ILineaFactura
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public string? Descripcion { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeImpuesto { get; set; }
    public int? CentroCostoId { get; set; }
    public int CuentaContableId { get; set; }
}
