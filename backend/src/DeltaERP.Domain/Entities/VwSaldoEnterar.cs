namespace DeltaERP.Domain.Entities;

// Vista de solo lectura (vw_saldo_enterar), sin clave primaria propia.
public class VwSaldoEnterar
{
    public DateOnly Periodo { get; set; }
    public decimal Retenido { get; set; }
    public decimal Enterado { get; set; }
    public decimal Saldo { get; set; }
}
