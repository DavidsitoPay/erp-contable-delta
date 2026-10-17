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

    private static readonly Dictionary<string, string> MensajePorRestriccionUnica = new()
    {
        ["ux_retencion_isr_documento"] = "La factura ya tiene retención de ISR registrada.",
        ["ux_retencion_iva_documento"] = "La factura ya tiene IVA retenido registrado.",
        ["ux_constancia_retencion"] = "La retención ya tiene una constancia emitida.",
        ["ux_constancia_numero"] = "Ya existe una constancia emitida con ese número para el mismo tipo y retenedor.",
        ["ux_revaluacion_vigente"] = "Ya existe una revaluación vigente para ese mes y moneda; anúlela antes de repetirla.",
        ["ux_historialtipocambio_activa"] = "Ya existe un tipo de cambio activo para esa moneda y fecha.",
        ["ux_enteramiento_boleta"] = "Ya existe un enteramiento vigente con ese formulario y boleta.",
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
        if (ex is PostgresException directa)
        {
            return directa;
        }
        var actualizacion = ex as DbUpdateException ?? (ex as InvalidOperationException)?.InnerException as DbUpdateException;
        return actualizacion?.InnerException as PostgresException;
    }

    public static ObjectResult? TraducirActualizacionAsync(Exception ex)
    {
        if (ObtenerPostgres(ex) is { } pg && EstadoHttpPorSqlState.TryGetValue(pg.SqlState, out var status))
        {
            return new ObjectResult(new { error = pg.MessageText }) { StatusCode = status };
        }
        return MensajeRestriccionUnica(ex) is { } mensaje
            ? new ObjectResult(new { error = mensaje }) { StatusCode = StatusCodes.Status409Conflict }
            : null;
    }

    // 55000 -> 409, 23514 -> 400 y las restricciones únicas conocidas -> 409.
    public static ErrorValidacion? ComoErrorValidacion(Exception ex)
    {
        if (ObtenerPostgres(ex) is { } pg
            && EstadoHttpPorSqlState.TryGetValue(pg.SqlState, out var estado)
            && estado is StatusCodes.Status409Conflict or StatusCodes.Status400BadRequest)
        {
            return new ErrorValidacion(estado, pg.MessageText);
        }
        return MensajeRestriccionUnica(ex) is { } mensaje ? ErrorValidacion.Conflicto(mensaje) : null;
    }

    // Conflicto (409) de los triggers (55000) y de los índices únicos de retenciones y constancias.
    public static string? MensajeConflicto(Exception ex) =>
        ObtenerPostgres(ex) is { SqlState: "55000" } pg ? pg.MessageText : MensajeRestriccionUnica(ex);

    private static string? MensajeRestriccionUnica(Exception ex) =>
        ObtenerPostgres(ex) is { SqlState: "23505", ConstraintName: { } restriccion }
        && MensajePorRestriccionUnica.TryGetValue(restriccion, out var mensaje)
            ? mensaje
            : null;
}
