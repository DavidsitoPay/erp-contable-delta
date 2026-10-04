using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class AuditoriaService
{
    private readonly DeltaErpDbContext _db;

    public AuditoriaService(DeltaErpDbContext db)
    {
        _db = db;
    }

    // operacion persiste los cambios y devuelve el detalle de la auditoría; el
    // registro y la operación confirman o se revierten juntos.
    public async Task EjecutarAsync(int usuarioId, string accion, string tabla, Func<Task<string>> operacion)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var detalle = await operacion();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {accion}, {tabla}, {detalle})");

        await transaction.CommitAsync();
    }
}
