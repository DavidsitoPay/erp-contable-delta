using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/tipos-cambio")]
[Authorize]
public class TiposCambioController : ControllerBase
{
    private readonly TipoCambioService _tipos;

    public TiposCambioController(TipoCambioService tipos)
    {
        _tipos = tipos;
    }

    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] string? moneda, [FromQuery] DateOnly? fecha)
    {
        if (string.IsNullOrWhiteSpace(moneda) || fecha is null)
        {
            return BadRequest(new { error = "Indique la moneda y la fecha." });
        }
        var (resultado, error) = await _tipos.ConsultarAsync(moneda, fecha.Value);
        return resultado is null ? this.Fallo(error!) : Ok(await _tipos.ComoRespuestaAsync(moneda, fecha.Value, resultado));
    }

    [HttpGet("historial")]
    [Authorize(Roles = Roles.GestionTesoreria)]
    public async Task<IActionResult> Historial(
        [FromQuery] string? moneda, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] bool incluirInactivas = false)
    {
        var (filas, error) = await _tipos.HistorialAsync(moneda, desde, hasta, incluirInactivas);
        return filas is null ? this.Fallo(error!) : Ok(filas);
    }

    [HttpPost("manual")]
    [Authorize(Roles = Roles.GestionTesoreria)]
    public async Task<IActionResult> CapturarManual([FromBody] SolicitudTasaManual solicitud)
    {
        var (fila, error) = await _tipos.CapturarManualAsync(
            new CapturaTasaManual(solicitud.MonedaCodigo, solicitud.Fecha, solicitud.Tasa, solicitud.Motivo), this.UsuarioId());
        return fila is null
            ? this.Fallo(error!)
            : CreatedAtAction(nameof(Historial), new { moneda = solicitud.MonedaCodigo, desde = solicitud.Fecha, hasta = solicitud.Fecha }, await _tipos.ObtenerDtoAsync(fila.Id));
    }

    [HttpPost("sustituir-por-banguat")]
    [Authorize(Roles = Roles.GestionTesoreria)]
    public async Task<IActionResult> SustituirPorBanguat([FromBody] SolicitudSustitucion solicitud)
    {
        var (fila, error) = await _tipos.SustituirPorBanguatAsync(solicitud.MonedaCodigo, solicitud.Fecha, this.UsuarioId());
        return fila is null ? this.Fallo(error!) : Ok(await _tipos.ObtenerDtoAsync(fila.Id));
    }

    [HttpPost("{id:int}/baja")]
    [Authorize(Roles = Roles.GestionTesoreria)]
    public async Task<IActionResult> DarDeBaja(int id, [FromBody] SolicitudMotivo solicitud)
    {
        var error = await _tipos.DarDeBajaAsync(id, solicitud.Motivo, this.UsuarioId());
        return error is null ? Ok(await _tipos.ObtenerDtoAsync(id)) : this.Fallo(error);
    }
}
