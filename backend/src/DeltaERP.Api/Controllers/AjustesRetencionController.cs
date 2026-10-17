using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/retenciones/ajustes")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class AjustesRetencionController : ControllerBase
{
    private readonly AjusteRetencionService _ajustes;

    public AjustesRetencionController(AjusteRetencionService ajustes)
    {
        _ajustes = ajustes;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? estado) => Ok(await _ajustes.ListarAsync(estado));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var ajuste = await _ajustes.ObtenerAsync(id);
        return ajuste is null ? NotFound(new { error = "El ajuste de retención indicado no existe." }) : Ok(ajuste);
    }

    [HttpPost("{id:int}/resolver")]
    public async Task<IActionResult> Resolver(int id, [FromBody] SolicitudResolverAjuste solicitud)
    {
        var (ajuste, error) = await _ajustes.ResolverAsync(id, solicitud, this.UsuarioId());
        return ajuste is null ? this.Fallo(error!) : Ok(ajuste);
    }
}
