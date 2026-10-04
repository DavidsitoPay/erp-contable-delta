namespace DeltaERP.Domain.Entities;

public class AplicacionPagoCliente
{
    public int Id { get; set; }
    public int ReciboPagoId { get; set; }
    public int DocumentoId { get; set; }
    public decimal MontoAplicado { get; set; }
}
