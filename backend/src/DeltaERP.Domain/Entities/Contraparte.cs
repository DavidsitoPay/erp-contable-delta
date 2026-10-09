namespace DeltaERP.Domain.Entities;

// Sin columna "activa" (a diferencia de CuentaContable/CentroCosto): una
// contraparte solo admite editar sus datos de contacto y fiscales.
public class Contraparte
{
    public static readonly string[] TiposValidos = { "Cliente", "Proveedor" };
    public static readonly string[] RegimenesIvaValidos = { "GENERAL", "PEQUENO_CONTRIBUYENTE", "EXENTO" };
    public static readonly string[] RegimenesIsrValidos = { "UTILIDADES", "SIMPLIFICADO" };

    public const string RegimenIvaGeneral = "GENERAL";
    public const string RegimenIvaPequenoContribuyente = "PEQUENO_CONTRIBUYENTE";
    public const string RegimenIvaExento = "EXENTO";

    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
    public string RegimenIva { get; set; } = RegimenIvaGeneral;
    public string RegimenIsr { get; set; } = "UTILIDADES";
    public bool EsResidente { get; set; } = true;
    public bool EsAgenteRetencionIva { get; set; }
}
