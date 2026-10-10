namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxc. AsientoId
// queda poblado al registrar la factura (ver CxCController), nunca se asigna a mano.
public class DocumentoCxC : DocumentoFacturaBase<LineaDocumentoCxC>, IDocumentoFactura
{
    public static readonly string[] TiposDocumentoValidos = TiposDocumento.Validos;

    public int ClienteId { get; set; }

    IReadOnlyList<ILineaFactura> IDocumentoFactura.Lineas => Lineas;

    int IDocumentoFactura.TerceroId => ClienteId;
}
