namespace DeltaERP.Domain.Entities;

// Vista de solo lectura; Id se mapea a conciliacion_id.
public class ConciliacionResumen
{
    public int Id { get; set; }
    public int CuentaBancariaId { get; set; }
    public int PeriodoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal SaldoExtracto { get; set; }
    public decimal SaldoInicial { get; set; }
    public decimal TotalMarcado { get; set; }
    public decimal SaldoConciliado { get; set; }
    public decimal Diferencia { get; set; }
    public int CantidadMovimientos { get; set; }
}
