namespace DeltaERP.Domain.Entities;

public interface ILineaFactura
{
    int Id { get; set; }
    string? Descripcion { get; }
    decimal Cantidad { get; }
    decimal PrecioUnitario { get; }
    decimal PorcentajeImpuesto { get; }
    int? CentroCostoId { get; }
    int CuentaContableId { get; }
}
