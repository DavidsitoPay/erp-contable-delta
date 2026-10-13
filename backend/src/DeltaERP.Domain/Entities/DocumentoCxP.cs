using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxp. El asiento
// se genera al registrarla (ver CxPController).
public class DocumentoCxP : DocumentoFacturaBase<LineaDocumentoCxP>, IDocumentoFactura
{
    public static readonly string[] TiposDocumentoValidos = TiposDocumento.Validos;

    public const string RevisionNinguna = "NINGUNA";
    public const string RevisionPendiente = "PENDIENTE";
    public const string RevisionResuelta = "RESUELTA";

    public int ProveedorId { get; set; }

    // El servidor fija la revisión; un cliente no puede enviarla.
    [JsonIgnore]
    public string RevisionRetencion { get; set; } = RevisionNinguna;

    [JsonIgnore]
    public string? RevisionMotivo { get; set; }

    [NotMapped]
    public AjusteIsr? RetencionIsrAjuste { get; set; }

    IReadOnlyList<ILineaFactura> IDocumentoFactura.Lineas => Lineas;

    int IDocumentoFactura.TerceroId => ProveedorId;
}
