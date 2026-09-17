namespace DeltaERP.Domain.Entities;

public class LineaAsiento
{
    public int Id { get; set; }
    public int AsientoId { get; set; }
    public int CuentaId { get; set; }
    public int? CentroCostoId { get; set; }
    public decimal Debito { get; set; }
    public decimal Credito { get; set; }
}
