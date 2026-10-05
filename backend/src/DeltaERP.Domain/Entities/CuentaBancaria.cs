namespace DeltaERP.Domain.Entities;

public class CuentaBancaria
{
    public static readonly string[] TiposValidos = { "Monetaria", "Ahorro" };

    public int Id { get; set; }
    public string Banco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public int CuentaContableId { get; set; }
    public decimal SaldoApertura { get; set; }
    public DateOnly? FechaApertura { get; set; }
}
