using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/reportes/saldos-antiguedad")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class ReporteSaldosController : ControllerBase
{
    private readonly ReporteSaldosService _reporte;

    public ReporteSaldosController(ReporteSaldosService reporte)
    {
        _reporte = reporte;
    }

    [HttpGet]
    public async Task<IActionResult> Generar([FromQuery] string? tipo, [FromQuery] string? moneda, [FromQuery] DateOnly? corte)
    {
        var (reporte, error) = await _reporte.GenerarAsync(tipo, moneda, corte);
        return reporte is null ? this.Fallo(error!) : Ok(reporte);
    }
}
