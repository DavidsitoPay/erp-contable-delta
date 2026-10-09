namespace DeltaERP.Domain.Entities;

public interface ILineaFactura
{
    int Id { get; set; }
    string? Descripcion { get; }
    decimal Cantidad { get; }
    decimal PrecioUnitario { get; }
    int ImpuestoId { get; }
    string TipoBienServicio { get; }
    decimal TasaAplicada { get; set; }
    decimal MontoLinea { get; set; }
    decimal MontoBase { get; set; }
    decimal MontoIva { get; set; }
    int? CentroCostoId { get; }
    int CuentaContableId { get; }
}
