namespace DeltaERP.Domain.Entities;

public class AjusteRetencion
{
    public const string EstadoPendiente = "PENDIENTE";
    public const string EstadoResuelto = "RESUELTO";

    public int Id { get; set; }
    public int RetencionId { get; set; }
    public int NotaCreditoId { get; set; }
    public decimal MontoNc { get; set; }
    public string Estado { get; set; } = EstadoPendiente;
    public int CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public string? MotivoResolucion { get; set; }
    public string? ReferenciaResolucion { get; set; }
    public int? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
}
