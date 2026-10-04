namespace DeltaERP.Domain.Entities;

/// <summary>
/// Mapea vw_saldodocumentocxp (database/05_views.sql). Ver SaldoDocumentoCxC.
/// </summary>
public class SaldoDocumentoCxP
{
    public int DocumentoId { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
}
