using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

// Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxc. AsientoId
// queda poblado al registrar la factura (ver CxCController), nunca se asigna a mano.
public class DocumentoCxC : IDocumentoFactura
{
    public static readonly string[] TiposDocumentoValidos = TiposDocumento.Validos;

    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public int ClienteId { get; set; }
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

    public List<LineaDocumentoCxC> Lineas { get; set; } = new();

    IReadOnlyList<ILineaFactura> IDocumentoFactura.Lineas => Lineas;

    int IDocumentoFactura.TerceroId => ClienteId;

    // Solo se usa en el POST de creación; el asiento generado ya guarda esta
    // cuenta en su propia línea de débito, por eso no persiste como columna.
    [NotMapped]
    public int CuentaControlId { get; set; }

    // Request-only: no persiste en documentocxc.
    [NotMapped]
    public int PeriodoId { get; set; }
}
