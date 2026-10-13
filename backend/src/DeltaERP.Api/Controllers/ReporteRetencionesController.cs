using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/retenciones")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class ReporteRetencionesController : ControllerBase
{
    private readonly ReporteRetencionService _reporte;

    public ReporteRetencionesController(ReporteRetencionService reporte)
    {
        _reporte = reporte;
    }

    [HttpGet("reporte")]
    public async Task<IActionResult> Reporte(
        [FromQuery] int anio, [FromQuery] int mes, [FromQuery] string? sentido, [FromQuery] string? concepto,
        [FromQuery] string? nit, [FromQuery] string? estadoConstancia)
    {
        var filtro = new FiltroReporteRetencion(anio, mes, Limpiar(sentido), Limpiar(concepto), nit, Limpiar(estadoConstancia));
        var error = PeriodoFiscalRules.Validar(anio, mes) ?? ReporteRetencionService.ValidarFiltro(filtro);
        return error is null ? Ok(await _reporte.GenerarAsync(filtro)) : BadRequest(new { error });
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();
}
