using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public static class ConsultasRevaluacion
{
    public static Task<bool> EsAsientoDeRevaluacionAsync(this DeltaErpDbContext db, int asientoId) =>
        db.Revaluaciones.AnyAsync(r =>
            r.AsientoId == asientoId || r.AsientoReversaId == asientoId
            || r.AsientoAnulacionId == asientoId || r.AsientoAnulacionReversaId == asientoId);

    public static async Task<HashSet<int>> AsientosDeRevaluacionAsync(this DeltaErpDbContext db, IReadOnlyCollection<int> ids)
    {
        var filas = await db.Revaluaciones
            .Where(r => ids.Contains(r.AsientoId) || ids.Contains(r.AsientoReversaId)
                        || (r.AsientoAnulacionId != null && ids.Contains(r.AsientoAnulacionId.Value))
                        || (r.AsientoAnulacionReversaId != null && ids.Contains(r.AsientoAnulacionReversaId.Value)))
            .Select(r => new { r.AsientoId, r.AsientoReversaId, r.AsientoAnulacionId, r.AsientoAnulacionReversaId })
            .ToListAsync();
        return filas
            .SelectMany(f => new int?[] { f.AsientoId, f.AsientoReversaId, f.AsientoAnulacionId, f.AsientoAnulacionReversaId })
            .OfType<int>()
            .Where(ids.Contains)
            .ToHashSet();
    }
}
