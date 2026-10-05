namespace DeltaERP.Domain.Entities;

public class ReciboPagoCliente
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal MontoTotal { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? ReferenciaBancaria { get; set; }
    public int? CuentaBancariaId { get; set; }

    public List<AplicacionPagoCliente> Aplicaciones { get; set; } = new();
}
