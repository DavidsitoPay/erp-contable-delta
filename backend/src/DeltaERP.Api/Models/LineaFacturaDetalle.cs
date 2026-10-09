using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public record LineaFacturaDetalle(
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    int ImpuestoId,
    string? ImpuestoCodigo,
    string? ImpuestoNombre,
    decimal TasaAplicada,
    string TipoBienServicio,
    decimal MontoLinea,
    decimal MontoBase,
    decimal MontoIva,
    int? CentroCostoId,
    int CuentaContableId,
    string? CuentaCodigo,
    string? CuentaNombre)
{
    public static List<LineaFacturaDetalle> Desde(
        IEnumerable<ILineaFactura> lineas,
        IReadOnlyDictionary<int, CuentaContable> cuentas,
        IReadOnlyDictionary<int, Impuesto> impuestos) =>
        lineas.Select(l =>
        {
            cuentas.TryGetValue(l.CuentaContableId, out var cuenta);
            impuestos.TryGetValue(l.ImpuestoId, out var impuesto);
            return new LineaFacturaDetalle(
                l.Descripcion, l.Cantidad, l.PrecioUnitario, l.ImpuestoId, impuesto?.Codigo, impuesto?.Nombre,
                l.TasaAplicada, l.TipoBienServicio, l.MontoLinea, l.MontoBase, l.MontoIva,
                l.CentroCostoId, l.CuentaContableId, cuenta?.Codigo, cuenta?.Nombre);
        }).ToList();
}
