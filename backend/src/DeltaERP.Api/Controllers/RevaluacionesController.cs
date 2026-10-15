using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/revaluaciones")]
[Authorize(Roles = Roles.GestionTesoreria)]
public class RevaluacionesController : ControllerBase
{
    private readonly RevaluacionService _revaluaciones;

    public RevaluacionesController(RevaluacionService revaluaciones)
    {
        _revaluaciones = revaluaciones;
    }

    [HttpPost("vista-previa")]
    public async Task<IActionResult> VistaPrevia([FromBody] SolicitudRevaluacion solicitud)
    {
        var (vista, error) = await _revaluaciones.VistaPreviaAsync(solicitud);
        return vista is null ? this.Fallo(error!) : Ok(vista);
    }

    [HttpPost]
    public async Task<IActionResult> Ejecutar([FromBody] SolicitudRevaluacion solicitud)
    {
        var (revaluacion, error) = await _revaluaciones.EjecutarAsync(solicitud, this.UsuarioId());
        return revaluacion is null ? this.Fallo(error!) : CreatedAtAction(nameof(Obtener), new { id = revaluacion.Id }, revaluacion);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? anio, [FromQuery] int? mes, [FromQuery] string? monedaCodigo, [FromQuery] string? estado)
    {
        var (filas, error) = await _revaluaciones.ListarAsync(anio, mes, monedaCodigo, estado);
        return filas is null ? this.Fallo(error!) : Ok(filas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var revaluacion = await _revaluaciones.ObtenerAsync(id);
        return revaluacion is null ? NotFound() : Ok(revaluacion);
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] SolicitudMotivo solicitud)
    {
        var error = await _revaluaciones.AnularAsync(id, solicitud.Motivo, this.UsuarioId());
        return error is null ? Ok(await _revaluaciones.ObtenerAsync(id)) : this.Fallo(error);
    }
}
