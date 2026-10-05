using System.Security.Claims;
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
public class CxCController : ControllerBase
{
    private static readonly PerfilFactura Perfil = new("CxC", "Cliente", DocumentoCxC.TiposDocumentoValidos, "Activo", "Deudora", "Cuentas por cobrar", LadoControl.Debito);

    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly AuditoriaService _auditoria;
    private readonly FacturaService _facturas;
    private readonly TesoreriaService _tesoreria;

    public CxCController(DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria, FacturaService facturas, TesoreriaService tesoreria)
    {
        _db = db;
        _validacion = validacion;
        _auditoria = auditoria;
        _facturas = facturas;
        _tesoreria = tesoreria;
    }

    [HttpGet("facturas")]
    public async Task<IActionResult> ListarFacturas()
    {
        var facturas = await (
            from d in _db.DocumentosCxC
            join s in _db.SaldosDocumentoCxC on d.Id equals s.DocumentoId
            join c in _db.Contrapartes on d.ClienteId equals c.Id
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
                d.Estado,
                d.AsientoId,
                s.SaldoPendiente,
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

        var saldo = await _db.SaldosDocumentoCxC.FirstOrDefaultAsync(s => s.DocumentoId == id);
        var cuentas = await _validacion.ObtenerCuentasAsync(documento.Lineas);

        return Ok(FacturaDetalle.Desde(documento, saldo?.SaldoPendiente ?? 0, cuentas, "clienteId", documento.ClienteId));
    }

    [HttpPost("facturas")]
    [Authorize(Roles = Roles.GestionCxC)]
    public async Task<IActionResult> CrearFactura([FromBody] DocumentoCxC documento)
    {
        // RN-04
        var (cliente, error) = await _validacion.ValidarFacturaAsync(documento, documento.ClienteId, Perfil);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _facturas.RegistrarAsync(documento, cliente!, Perfil, usuarioId);

        return CreatedAtAction(nameof(ObtenerFactura), new { id = documento.Id }, documento);
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> ListarPagos()
    {
        var pagos = await (
            from r in _db.RecibosPagoCliente
            join c in _db.Contrapartes on r.ClienteId equals c.Id
            orderby r.Fecha descending, r.Id descending
            select new { r.Id, r.ClienteId, ClienteNombre = c.Nombre, r.Fecha, r.MontoTotal, r.MetodoPago, r.ReferenciaBancaria }
            ).ToListAsync();

        var ids = pagos.Select(p => p.Id).ToList();
        var aplicaciones = await _db.AplicacionesPagoCliente
            .Where(a => ids.Contains(a.ReciboPagoId))
            .Join(_db.DocumentosCxC, a => a.DocumentoId, d => d.Id, (a, d) => new { a.ReciboPagoId, d.Numero, a.MontoAplicado })
            .ToListAsync();

        var resultado = pagos.Select(p => new
        {
            p.Id,
            p.ClienteId,
            p.ClienteNombre,
            p.Fecha,
            p.MontoTotal,
            p.MetodoPago,
            p.ReferenciaBancaria,
            Aplicaciones = aplicaciones.Where(a => a.ReciboPagoId == p.Id).Select(a => new { a.Numero, a.MontoAplicado }),
        });

        return Ok(resultado);
    }

    [HttpPost("pagos")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> CrearPago([FromBody] ReciboPagoCliente recibo)
    {
        var aplicaciones = recibo.Aplicaciones.Select(a => (a.DocumentoId, Monto: a.MontoAplicado)).ToList();

        var errorSolicitud = PagoRules.ValidarSolicitud(aplicaciones.Select(a => a.Monto).ToList(), recibo.MetodoPago);
        if (errorSolicitud is not null)
        {
            return BadRequest(new { error = errorSolicitud });
        }

        var (cliente, errorCliente) = await _validacion.ValidarTerceroAsync(recibo.ClienteId, "Cliente");
        if (errorCliente is not null)
        {
            return BadRequest(new { error = errorCliente });
        }

        var idsDocumento = aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();
        var documentos = await (
            from d in _db.DocumentosCxC
            join s in _db.SaldosDocumentoCxC on d.Id equals s.DocumentoId
            where idsDocumento.Contains(d.Id)
            select new DocumentoPagable(d.Id, d.Numero, d.ClienteId, d.Estado, s.SaldoPendiente, d.AsientoId)
            ).ToListAsync();

        var errorAplicaciones = PagoRules.ValidarAplicaciones("cliente", recibo.ClienteId, aplicaciones, documentos);
        if (errorAplicaciones is not null)
        {
            return BadRequest(new { error = errorAplicaciones });
        }

        var (bancario, errorBanco) = await _tesoreria.PrepararPagoAsync(Perfil, recibo.CuentaBancariaId, recibo.Fecha, documentos, aplicaciones);
        if (errorBanco is not null)
        {
            return BadRequest(new { error = errorBanco });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        recibo.Id = 0;
        recibo.MontoTotal = aplicaciones.Sum(a => a.Monto);
        foreach (var aplicacion in recibo.Aplicaciones)
        {
            aplicacion.Id = 0;
        }

        await _auditoria.EjecutarAsync(usuarioId, "registrar_pago_cxc", "recibopagocliente", async () =>
        {
            _db.RecibosPagoCliente.Add(recibo);
            await _db.SaveChangesAsync();
            await _tesoreria.RegistrarMovimientoDePagoAsync(
                bancario!, Perfil, recibo.Fecha, $"Cobro {recibo.Id} - {cliente!.Nombre}", recibo.ReferenciaBancaria, recibo.Id, usuarioId);

            return $"Recibo {recibo.Id} (cliente {cliente!.Nombre}) registrado, monto {recibo.MontoTotal}";
        });

        return CreatedAtAction(nameof(ListarPagos), new { }, recibo);
    }
}
