using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

// A diferencia de CxC, aquí se CREDITA la cuenta de control (Pasivo/Acreedora) y
// se DEBITA la cuenta de cada línea: es la operación inversa de una factura de venta.
[ApiController]
[Route("api/cxp")]
[Authorize]
public class CxPController : FacturaControllerBase
{
    private static readonly PerfilFactura Perfil = new(
        "CxP", "Proveedor", DocumentoCxP.TiposDocumentoValidos, "Pasivo", "Acreedora", "Cuentas por pagar", LadoControl.Credito,
        Impuesto.AmbitoCompras, "IVA crédito fiscal", false);

    private readonly RetencionService _retenciones;

    public CxPController(
        DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria, FacturaService facturas, TesoreriaService tesoreria,
        RetencionService retenciones)
        : base(db, validacion, auditoria, facturas, tesoreria)
    {
        _retenciones = retenciones;
    }

    [HttpGet("facturas")]
    public async Task<IActionResult> ListarFacturas()
    {
        var facturas = await (
            from d in _db.DocumentosCxP
            join s in _db.SaldosDocumentoCxP on d.Id equals s.DocumentoId
            join c in _db.Contrapartes on d.ProveedorId equals c.Id
            join m in _db.Monedas on d.MonedaId equals m.Id
            orderby d.Fecha descending, d.Numero descending
            select new
            {
                d.Id,
                d.Numero,
                d.TipoDocumento,
                d.ProveedorId,
                d.Fecha,
                d.FechaVencimiento,
                d.MontoTotal,
                d.MontoBase,
                d.MontoIva,
                d.Estado,
                d.AsientoId,
                d.DteSerie,
                d.DteNumero,
                d.CalculoLegado,
                Moneda = m.Codigo,
                d.MontoTotalOriginal,
                d.TipoCambioAplicado,
                d.TipoCambioFuente,
                d.TipoCambioArrastrado,
                s.SaldoPendiente,
                s.SaldoPendienteGtq,
                MontoRetencionIsr = _db.Retenciones
                    .Where(r => r.DocumentoCxpId == d.Id && r.Concepto == Retencion.ConceptoIsr)
                    .Sum(r => (decimal?)r.MontoRetenido) ?? 0m,
                d.RevisionRetencion,
                d.RevisionMotivo,
                AjusteRetencionPendiente = _db.AjustesRetencion.Any(a => a.Estado == AjusteRetencion.EstadoPendiente
                    && (a.NotaCreditoId == d.Id || _db.Retenciones.Any(r => r.Id == a.RetencionId && r.DocumentoCxpId == d.Id))),
                ProveedorNombre = c.Nombre,
            }).ToListAsync();

        return Ok(facturas);
    }

    [HttpGet("facturas/{id:int}")]
    public async Task<IActionResult> ObtenerFactura(int id)
    {
        var documento = await _db.DocumentosCxP.Include(d => d.Lineas).FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return NotFound();
        }

