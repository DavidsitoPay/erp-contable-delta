using System.ComponentModel.DataAnnotations.Schema;

namespace DeltaERP.Domain.Entities;

/// <summary>
/// Factura/nota de un proveedor (módulo M5), simétrica a DocumentoCxC. Sin
/// columna "saldo_pendiente": se calcula en vw_saldodocumentocxp
/// (database/05_views.sql). Registrarla genera automáticamente el
/// AsientoContable correspondiente (ver CxPController).
/// </summary>
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

    /// <summary>
    /// Cuenta de control (Pasivo/Acreedora, ej. "Cuentas por pagar") que
    /// recibe el crédito del asiento generado al registrar la factura. Ver el
    /// comentario equivalente en DocumentoCxC.CuentaControlId.
    /// </summary>
    [NotMapped]
    public int CuentaControlId { get; set; }

    [NotMapped]
    public int PeriodoId { get; set; }
}
