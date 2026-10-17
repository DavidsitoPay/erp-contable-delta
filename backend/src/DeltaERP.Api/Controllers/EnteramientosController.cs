using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/retenciones/enteramientos")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class EnteramientosController : ControllerBase
{
    private readonly EnteramientoService _enteramientos;

    public EnteramientosController(EnteramientoService enteramientos)
    {
        _enteramientos = enteramientos;
    }

    [HttpGet("saldo")]
    public async Task<IActionResult> Saldo([FromQuery] DateOnly? periodo)
    {
        var (filas, error) = await _enteramientos.SaldoAsync(periodo);
        return filas is null ? this.Fallo(error!) : Ok(filas);
    }

    [HttpPost("vista-previa")]
    public async Task<IActionResult> VistaPrevia([FromBody] SolicitudVistaPreviaEnteramiento solicitud)
    {
        var (vista, error) = await _enteramientos.VistaPreviaAsync(solicitud);
        return vista is null ? this.Fallo(error!) : Ok(vista);
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] SolicitudEnteramiento solicitud)
    {
        var (entero, error) = await _enteramientos.RegistrarAsync(solicitud, this.UsuarioId());
        return entero is null
            ? this.Fallo(error!)
            : Created($"/api/retenciones/enteramientos?periodo={entero.Periodo:yyyy-MM-dd}", entero);
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] SolicitudMotivo solicitud)
    {
        var (entero, error) = await _enteramientos.AnularAsync(id, solicitud.Motivo, this.UsuarioId());
        return entero is null ? this.Fallo(error!) : Ok(entero);
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] DateOnly? periodo, [FromQuery] string? estado) =>
        Ok(await _enteramientos.ListarAsync(periodo, estado));
}
