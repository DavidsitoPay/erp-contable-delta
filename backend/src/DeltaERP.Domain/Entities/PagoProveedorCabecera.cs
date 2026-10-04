namespace DeltaERP.Domain.Entities;

/// <summary>
/// Pago a un proveedor, aplicado a una o varias facturas (RN-05). Simétrico a
/// ReciboPagoCliente — tampoco genera AsientoContable (ver el comentario ahí).
/// </summary>
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
