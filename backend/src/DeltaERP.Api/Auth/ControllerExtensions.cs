using System.Security.Claims;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Auth;

public static class ControllerExtensions
{
    public static int UsuarioId(this ControllerBase controlador) =>
        int.Parse(controlador.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static ObjectResult Fallo(this ControllerBase controlador, ErrorValidacion error) =>
        controlador.StatusCode(error.Estado, new { error = error.Mensaje });
}