        return Ok(await _validacion.ObtenerDetalleAsync(documento, Perfil));
    }

    [HttpPost("facturas")]
    [Authorize(Roles = Roles.GestionCxP)]
    public Task<IActionResult> CrearFactura([FromBody] DocumentoCxP documento) =>
        RegistrarFacturaAsync(documento, Perfil, nameof(ObtenerFactura));

    [HttpPost("facturas/retencion-previa")]
    [Authorize(Roles = Roles.GestionCxP)]
    public async Task<IActionResult> RetencionPrevia([FromBody] SolicitudRetencionPrevia solicitud)
    {
        var (proveedor, errorProveedor) = await _validacion.ValidarTerceroAsync(solicitud.ProveedorId, Perfil.TipoTercero);
        if (proveedor is null)
        {
            return BadRequest(new { error = errorProveedor });
        }
        if (solicitud.MontoBase < 0 || solicitud.MontoTotal < solicitud.MontoBase)
        {
            return BadRequest(new { error = "El monto base debe ser mayor o igual a cero y no puede exceder el monto total." });
        }

        var (vista, error) = await _retenciones.VistaPreviaIsrAsync(proveedor, solicitud.Fecha, solicitud.MontoBase, solicitud.MontoTotal);
        return error is null ? Ok(vista) : StatusCode(error.Estado, new { error = error.Mensaje });
    }

    [HttpPost("facturas/{id:int}/revision-retencion")]
    [Authorize(Roles = Roles.GestionCxP)]
    public async Task<IActionResult> ResolverRevisionRetencion(int id, [FromBody] SolicitudMotivo solicitud)
    {
        var errorMotivo = ConstanciaRetencionService.ValidarMotivo(solicitud.Motivo, "de la resolución");
        if (errorMotivo is not null)
        {
            return BadRequest(new { error = errorMotivo });
        }
        var documento = await _db.DocumentosCxP.Include(d => d.Lineas).FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return NotFound();
        }
        if (documento.RevisionRetencion != DocumentoCxP.RevisionPendiente)
        {
            return Conflict(new { error = "La factura no tiene una revisión de retención pendiente." });
        }

        documento.RevisionRetencion = DocumentoCxP.RevisionResuelta;
        documento.RevisionMotivo = solicitud.Motivo?.Trim();
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "resolver_revision_retencion", "documentocxp", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Revisión de retención de la factura {documento.Numero} (id {id}) resuelta: {documento.RevisionMotivo}, calculado Q0, aplicado Q0";
        });
        return conflicto ?? Ok(await _validacion.ObtenerDetalleAsync(documento, Perfil));
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> ListarPagos()
    {
        var pagos = await (
            from p in _db.PagosProveedor
            join c in _db.Contrapartes on p.ProveedorId equals c.Id
            join m in _db.Monedas on p.MonedaId equals m.Id
            orderby p.Fecha descending, p.Id descending
            select new { p.Id, p.ProveedorId, ProveedorNombre = c.Nombre, p.Fecha, p.MontoTotal, p.MontoTotalGtq, Moneda = m.Codigo, p.MetodoPago, p.ReferenciaBancaria }
            ).ToListAsync();

        var ids = pagos.Select(p => p.Id).ToList();
        var aplicaciones = await _db.AplicacionesPagoProveedor
            .Where(a => ids.Contains(a.PagoCabeceraId))
            .Join(_db.DocumentosCxP, a => a.DocumentoId, d => d.Id, (a, d) => new
            {
                a.PagoCabeceraId,
                d.Numero,
                a.MontoAplicado,
                a.MontoAplicadoGtq,
                a.ValorGtq,
                a.DiferencialGtq,
                TipoCambioPago = _db.TiposCambio.Where(t => t.Id == a.TipoCambioPagoId).Select(t => (decimal?)t.Tasa).FirstOrDefault(),
            })
            .ToListAsync();

        var resultado = pagos.Select(p => new
        {
            p.Id,
            p.ProveedorId,
            p.ProveedorNombre,
            p.Fecha,
            p.MontoTotal,
            p.MontoTotalGtq,
            p.Moneda,
            p.MetodoPago,
            p.ReferenciaBancaria,
            Aplicaciones = aplicaciones
                .Where(a => a.PagoCabeceraId == p.Id)
                .Select(a => new { a.Numero, a.MontoAplicado, a.MontoAplicadoGtq, a.ValorGtq, a.DiferencialGtq, a.TipoCambioPago }),
        });

        return Ok(resultado);
    }

    [HttpPost("pagos/retencion-previa")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> RetencionPreviaPago([FromBody] SolicitudRetencionPreviaPago solicitud)
    {
        var (proveedor, errorProveedor) = await _validacion.ValidarTerceroAsync(solicitud.ProveedorId, "Proveedor");
        if (proveedor is null)
        {
            return BadRequest(new { error = errorProveedor });
        }

        var aplicaciones = solicitud.Aplicaciones.Select(a => (a.DocumentoId, a.MontoAplicado)).ToList();
        var documentos = await CargarDocumentosPagablesAsync(aplicaciones.Select(a => a.DocumentoId).Distinct());

        var errorAplicaciones = PagoRules.ValidarAplicaciones("proveedor", solicitud.ProveedorId, aplicaciones, documentos);
        if (errorAplicaciones is not null)
        {
            return BadRequest(new { error = errorAplicaciones });
        }

        var entradas = solicitud.Aplicaciones.Select(a => new EntradaIsrNr(a.DocumentoId, a.MontoAplicado, a.ValorEfectivoGtq, a.TipoRentaCodigo)).ToList();
        var (preparacion, errorNr) = await _retenciones.PrepararIsrNoResidenteAsync(proveedor, solicitud.Fecha, documentos, entradas);
        if (errorNr is not null)
        {
            return this.Fallo(errorNr);
        }

        if (!preparacion.Aplica)
        {
            return Ok(new RetencionPreviaPagoRespuesta(false, preparacion.MotivoNoAplica, [], 0m, 0m, null, []));
        }

        var valorGtq = preparacion.Lineas.Sum(l => l.ValorGtq);
        var errorNeto = TesoreriaRules.ValidarNetoPositivo(valorGtq, preparacion.TotalRetencionGtq);
        if (errorNeto is not null)
        {
            return BadRequest(new { error = errorNeto });
        }

        var lineas = preparacion.Lineas.Select(l => new LineaRetencionPrevia(
            l.DocumentoId, l.Parametro.Codigo, l.Parametro.Nombre, l.Parametro.TasaBase ?? 0m, l.Parametro.Id, l.MonedaCodigo, l.MontoOrigen,
            l.TipoCambio, l.TipoCambioFecha, l.BaseGtq, l.RetencionGtq)).ToList();
        return Ok(new RetencionPreviaPagoRespuesta(
            true, null, lineas, preparacion.Lineas.Sum(l => l.BaseGtq), preparacion.TotalRetencionGtq, valorGtq - preparacion.TotalRetencionGtq, []));
    }

    [HttpPost("pagos")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> CrearPago([FromBody] PagoProveedorCabecera pago)
    {
        var entradas = AplicacionEntrada.Desde(pago.Aplicaciones);
        var aplicaciones = entradas.Select(a => (a.DocumentoId, a.Monto)).ToList();

        var errorSolicitud = PagoRules.ValidarSolicitud(aplicaciones.Select(a => a.Monto).ToList(), pago.MetodoPago);
        if (errorSolicitud is not null)
        {
            return BadRequest(new { error = errorSolicitud });
        }

        var (proveedor, errorProveedor) = await _validacion.ValidarTerceroAsync(pago.ProveedorId, "Proveedor");
        if (proveedor is null)
        {
            return BadRequest(new { error = errorProveedor });
        }

        var idsDocumento = aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();
        var documentos = await CargarDocumentosPagablesAsync(idsDocumento);

        var errorAplicaciones = PagoRules.ValidarAplicaciones("proveedor", pago.ProveedorId, aplicaciones, documentos);
        if (errorAplicaciones is not null)
        {
            return BadRequest(new { error = errorAplicaciones });
        }

        var entradasNr = pago.Aplicaciones.Select(a => new EntradaIsrNr(a.DocumentoId, a.MontoAplicado, a.ValorEfectivoGtq, a.TipoRentaCodigo)).ToList();
        var (retencionNr, errorNr) = await _retenciones.PrepararIsrNoResidenteAsync(proveedor, pago.Fecha, documentos, entradasNr);
        if (errorNr is not null)
        {
            return this.Fallo(errorNr);
        }

        var (desembolso, errorDesembolso) = await _tesoreria.PrepararPagoAsync(
            Perfil, pago.CuentaBancariaId, pago.Fecha, documentos, entradas, retencionNr.ParaAsiento(), retencionNr.TotalRetencionOrigen);
        if (desembolso is null)
        {
            return this.Fallo(errorDesembolso!);
        }

        var usuarioId = this.UsuarioId();

        pago.Id = 0;
        pago.MonedaId = desembolso.MonedaDocumentoId;
        pago.MontoTotal = desembolso.TotalAplicado;
        pago.MontoTotalGtq = desembolso.TotalValorGtq;
        foreach (var aplicacion in pago.Aplicaciones)
        {
            aplicacion.Id = 0;
        }
        TesoreriaService.AplicarLiquidacion(pago.Aplicaciones, desembolso);

        IReadOnlyList<RetencionPagoRespuesta> retenciones = [];
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "registrar_pago_cxp", "pagoproveedorcabecera", async () =>
        {
            _db.PagosProveedor.Add(pago);
            await _db.SaveChangesAsync();
            var asientoId = await _tesoreria.RegistrarMovimientoDePagoAsync(
                desembolso, Perfil, pago.Fecha, $"Pago {pago.Id} - {proveedor.Nombre}", pago.ReferenciaBancaria, pago.Id, usuarioId);
            retenciones = await _retenciones.RegistrarIsrNoResidenteAsync(retencionNr, pago.Aplicaciones.ToList(), pago.ProveedorId, pago.Fecha, asientoId, usuarioId);

            var isrNr = retencionNr.Aplica ? $", ISR no residente Q{retencionNr.TotalRetencionGtq}" : string.Empty;
            return $"Pago {pago.Id} (proveedor {proveedor.Nombre}) registrado, monto {pago.MontoTotal}{TesoreriaService.DescribirDiferencial(desembolso)}{isrNr}";
        });

        var aplicacionesGuardadas = pago.Aplicaciones
            .Select(a => new AplicacionPagoRespuesta(
                a.Id, a.PagoCabeceraId, a.DocumentoId, a.MontoAplicado, a.MontoAplicadoGtq, a.ValorGtq, a.DiferencialGtq, desembolso.TipoCambioPago))
            .ToList();
        return conflicto ?? CreatedAtAction(
            nameof(ListarPagos),
            new { },
            new PagoProveedorRespuesta(
                pago.Id, pago.ProveedorId, pago.Fecha, pago.MontoTotal, pago.MetodoPago, pago.ReferenciaBancaria, pago.CuentaBancariaId,
                aplicacionesGuardadas, desembolso.MonedaDocumentoCodigo, pago.MontoTotalGtq, desembolso.MontoCuenta, desembolso.DiferencialRealizado,
                retenciones));
    }

    private async Task<List<DocumentoPagable>> CargarDocumentosPagablesAsync(IEnumerable<int> ids)
    {
        var idsDocumento = ids.ToList();
        return await (
            from d in _db.DocumentosCxP
            join s in _db.SaldosDocumentoCxP on d.Id equals s.DocumentoId
            where idsDocumento.Contains(d.Id)
            select new DocumentoPagable(
                d.Id, d.Numero, d.ProveedorId, d.Estado, s.SaldoPendiente, d.AsientoId, d.TipoDocumento == TiposDocumento.NotaCredito,
                d.MonedaId, d.TipoCambioAplicado, s.SaldoPendienteGtq, d.MontoTotalOriginal)
            ).ToListAsync();
    }
}
