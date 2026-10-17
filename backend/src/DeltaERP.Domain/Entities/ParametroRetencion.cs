namespace DeltaERP.Domain.Entities;

public class ParametroRetencion
{
    public const string ConceptoIsrProveedor = "ISR_PROVEEDOR_ROS";
    public const string ConceptoIvaCliente = "IVA_RETENIDO_CLIENTE";
    public const string ConceptoPlazoConstancia = "PLAZO_CONSTANCIA";
    public const string ConceptoIsrNoResidente = "ISR_NO_RESIDENTE";
    public const string UmbralBase = "BASE";
    public const string UmbralTotal = "TOTAL";
    public const string NotaReferencial = "Referencial, validar con asesor";

    public const string CodigoIsrProveedor = "ISR_PROVEEDOR_ROS";
    public const string CodigoIvaSectorPublico = "IVA_CLIENTE_SECTOR_PUBLICO";
    public const string CodigoIvaAgentePrivado = "IVA_CLIENTE_AGENTE_PRIVADO";
    public const string CodigoPlazoConstanciaIsr = "PLAZO_CONSTANCIA_ISR";

    public static readonly string[] ConceptosValidos = { ConceptoIsrProveedor, ConceptoIvaCliente, ConceptoPlazoConstancia, ConceptoIsrNoResidente };
    public static readonly string[] UmbralesValidos = { UmbralBase, UmbralTotal };

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal? TasaBase { get; set; }
    public decimal? TramoLimite { get; set; }
    public decimal? TasaExcedente { get; set; }
    public decimal UmbralMinimo { get; set; }
    public string UmbralSobre { get; set; } = UmbralBase;
    public short? DiaLimite { get; set; }
    public string ArticuloLegal { get; set; } = string.Empty;
    public string Nota { get; set; } = NotaReferencial;
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public bool Activo { get; set; } = true;
}
