using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/retenciones")]
[Authorize(Roles = Roles.ReportesFiscales)]
public class ConstanciasRetencionController : ControllerBase
{
    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly ConstanciaRetencionService _constancias;

    public ConstanciasRetencionController(DeltaErpDbContext db, AuditoriaService auditoria, ConstanciaRetencionService constancias)
    {
        _db = db;
        _auditoria = auditoria;
        _constancias = constancias;
    }

    [HttpPost("{retencionId:int}/constancia")]
    public async Task<IActionResult> RegistrarConstancia(int retencionId, [FromBody] SolicitudConstancia datos)
    {
        var retencion = await _db.Retenciones.FindAsync(retencionId);
        if (retencion is null)
        {
            return NotFound();
        }
        var error = await _constancias.ValidarNuevaAsync(retencion, datos);
        if (error is not null)
        {
            return StatusCode(error.Estado, new { error = error.Mensaje });
        }

        var constancia = ConstanciaRetencionService.Registrar(retencion, datos, this.UsuarioId());
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "registrar_constancia_retencion", "constanciaretencion", async () =>
        {
            _db.ConstanciasRetencion.Add(constancia);
            await _db.SaveChangesAsync();
            return $"Constancia {constancia.Numero} ({constancia.Tipo}) registrada para la retención {retencion.Id}";
        });
        var advertencias = RetencionRules.AdvertenciasConstancia(retencion.Concepto, retencion.FechaDocumento, constancia.Fecha);
        return conflicto ?? Created($"/api/retenciones/constancias/{constancia.Id}", ConstanciaRespuesta.Desde(constancia, advertencias));
    }

    [HttpPost("constancias/{id:int}/anular")]
    public async Task<IActionResult> AnularConstancia(int id, [FromBody] SolicitudMotivo datos)
    {
        var errorMotivo = ConstanciaRetencionService.ValidarMotivo(datos.Motivo, "de la anulación");
        if (errorMotivo is not null)
        {
            return BadRequest(new { error = errorMotivo });
        }
        var constancia = await _db.ConstanciasRetencion.FindAsync(id);
        if (constancia is null)
        {
            return NotFound();
        }
        if (constancia.Estado == ConstanciaRetencion.EstadoAnulada)
        {
            return Conflict(new { error = "La constancia ya está anulada." });
        }

        ConstanciaRetencionService.Anular(constancia, datos.Motivo ?? string.Empty, this.UsuarioId());
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "anular_constancia_retencion", "constanciaretencion", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Constancia {constancia.Numero} (id {id}) anulada: {constancia.MotivoAnulacion}";
        });
        return conflicto ?? Ok(ConstanciaRespuesta.Desde(constancia, []));
    }
}
