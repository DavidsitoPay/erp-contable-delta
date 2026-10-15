using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public class AplicacionPagoCliente : AplicacionPagoBase
{
    public int ReciboPagoId { get; set; }

    [NotMapped]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetencionIvaEntrada? RetencionIva { get; set; }
}
