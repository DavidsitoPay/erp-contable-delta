using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public record LineaFacturaDetalle(
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal PorcentajeImpuesto,
    int? CentroCostoId,
    int CuentaContableId,
    string? CuentaCodigo,
    string? CuentaNombre)
{
    public static List<LineaFacturaDetalle> Desde(IEnumerable<ILineaFactura> lineas, IReadOnlyDictionary<int, CuentaContable> cuentas) =>
        lineas.Select(l =>
        {
            cuentas.TryGetValue(l.CuentaContableId, out var cuenta);
            return new LineaFacturaDetalle(
                l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeImpuesto,
                l.CentroCostoId, l.CuentaContableId, cuenta?.Codigo, cuenta?.Nombre);
        }).ToList();
}
