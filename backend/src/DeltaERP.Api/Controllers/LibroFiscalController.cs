using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/libros/fiscal")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class LibroFiscalController : ControllerBase
{
    private readonly LibroFiscalService _libro;

    public LibroFiscalController(LibroFiscalService libro)
    {
        _libro = libro;
    }

    [HttpGet("ventas")]
    public Task<IActionResult> Ventas([FromQuery] int anio, [FromQuery] int mes) => GenerarAsync(true, anio, mes);

    [HttpGet("compras")]
    public Task<IActionResult> Compras([FromQuery] int anio, [FromQuery] int mes) => GenerarAsync(false, anio, mes);

    private async Task<IActionResult> GenerarAsync(bool esVenta, int anio, int mes)
    {
        if (mes is < 1 or > 12)
        {
            return BadRequest(new { error = "El mes debe estar entre 1 y 12." });
        }
        if (anio is < 2000 or > 2100)
        {
            return BadRequest(new { error = "El año debe estar entre 2000 y 2100." });
        }
        return Ok(await _libro.GenerarAsync(esVenta, anio, mes));
    }
}
