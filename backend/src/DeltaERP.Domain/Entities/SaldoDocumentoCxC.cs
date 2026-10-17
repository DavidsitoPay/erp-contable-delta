namespace DeltaERP.Domain.Entities;

// Vista de solo lectura, sin clave primaria propia.
public class SaldoDocumentoCxC
{
    public int DocumentoId { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public int MonedaId { get; set; }
    public decimal MontoTotalOriginal { get; set; }
    public decimal SaldoPendienteGtq { get; set; }
}
