using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public class AplicacionPagoCliente
{
    public int Id { get; set; }
    public int ReciboPagoId { get; set; }
    public int DocumentoId { get; set; }
    public decimal MontoAplicado { get; set; }

    [NotMapped]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetencionIvaEntrada? RetencionIva { get; set; }
}
