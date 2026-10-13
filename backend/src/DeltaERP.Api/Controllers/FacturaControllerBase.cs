using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

public abstract class FacturaControllerBase : ControllerBase
{
    protected readonly DeltaErpDbContext _db;
    protected readonly ValidacionContable _validacion;
    protected readonly AuditoriaService _auditoria;
    protected readonly FacturaService _facturas;
    protected readonly TesoreriaService _tesoreria;

    protected FacturaControllerBase(
        DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria, FacturaService facturas, TesoreriaService tesoreria)
    {
        _db = db;
        _validacion = validacion;
        _auditoria = auditoria;
        _facturas = facturas;
        _tesoreria = tesoreria;
    }

    protected async Task<IActionResult> RegistrarFacturaAsync(IDocumentoFactura documento, PerfilFactura perfil, string accionDetalle)
    {
        // RN-04
        var (tercero, error) = await _validacion.ValidarFacturaAsync(documento, documento.TerceroId, perfil);
        if (error is not null || tercero is null)
        {
            return BadRequest(new { error });
        }

        var registro = await _facturas.RegistrarAsync(documento, tercero, perfil, this.UsuarioId());
        if (registro.Error is { } errorFiscal)
        {
            return StatusCode(errorFiscal.Estado, new { error = errorFiscal.Mensaje });
        }

        var detalle = await _validacion.ObtenerDetalleAsync(documento, perfil);
        var respuesta = detalle with
        {
            Advertencias = registro.Advertencias,
            RetencionIsr = registro.Retencion?.ComoRespuesta(registro.RetencionId, documento.MontoTotal) ?? detalle.RetencionIsr,
        };
        return CreatedAtAction(accionDetalle, new { id = documento.Id }, respuesta);
    }
}
