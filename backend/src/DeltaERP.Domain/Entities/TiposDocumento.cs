namespace DeltaERP.Domain.Entities;

public static class TiposDocumento
{
    public const string Factura = "Factura";
    public const string NotaCredito = "NotaCredito";
    public const string NotaDebito = "NotaDebito";
    public static readonly string[] Validos = { Factura, NotaCredito, NotaDebito };
}
