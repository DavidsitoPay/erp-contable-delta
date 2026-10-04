using System.ComponentModel.DataAnnotations.Schema;

namespace DeltaERP.Domain.Entities;

/// <summary>
/// Factura/nota a un cliente (módulo M4). Sin columna "saldo_pendiente": se
/// calcula en vw_saldodocumentocxc (database/05_views.sql). Registrarla genera
/// automáticamente el AsientoContable correspondiente (ver CxCController) —
/// AsientoId queda poblado tras esa llamada, nunca se asigna a mano.
/// </summary>
public class DocumentoCxC
{
    public static readonly string[] TiposDocumentoValidos = { "Factura", "NotaCredito", "NotaDebito" };

    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal? TipoCambioAplicado { get; set; }
    public string Estado { get; set; } = "Vigente"; // Vigente | Anulado
    public int? AsientoId { get; set; }

    public List<LineaDocumentoCxC> Lineas { get; set; } = new();

    /// <summary>
    /// Cuenta de control (Activo/Deudora, ej. "Cuentas por cobrar") que recibe
    /// el débito del asiento generado al registrar la factura. Solo se usa en
    /// el POST de creación — no existe como columna en documentocxc, porque el
    /// asiento resultante ya guarda esa cuenta en su propia línea de débito
    /// (AsientoId -> LineaAsiento). [NotMapped] evita que EF intente mapearla
    /// a una columna inexistente.
    /// </summary>
    [NotMapped]
    public int CuentaControlId { get; set; }

    /// <summary>
    /// Periodo contable donde se registra el asiento generado — igual patrón
    /// que AsientoContable.PeriodoId en AsientosController.Registrar.
    /// </summary>
    [NotMapped]
    public int PeriodoId { get; set; }
}
