namespace DeltaERP.Domain.Entities;

public class Impuesto
{
    public const string TipoIvaGeneral = "IVA_GENERAL";
    public const string TipoExento = "EXENTO";
    public const string TipoNoAfecto = "NO_AFECTO";
    public const string TipoPequenoContribuyente = "PEQUENO_CONTRIBUYENTE";
    public const string TipoLegado = "LEGADO";

    public const string AmbitoVentas = "VENTAS";
    public const string AmbitoCompras = "COMPRAS";
    public const string AmbitoAmbos = "AMBOS";

    public static readonly string[] TiposCreables = { TipoIvaGeneral, TipoExento, TipoNoAfecto, TipoPequenoContribuyente };
    public static readonly string[] AmbitosValidos = { AmbitoVentas, AmbitoCompras, AmbitoAmbos };

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public decimal Tasa { get; set; }
    public string AplicaA { get; set; } = AmbitoAmbos;
    public bool GeneraCredito { get; set; }
    public string ArticuloLegal { get; set; } = string.Empty;
    public string? Nota { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public bool Activo { get; set; } = true;
}
