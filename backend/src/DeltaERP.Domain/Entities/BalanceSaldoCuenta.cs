namespace DeltaERP.Domain.Entities;

/// <summary>
/// Fila de database/05_views.sql::vw_balance_saldos (módulo M3). Vista de solo
/// lectura (sin clave primaria propia — HasNoKey() en DeltaErpDbContext): el
/// saldo se calcula en tiempo real a partir de LineaAsiento/AsientoContable,
/// nunca se persiste.
/// </summary>
public class BalanceSaldoCuenta
{
    public int CuentaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty;
    public decimal TotalDebito { get; set; }
    public decimal TotalCredito { get; set; }
    public decimal Saldo { get; set; }
}
