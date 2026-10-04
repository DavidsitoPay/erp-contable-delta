using System.ComponentModel.DataAnnotations.Schema;

namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxp. El asiento
// se genera al registrarla (ver CxPController).
public class DocumentoCxP
{
    public static readonly string[] TiposDocumentoValidos = { "Factura", "NotaCredito", "NotaDebito" };

    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public int ProveedorId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal? TipoCambioAplicado { get; set; }
    public string Estado { get; set; } = "Vigente"; // Vigente | Anulado
    public int? AsientoId { get; set; }

    public List<LineaDocumentoCxP> Lineas { get; set; } = new();

    // Solo se usa en el POST de creación; ver CuentaControlId en DocumentoCxC.
    [NotMapped]
    public int CuentaControlId { get; set; }

    [NotMapped]
    public int PeriodoId { get; set; }
}
