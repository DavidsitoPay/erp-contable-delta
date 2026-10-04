namespace DeltaERP.Domain.Entities;

// Vista de solo lectura (sin clave primaria): el saldo se calcula en tiempo
// real, nunca persiste.
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
