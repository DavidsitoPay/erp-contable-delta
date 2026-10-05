namespace DeltaERP.Domain.Entities;

// Vista de solo lectura (sin clave primaria): el saldo se calcula en tiempo
// real, nunca persiste. Totales y saldo acumulan las subcuentas; los valores
// propios excluyen la jerarquía.
public class BalanceSaldoCuenta
{
    public int CuentaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty;
    public decimal TotalDebito { get; set; }
    public decimal TotalCredito { get; set; }
    public decimal Saldo { get; set; }
    public int Nivel { get; set; }
    public int? CuentaPadreId { get; set; }
    public bool EsHoja { get; set; }
    public bool Activa { get; set; }
    public decimal DebitoPropio { get; set; }
    public decimal CreditoPropio { get; set; }
}
