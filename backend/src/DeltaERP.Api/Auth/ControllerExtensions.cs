using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Auth;

public static class ControllerExtensions
{
    public static int UsuarioId(this ControllerBase controlador) =>
        int.Parse(controlador.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
