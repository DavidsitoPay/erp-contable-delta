namespace DeltaERP.Domain.Entities;

public class MovimientoTesoreria
{
    public const string TipoIngreso = "Ingreso";
    public const string TipoEgreso = "Egreso";
    public const string OrigenManual = "Manual";
    public const string OrigenTransferencia = "Transferencia";
    public const string OrigenCxC = "CxC";
    public const string OrigenCxP = "CxP";
    public const string OrigenApertura = "Apertura";

    public int Id { get; set; }
    public int CuentaBancariaId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int AsientoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string Origen { get; set; } = string.Empty;
    public Guid? TransferenciaId { get; set; }
    public int? ReciboPagoId { get; set; }
    public int? PagoProveedorId { get; set; }
}
