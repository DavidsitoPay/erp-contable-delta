namespace DeltaERP.Domain.Entities;

public class AplicacionPagoProveedor
{
    public int Id { get; set; }
    public int PagoCabeceraId { get; set; }
    public int DocumentoId { get; set; }
    public decimal MontoAplicado { get; set; }
}
