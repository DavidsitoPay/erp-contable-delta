namespace DeltaERP.Domain.Entities;

/// <summary>
/// Mapea vw_saldodocumentocxc (database/05_views.sql) — vista de solo
/// lectura, sin clave primaria propia. Ver BalanceSaldoCuenta (M3) para el
/// mismo patrón.
/// </summary>
public class SaldoDocumentoCxC
{
    public int DocumentoId { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
}
