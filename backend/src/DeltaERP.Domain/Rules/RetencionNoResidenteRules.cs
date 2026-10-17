using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class RetencionNoResidenteRules
{
    public static bool Aplica(Contraparte proveedor) =>
        !proveedor.EsResidente && proveedor.RegimenIsr != Contraparte.RegimenIsrExento;

    public static string? CodigoRenta(Contraparte proveedor, string? elegido) =>
        string.IsNullOrWhiteSpace(elegido) ? proveedor.TipoRentaNrCodigo : elegido.Trim().ToUpperInvariant();

    public static decimal BaseGtq(decimal montoOrigen, decimal tipoCambio) => MotorFiscal.Redondear(montoOrigen * tipoCambio);

    public static decimal Retencion(decimal baseImponible, decimal tasa) => MotorFiscal.Redondear(baseImponible * tasa / 100m);
}
