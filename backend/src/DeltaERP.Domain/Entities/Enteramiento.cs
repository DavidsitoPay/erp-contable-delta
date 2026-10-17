namespace DeltaERP.Domain.Entities;

public class Enteramiento
{
    public const string EstadoVigente = "VIGENTE";
    public const string EstadoAnulado = "ANULADO";
    public const string ConceptoIsrNoResidente = "ISR_NORES";
    public static readonly string[] FormulariosValidos = { "SAT-1352", "SAT-1331" };

    public int Id { get; set; }
    public string Concepto { get; set; } = ConceptoIsrNoResidente;
    public string Formulario { get; set; } = string.Empty;
    public DateOnly Periodo { get; set; }
    public DateOnly FechaPago { get; set; }
    public string Boleta { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int CuentaBancariaId { get; set; }
    public int AsientoId { get; set; }
    public int MovimientoTesoreriaId { get; set; }
    public string Estado { get; set; } = EstadoVigente;
    public string? MotivoAnulacion { get; set; }
    public int? AnuladoPor { get; set; }
    public DateTimeOffset? AnuladoEn { get; set; }
    public int? AsientoReversaId { get; set; }
    public int? MovimientoReversaId { get; set; }
    public int RegistradoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
