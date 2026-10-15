using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public abstract class AplicacionPagoBase : IAplicacionPago
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public decimal MontoAplicado { get; set; }
    public decimal MontoAplicadoGtq { get; set; }
    public decimal ValorGtq { get; set; }
    public decimal DiferencialGtq { get; set; }
    public int? TipoCambioPagoId { get; set; }

    [NotMapped]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ValorEfectivoGtq { get; set; }
}
