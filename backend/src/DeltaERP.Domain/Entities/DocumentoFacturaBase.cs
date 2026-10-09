using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public abstract class DocumentoFacturaBase<TLinea> where TLinea : class, ILineaFactura
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
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

    public List<TLinea> Lineas { get; set; } = new();

    // Solo se usa en el POST de creación; el asiento generado ya guarda esta
    // cuenta en su propia línea, por eso no persiste como columna.
    [NotMapped]
    public int CuentaControlId { get; set; }

    // Request-only: no persiste en la tabla del documento.
    [NotMapped]
    public int PeriodoId { get; set; }
}
