using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Rules;
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
        var error = PeriodoFiscalRules.Validar(anio, mes);
        return error is null ? Ok(await _libro.GenerarAsync(esVenta, anio, mes)) : BadRequest(new { error });
    }
}
