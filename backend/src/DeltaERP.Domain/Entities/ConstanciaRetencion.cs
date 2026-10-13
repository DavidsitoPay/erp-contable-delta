namespace DeltaERP.Domain.Entities;

public class ConstanciaRetencion
{
    public const string TipoIsr = "ISR";
    public const string TipoIva = "IVA";
    public const string EstadoEmitida = "EMITIDA";
    public const string EstadoAnulada = "ANULADA";
    public static readonly string[] FormulariosValidos = { "SAT-1331", "SAT-1352", "SAT-2340" };

    public int Id { get; set; }
    public int RetencionId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int? RetenedorContraparteId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string? Formulario { get; set; }
    public DateOnly? FechaPagoFormulario { get; set; }
    public DateOnly? PeriodoDeclarado { get; set; }
    public string Estado { get; set; } = EstadoEmitida;
    public string? MotivoAnulacion { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public int CreadoPor { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
    public int? ActualizadoPor { get; set; }
}
