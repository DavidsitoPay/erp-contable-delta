namespace DeltaERP.Domain.Entities;

// Vista de solo lectura, sin clave primaria propia.
public class SaldoCuentaBancaria
{
    public int CuentaBancariaId { get; set; }
    public string Banco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
}
