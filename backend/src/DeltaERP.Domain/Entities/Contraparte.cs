namespace DeltaERP.Domain.Entities;

// Sin columna "activa" (a diferencia de CuentaContable/CentroCosto): una
// contraparte solo admite editar sus datos de contacto y fiscales.
public class Contraparte
{
    private const string ValorExento = "EXENTO";

    public static readonly string[] TiposValidos = { "Cliente", "Proveedor" };
    public static readonly string[] RegimenesIvaValidos = { RegimenIvaGeneral, RegimenIvaPequenoContribuyente, RegimenIvaExento };
    public static readonly string[] RegimenesIsrValidos = { RegimenIsrUtilidades, RegimenIsrSimplificado, RegimenIsrPagoDirecto, RegimenIsrExento, RegimenIsrNoDefinido };
    public static readonly string[] TiposAgenteIvaValidos = { AgenteSectorPublico, AgentePrivado };

    public const string RegimenIvaGeneral = "GENERAL";
    public const string RegimenIvaPequenoContribuyente = "PEQUENO_CONTRIBUYENTE";
    public const string RegimenIvaExento = ValorExento;

    public const string RegimenIsrUtilidades = "UTILIDADES";
    public const string RegimenIsrSimplificado = "SIMPLIFICADO";
    public const string RegimenIsrPagoDirecto = "PAGO_DIRECTO";
    public const string RegimenIsrExento = ValorExento;
    public const string RegimenIsrNoDefinido = "NO_DEFINIDO";

    public const string AgenteSectorPublico = "SECTOR_PUBLICO";
    public const string AgentePrivado = "AGENTE_PRIVADO";

    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
    public string RegimenIva { get; set; } = RegimenIvaGeneral;
    public string RegimenIsr { get; set; } = RegimenIsrNoDefinido;
    public string? IsrFundamento { get; set; }
    public bool EsResidente { get; set; } = true;
    public bool EsAgenteRetencionIva { get; set; }
    public string? TipoAgenteIva { get; set; }
    public string? TipoRentaNrCodigo { get; set; }
}
