namespace DeltaERP.Domain.Entities;

public class ConciliacionBancaria
{
    public const string EstadoPendiente = "Pendiente";
    public const string EstadoConciliado = "Conciliado";
    public const string EstadoCancelada = "Cancelada";

    public int Id { get; set; }
    public int CuentaBancariaId { get; set; }
    public int PeriodoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Estado { get; set; } = EstadoPendiente;
    public decimal SaldoExtracto { get; set; }
}
