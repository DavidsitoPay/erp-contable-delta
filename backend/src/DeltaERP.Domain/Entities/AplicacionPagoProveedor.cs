using System.ComponentModel.DataAnnotations.Schema;

namespace DeltaERP.Domain.Entities;

public class AplicacionPagoProveedor : AplicacionPagoBase
{
    public int PagoCabeceraId { get; set; }

    [NotMapped]
    public string? TipoRentaCodigo { get; set; }
}
