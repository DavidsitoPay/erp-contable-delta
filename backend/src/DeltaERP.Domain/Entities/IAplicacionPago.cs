namespace DeltaERP.Domain.Entities;

public interface IAplicacionPago
{
    int DocumentoId { get; }
    decimal? ValorEfectivoGtq { get; }
    decimal MontoAplicado { get; }
    decimal MontoAplicadoGtq { get; set; }
    decimal ValorGtq { get; set; }
    decimal DiferencialGtq { get; set; }
    int? TipoCambioPagoId { get; set; }
}
