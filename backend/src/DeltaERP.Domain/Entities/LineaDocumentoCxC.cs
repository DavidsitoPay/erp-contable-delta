namespace DeltaERP.Domain.Entities;

public class LineaDocumentoCxC : ILineaFactura
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public string? Descripcion { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int ImpuestoId { get; set; }
    public string TipoBienServicio { get; set; } = string.Empty;
    public decimal TasaAplicada { get; set; }
    public decimal MontoLinea { get; set; }
    public decimal MontoBase { get; set; }
    public decimal MontoIva { get; set; }
    public int? CentroCostoId { get; set; }
    public int CuentaContableId { get; set; }
}
