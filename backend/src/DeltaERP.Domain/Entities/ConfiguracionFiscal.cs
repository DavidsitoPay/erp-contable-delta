namespace DeltaERP.Domain.Entities;

// Fila única (Id = 1).
public class ConfiguracionFiscal
{
    public const int IdUnico = 1;
    public static readonly string[] RegimenesIsrValidos = { "UTILIDADES", "SIMPLIFICADO" };
    public static readonly string[] TiposAgenteIvaValidos =
    {
        "EXPORTADOR_HABITUAL", "SECTOR_PUBLICO", "TARJETA_CREDITO", "COMBUSTIBLE", "CONTRIBUYENTE_ESPECIAL",
    };

    public int Id { get; set; } = IdUnico;
    public string? NitEmpresa { get; set; }
    public string? NombreLegal { get; set; }
    public int MonedaFuncionalId { get; set; }
    public string RegimenIsr { get; set; } = "UTILIDADES";
    public bool AgenteRetencionIva { get; set; }
    public string? TipoAgenteIva { get; set; }
    public int? CuentaIvaDebitoId { get; set; }
    public int? CuentaIvaCreditoId { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
    public int? ActualizadoPor { get; set; }
}
