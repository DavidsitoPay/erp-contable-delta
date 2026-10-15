namespace DeltaERP.Domain.Entities;

public class HistorialTipoCambio
{
    public const string FuenteBanguat = "Banguat";
    public const string FuenteManual = "Manual";

    public int Id { get; set; }
    public int MonedaId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Tasa { get; set; }
    public string Fuente { get; set; } = FuenteBanguat;
    public DateOnly FechaReferencia { get; set; }
    public bool Arrastrado { get; set; }
    public decimal? CotizacionVenta { get; set; }
    public decimal? CotizacionCompra { get; set; }
    public decimal? ReferenciaUsd { get; set; }
    public string? Motivo { get; set; }
    public int? UsuarioId { get; set; }
    public bool Activa { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; }
}
