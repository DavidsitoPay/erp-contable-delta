namespace DeltaERP.Domain.Entities;

// Instantánea inmutable: la API solo inserta y consulta (delta_api sin UPDATE ni DELETE).
public class Retencion
{
    public const string ConceptoIsr = "ISR_ROS";
    public const string ConceptoIva = "IVA_CLIENTE";

    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public int ContraparteId { get; set; }
    public int? DocumentoCxpId { get; set; }
    public int? DocumentoCxcId { get; set; }
    public int? AplicacionPagoClienteId { get; set; }
    public int ParametroId { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public DateOnly FechaOperacion { get; set; }
    public int CuentaId { get; set; }
    public decimal Base { get; set; }
    public decimal Tasa { get; set; }
    public decimal MontoCalculado { get; set; }
    public decimal MontoRetenido { get; set; }
    public string? MotivoAjuste { get; set; }
    public int AsientoId { get; set; }
    public int RegistradoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
