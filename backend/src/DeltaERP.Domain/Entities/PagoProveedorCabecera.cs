namespace DeltaERP.Domain.Entities;

// Simétrico a ReciboPagoCliente: tampoco genera AsientoContable.
public class PagoProveedorCabecera
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal MontoTotal { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? ReferenciaBancaria { get; set; }
    public int? CuentaBancariaId { get; set; }

    public List<AplicacionPagoProveedor> Aplicaciones { get; set; } = new();
}
