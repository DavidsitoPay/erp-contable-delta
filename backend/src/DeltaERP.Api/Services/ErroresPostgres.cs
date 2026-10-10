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
        ["23514"] = StatusCodes.Status400BadRequest,
    };

    // Los procedimientos almacenados señalan perfil no autorizado (P0001), inexistente (P0002)
    // estado inválido (55000) y regla de check no cumplida (23514) con RAISE EXCEPTION; cualquier otro error se propaga.
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

    // Npgsql trata 55000 como transitorio y NpgsqlExecutionStrategy lo envuelve en InvalidOperationException.
    public static PostgresException? ObtenerPostgres(Exception ex)
    {
        var actualizacion = ex as DbUpdateException ?? (ex as InvalidOperationException)?.InnerException as DbUpdateException;
        return actualizacion?.InnerException as PostgresException;
    }

    public static ObjectResult? TraducirActualizacionAsync(Exception ex)
    {
        if (ObtenerPostgres(ex) is { } pg && EstadoHttpPorSqlState.TryGetValue(pg.SqlState, out var status))
        {
            return new ObjectResult(new { error = pg.MessageText }) { StatusCode = status };
        }
        return null;
    }
}
