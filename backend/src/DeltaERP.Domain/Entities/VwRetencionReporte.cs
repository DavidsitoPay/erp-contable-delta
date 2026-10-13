namespace DeltaERP.Domain.Entities;

// Vista de solo lectura (vw_retencion_reporte), sin clave primaria propia.
public class VwRetencionReporte
{
    public int RetencionId { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Sentido { get; set; } = string.Empty;
    public int ContraparteId { get; set; }
    public string? Nit { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int DocumentoId { get; set; }
    public string DocumentoTipo { get; set; } = string.Empty;
    public string DocumentoNumero { get; set; } = string.Empty;
    public string? DteSerie { get; set; }
    public string? DteNumero { get; set; }
    public Guid? DteUuid { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public DateOnly FechaOperacion { get; set; }
    public DateOnly PeriodoFiscal { get; set; }
    public decimal Base { get; set; }
    public decimal Tasa { get; set; }
    public decimal MontoCalculado { get; set; }
    public decimal MontoRetenido { get; set; }
    public int AsientoId { get; set; }
    public DateOnly FechaAsiento { get; set; }
    public int? ConstanciaId { get; set; }
    public string? ConstanciaNumero { get; set; }
    public DateOnly? ConstanciaFecha { get; set; }
    public string? Formulario { get; set; }
    public DateOnly? FechaPagoFormulario { get; set; }
    public DateOnly? PeriodoDeclarado { get; set; }
    public string? ConstanciaEstado { get; set; }
}
