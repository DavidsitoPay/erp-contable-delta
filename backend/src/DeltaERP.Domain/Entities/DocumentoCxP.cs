namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxp. El asiento
// se genera al registrarla (ver CxPController).
public class DocumentoCxP : DocumentoFacturaBase<LineaDocumentoCxP>, IDocumentoFactura
{
    public static readonly string[] TiposDocumentoValidos = TiposDocumento.Validos;

    public int ProveedorId { get; set; }

    IReadOnlyList<ILineaFactura> IDocumentoFactura.Lineas => Lineas;

    int IDocumentoFactura.TerceroId => ProveedorId;
}
