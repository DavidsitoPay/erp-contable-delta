namespace DeltaERP.Domain.Entities;

/// <summary>
/// Recibo de pago de un cliente, aplicado a una o varias facturas (RN-05). No
/// genera AsientoContable: la tabla no tiene columna asiento_id en el modelo
/// de datos entregado (a diferencia de DocumentoCxC) — el impacto en bancos
/// se modela en M6 Tesorería (MovimientoTesoreria), fuera de este alcance.
/// </summary>
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
