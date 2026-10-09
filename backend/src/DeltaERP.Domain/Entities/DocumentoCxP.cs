using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxp. El asiento
// se genera al registrarla (ver CxPController).
public class DocumentoCxP : IDocumentoFactura
{
    public static readonly string[] TiposDocumentoValidos = TiposDocumento.Validos;

    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public int ProveedorId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal MontoBase { get; set; }
    public decimal MontoIva { get; set; }
    public decimal? TipoCambioAplicado { get; set; }
    public bool CalculoLegado { get; set; }
    [JsonIgnore]
    public Guid? DteUuid { get; set; }

    // El JSON recibe el UUID como texto para validar el formato en la API.
    [NotMapped]
    [JsonPropertyName("dteUuid")]
    public string? DteUuidTexto { get; set; }

    public string? DteSerie { get; set; }
    public string? DteNumero { get; set; }
    public DateTimeOffset? DteFechaCertificacion { get; set; }
    public int? DocumentoOrigenId { get; set; }
    public string Estado { get; set; } = "Vigente"; // Vigente | Anulado
    public int? AsientoId { get; set; }

    public List<LineaDocumentoCxP> Lineas { get; set; } = new();

    IReadOnlyList<ILineaFactura> IDocumentoFactura.Lineas => Lineas;

    int IDocumentoFactura.TerceroId => ProveedorId;

    // Solo se usa en el POST de creación; ver CuentaControlId en DocumentoCxC.
    [NotMapped]
    public int CuentaControlId { get; set; }

    [NotMapped]
    public int PeriodoId { get; set; }
}
