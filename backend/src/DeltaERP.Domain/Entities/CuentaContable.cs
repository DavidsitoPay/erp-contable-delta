namespace DeltaERP.Domain.Entities;

/// <summary>
/// Cuenta del catálogo contable (módulo M1). Sin columna de saldo: se calcula
/// en tiempo real en vw_balance_saldos (ver database/05_views.sql).
/// </summary>
public class CuentaContable
{
    public static readonly string[] TiposValidos = { "Activo", "Pasivo", "Capital", "Ingreso", "Gasto" };
    public static readonly string[] NaturalezasValidas = { "Deudora", "Acreedora" };

    /// <summary>
    /// Naturaleza esperada según el tipo de cuenta (convención contable estándar).
    /// Un tipo=Activo con naturaleza=Acreedora, por ejemplo, invertiría el signo
    /// del saldo calculado en vw_balance_saldos (ver database/05_views.sql).
    /// </summary>
    public static readonly Dictionary<string, string> NaturalezaEsperadaPorTipo = new()
    {
        ["Activo"] = "Deudora",
        ["Gasto"] = "Deudora",
        ["Pasivo"] = "Acreedora",
        ["Capital"] = "Acreedora",
        ["Ingreso"] = "Acreedora",
    };

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Naturaleza { get; set; } = string.Empty;
    public int? CuentaPadreId { get; set; }
    public bool Activa { get; set; } = true;
}
