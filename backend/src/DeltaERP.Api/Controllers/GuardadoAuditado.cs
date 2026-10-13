using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

public static class GuardadoAuditado
{
    // Devuelve el conflicto (409, 400 o 403) que traduce el error de la base de datos, o null si guardó.
    public static async Task<ObjectResult?> GuardarAuditadoAsync(
        this ControllerBase controlador, AuditoriaService auditoria, string accion, string tabla, Func<Task<string>> operacion)
    {
        try
        {
            await auditoria.EjecutarAsync(controlador.UsuarioId(), accion, tabla, operacion);
            return null;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException && ErroresPostgres.TraducirActualizacionAsync(ex) is { } conflicto)
        {
            return conflicto;
        }
    }
}
