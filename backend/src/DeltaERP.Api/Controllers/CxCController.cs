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

// CrearFactura genera su AsientoContable (débito CuentaControlId, crédito por
// línea) en la misma transacción; CrearPago delega movimiento y asiento a TesoreriaService.
[ApiController]
[Route("api/cxc")]
[Authorize]
public class CxCController : FacturaControllerBase
{
    private static readonly PerfilFactura Perfil = new(
        "CxC", "Cliente", DocumentoCxC.TiposDocumentoValidos, "Activo", "Deudora", "Cuentas por cobrar", LadoControl.Debito,
        Impuesto.AmbitoVentas, "IVA débito fiscal", true);

    private readonly RetencionService _retenciones;

    public CxCController(
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
            from d in _db.DocumentosCxC
            join s in _db.SaldosDocumentoCxC on d.Id equals s.DocumentoId
            join c in _db.Contrapartes on d.ClienteId equals c.Id
            join m in _db.Monedas on d.MonedaId equals m.Id
            orderby d.Fecha descending, d.Numero descending
            select new
            {
                d.Id,
                d.Numero,
                d.TipoDocumento,
                d.ClienteId,
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
                ClienteNombre = c.Nombre,
            }).ToListAsync();

        return Ok(facturas);
    }

    [HttpGet("facturas/{id:int}")]
    public async Task<IActionResult> ObtenerFactura(int id)
    {
        var documento = await _db.DocumentosCxC.Include(d => d.Lineas).FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return NotFound();
        }

        return Ok(await _validacion.ObtenerDetalleAsync(documento, Perfil));
    }

    [HttpPost("facturas")]
    [Authorize(Roles = Roles.GestionCxC)]
    public Task<IActionResult> CrearFactura([FromBody] DocumentoCxC documento) =>
        RegistrarFacturaAsync(documento, Perfil, nameof(ObtenerFactura));

    [HttpGet("facturas/{id:int}/retencion-iva-sugerida")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> RetencionIvaSugerida(int id)
    {
        var sugerencia = await _retenciones.SugerirIvaAsync(id);
        return sugerencia is null ? NotFound() : Ok(sugerencia);
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> ListarPagos()
    {
        var pagos = await (
            from r in _db.RecibosPagoCliente
            join c in _db.Contrapartes on r.ClienteId equals c.Id
            join m in _db.Monedas on r.MonedaId equals m.Id
            orderby r.Fecha descending, r.Id descending
            select new { r.Id, r.ClienteId, ClienteNombre = c.Nombre, r.Fecha, r.MontoTotal, r.MontoTotalGtq, Moneda = m.Codigo, r.MetodoPago, r.ReferenciaBancaria }
            ).ToListAsync();

        var ids = pagos.Select(p => p.Id).ToList();
        var aplicaciones = await _db.AplicacionesPagoCliente
            .Where(a => ids.Contains(a.ReciboPagoId))
            .Join(_db.DocumentosCxC, a => a.DocumentoId, d => d.Id, (a, d) => new
            {
                a.Id,
                a.ReciboPagoId,
                d.Numero,
                a.MontoAplicado,
                a.MontoAplicadoGtq,
                a.ValorGtq,
                a.DiferencialGtq,
                TipoCambioPago = _db.TiposCambio.Where(t => t.Id == a.TipoCambioPagoId).Select(t => (decimal?)t.Tasa).FirstOrDefault(),
            })
            .ToListAsync();
        var idsAplicacion = aplicaciones.Select(a => (int?)a.Id).ToList();
        var retenido = await _db.Retenciones
            .Where(r => r.Concepto == Retencion.ConceptoIva && idsAplicacion.Contains(r.AplicacionPagoClienteId))
            .ToDictionaryAsync(r => r.AplicacionPagoClienteId ?? 0, r => r.MontoRetenido);

        var resultado = pagos.Select(p => new
        {
            p.Id,
            p.ClienteId,
            p.ClienteNombre,
            p.Fecha,
            p.MontoTotal,
            p.MontoTotalGtq,
            p.Moneda,
            MontoNeto = p.MontoTotal - aplicaciones.Where(a => a.ReciboPagoId == p.Id).Sum(a => retenido.GetValueOrDefault(a.Id)),
            p.MetodoPago,
            p.ReferenciaBancaria,
            Aplicaciones = aplicaciones
                .Where(a => a.ReciboPagoId == p.Id)
                .Select(a => new
                {
                    a.Numero,
                    a.MontoAplicado,
                    RetencionIva = retenido.GetValueOrDefault(a.Id),
                    a.MontoAplicadoGtq,
                    a.ValorGtq,
                    a.DiferencialGtq,
                    a.TipoCambioPago,
                }),
        });

        return Ok(resultado);
    }

    [HttpPost("pagos")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> CrearPago([FromBody] ReciboPagoCliente recibo)
    {
        var entradas = AplicacionEntrada.Desde(recibo.Aplicaciones);
        var aplicaciones = entradas.Select(a => (a.DocumentoId, a.Monto)).ToList();

        var errorSolicitud = PagoRules.ValidarSolicitud(aplicaciones.Select(a => a.Monto).ToList(), recibo.MetodoPago);
        if (errorSolicitud is not null)
        {
            return BadRequest(new { error = errorSolicitud });
        }

        var (cliente, errorCliente) = await _validacion.ValidarTerceroAsync(recibo.ClienteId, "Cliente");
        if (cliente is null)
        {
            return BadRequest(new { error = errorCliente });
        }

        var idsDocumento = aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();
        var documentos = await (
            from d in _db.DocumentosCxC
            join s in _db.SaldosDocumentoCxC on d.Id equals s.DocumentoId
            where idsDocumento.Contains(d.Id)
            select new DocumentoPagable(
                d.Id, d.Numero, d.ClienteId, d.Estado, s.SaldoPendiente, d.AsientoId, d.TipoDocumento == TiposDocumento.NotaCredito,
                d.MonedaId, d.TipoCambioAplicado, s.SaldoPendienteGtq, d.MontoTotalOriginal)
            ).ToListAsync();

        var errorAplicaciones = PagoRules.ValidarAplicaciones("cliente", recibo.ClienteId, aplicaciones, documentos);
        if (errorAplicaciones is not null)
        {
            return BadRequest(new { error = errorAplicaciones });
        }

        var (retencionIva, errorRetencion) = await _retenciones.PrepararIvaClienteAsync(cliente, recibo);
        if (errorRetencion is not null)
        {
            return this.Fallo(errorRetencion);
        }

        var (bancario, errorBanco) = await _tesoreria.PrepararPagoAsync(
            Perfil, recibo.CuentaBancariaId, recibo.Fecha, documentos, entradas, retencionIva.ParaAsiento());
        if (bancario is null)
        {
            return this.Fallo(errorBanco!);
        }

        var usuarioId = this.UsuarioId();

        recibo.Id = 0;
        recibo.MonedaId = bancario.MonedaDocumentoId;
        recibo.MontoTotal = bancario.TotalAplicado;
        recibo.MontoTotalGtq = bancario.TotalValorGtq;
        foreach (var aplicacion in recibo.Aplicaciones)
        {
            aplicacion.Id = 0;
        }
        TesoreriaService.AplicarLiquidacion(recibo.Aplicaciones, bancario);

        IReadOnlyList<RetencionCobroRespuesta> retenciones = [];
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "registrar_pago_cxc", "recibopagocliente", async () =>
        {
            _db.RecibosPagoCliente.Add(recibo);
            await _db.SaveChangesAsync();
            var asientoId = await _tesoreria.RegistrarMovimientoDePagoAsync(
                bancario, Perfil, recibo.Fecha, $"Cobro {recibo.Id} - {cliente.Nombre}", recibo.ReferenciaBancaria, recibo.Id, usuarioId);
            retenciones = await _retenciones.RegistrarIvaClienteAsync(retencionIva, recibo, asientoId, usuarioId);

            var detalleRetencion = retenciones.Count == 0 ? string.Empty : $", retención IVA Q{retenciones.Sum(r => r.Monto)}, cobro neto Q{bancario.Neto}";
            return $"Recibo {recibo.Id} (cliente {cliente.Nombre}) registrado, monto {recibo.MontoTotal}{detalleRetencion}{TesoreriaService.DescribirDiferencial(bancario)}";
        });

        var aplicacionesGuardadas = recibo.Aplicaciones
            .Select(a => new AplicacionCobroRespuesta(
                a.Id, a.ReciboPagoId, a.DocumentoId, a.MontoAplicado, a.MontoAplicadoGtq, a.ValorGtq, a.DiferencialGtq, bancario.TipoCambioPago))
            .ToList();
        return conflicto ?? CreatedAtAction(
            nameof(ListarPagos),
            new { },
            new ReciboCobroRespuesta(
                recibo.Id, recibo.ClienteId, recibo.Fecha, recibo.MontoTotal, bancario.MontoNetoDocumento, recibo.MetodoPago,
                recibo.ReferenciaBancaria, recibo.CuentaBancariaId, aplicacionesGuardadas, retenciones, retencionIva.Advertencias,
                bancario.MonedaDocumentoCodigo, recibo.MontoTotalGtq, bancario.MontoCuenta, bancario.DiferencialRealizado));
    }
}
