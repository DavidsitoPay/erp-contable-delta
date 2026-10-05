using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Services;

public static class ErroresPostgres
{
    private static readonly Dictionary<string, int> EstadoHttpPorSqlState = new()
    {
        ["P0001"] = StatusCodes.Status403Forbidden,
        ["P0002"] = StatusCodes.Status404NotFound,
        ["55000"] = StatusCodes.Status409Conflict,
    };

    // Los procedimientos almacenados señalan perfil no autorizado (P0001), inexistente (P0002)
    // y estado inválido (55000) con RAISE EXCEPTION; cualquier otro error se propaga.
    public static async Task<ObjectResult?> TraducirProcedimientoAsync(Func<Task> invocar)
    {
        try
        {
            await invocar();
            return null;
        }
        catch (PostgresException ex) when (EstadoHttpPorSqlState.ContainsKey(ex.SqlState))
        {
            return new ObjectResult(new { error = ex.MessageText }) { StatusCode = EstadoHttpPorSqlState[ex.SqlState] };
        }
    }

    public static bool EsViolacionUnica(DbUpdateException ex, string constraint) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pg && pg.ConstraintName == constraint;

    public static ObjectResult? TraducirActualizacionAsync(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pg && EstadoHttpPorSqlState.TryGetValue(pg.SqlState, out var status))
        {
            return new ObjectResult(new { error = pg.MessageText }) { StatusCode = status };
        }
        return null;
    }
}
