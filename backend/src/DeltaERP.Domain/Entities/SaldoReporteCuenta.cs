namespace DeltaERP.Domain.Entities;

// Fila de fn_reporte_saldos (sin clave): saldo firmado por naturaleza y acumulado con las subcuentas.
public class SaldoReporteCuenta
{
    public int CuentaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty;
    public int? CuentaPadreId { get; set; }
    public int Nivel { get; set; }
    public bool EsHoja { get; set; }
    public decimal TotalDebito { get; set; }
    public decimal TotalCredito { get; set; }
    public decimal Saldo { get; set; }
}
