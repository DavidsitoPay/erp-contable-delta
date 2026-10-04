using System.Collections.Frozen;

namespace DeltaERP.Domain.Entities;

// Sin columna de saldo: se calcula en tiempo real en vw_balance_saldos.
public class CuentaContable
{
    private const string Deudora = "Deudora";
    private const string Acreedora = "Acreedora";

    public static readonly string[] TiposValidos = { "Activo", "Pasivo", "Capital", "Ingreso", "Gasto" };
    public static readonly string[] NaturalezasValidas = { Deudora, Acreedora };

    // Una naturaleza inconsistente con el tipo invierte el signo del saldo en vw_balance_saldos.
    public static readonly IReadOnlyDictionary<string, string> NaturalezaEsperadaPorTipo = new Dictionary<string, string>
    {
        ["Activo"] = Deudora,
        ["Gasto"] = Deudora,
        ["Pasivo"] = Acreedora,
        ["Capital"] = Acreedora,
        ["Ingreso"] = Acreedora,
    }.ToFrozenDictionary();

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty;
    public int? CuentaPadreId { get; set; }
    public bool Activa { get; set; } = true;
}
