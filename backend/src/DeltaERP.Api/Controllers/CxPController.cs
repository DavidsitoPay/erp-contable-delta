using System.Security.Claims;
using DeltaERP.Api.Auth;
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
public class CxPController : ControllerBase
{
    private static readonly PerfilFactura Perfil = new(
        "CxP", "Proveedor", DocumentoCxP.TiposDocumentoValidos, "Pasivo", "Acreedora", "Cuentas por pagar", LadoControl.Credito,
        Impuesto.AmbitoCompras, "IVA crédito fiscal", false);

    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly AuditoriaService _auditoria;
    private readonly FacturaService _facturas;
    private readonly TesoreriaService _tesoreria;

    public CxPController(DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria, FacturaService facturas, TesoreriaService tesoreria)
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
            from d in _db.DocumentosCxP
            join s in _db.SaldosDocumentoCxP on d.Id equals s.DocumentoId
            join c in _db.Contrapartes on d.ProveedorId equals c.Id
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
                s.SaldoPendiente,
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
    public async Task<IActionResult> CrearFactura([FromBody] DocumentoCxP documento)
    {
        // RN-04
        var (proveedor, error) = await _validacion.ValidarFacturaAsync(documento, documento.ProveedorId, Perfil);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        if (proveedor is null)
        {
            return BadRequest(new { error });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var errorFiscal = await _facturas.RegistrarAsync(documento, proveedor, Perfil, usuarioId);
        if (errorFiscal is not null)
        {
            return StatusCode(errorFiscal.Estado, new { error = errorFiscal.Mensaje });
        }

        return CreatedAtAction(nameof(ObtenerFactura), new { id = documento.Id }, await _validacion.ObtenerDetalleAsync(documento, Perfil));
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> ListarPagos()
    {
        var pagos = await (
            from p in _db.PagosProveedor
            join c in _db.Contrapartes on p.ProveedorId equals c.Id
            orderby p.Fecha descending, p.Id descending
            select new { p.Id, p.ProveedorId, ProveedorNombre = c.Nombre, p.Fecha, p.MontoTotal, p.MetodoPago, p.ReferenciaBancaria }
            ).ToListAsync();

        var ids = pagos.Select(p => p.Id).ToList();
        var aplicaciones = await _db.AplicacionesPagoProveedor
            .Where(a => ids.Contains(a.PagoCabeceraId))
            .Join(_db.DocumentosCxP, a => a.DocumentoId, d => d.Id, (a, d) => new { a.PagoCabeceraId, d.Numero, a.MontoAplicado })
            .ToListAsync();

        var resultado = pagos.Select(p => new
        {
            p.Id,
            p.ProveedorId,
            p.ProveedorNombre,
            p.Fecha,
            p.MontoTotal,
            p.MetodoPago,
            p.ReferenciaBancaria,
            Aplicaciones = aplicaciones.Where(a => a.PagoCabeceraId == p.Id).Select(a => new { a.Numero, a.MontoAplicado }),
        });

        return Ok(resultado);
    }

    [HttpPost("pagos")]
    [Authorize(Roles = Roles.RegistroPagos)]
    public async Task<IActionResult> CrearPago([FromBody] PagoProveedorCabecera pago)
    {
        var aplicaciones = pago.Aplicaciones.Select(a => (a.DocumentoId, Monto: a.MontoAplicado)).ToList();

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
        var documentos = await (
            from d in _db.DocumentosCxP
            join s in _db.SaldosDocumentoCxP on d.Id equals s.DocumentoId
            where idsDocumento.Contains(d.Id)
            select new DocumentoPagable(d.Id, d.Numero, d.ProveedorId, d.Estado, s.SaldoPendiente, d.AsientoId, d.TipoDocumento == TiposDocumento.NotaCredito)
            ).ToListAsync();

        var errorAplicaciones = PagoRules.ValidarAplicaciones("proveedor", pago.ProveedorId, aplicaciones, documentos);
        if (errorAplicaciones is not null)
        {
            return BadRequest(new { error = errorAplicaciones });
        }

        var (desembolso, errorDesembolso) = await _tesoreria.PrepararPagoAsync(Perfil, pago.CuentaBancariaId, pago.Fecha, documentos, aplicaciones);
        if (desembolso is null)
        {
            return BadRequest(new { error = errorDesembolso });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        pago.Id = 0;
        pago.MontoTotal = aplicaciones.Sum(a => a.Monto);
        foreach (var aplicacion in pago.Aplicaciones)
        {
            aplicacion.Id = 0;
        }

        await _auditoria.EjecutarAsync(usuarioId, "registrar_pago_cxp", "pagoproveedorcabecera", async () =>
        {
            _db.PagosProveedor.Add(pago);
            await _db.SaveChangesAsync();
            await _tesoreria.RegistrarMovimientoDePagoAsync(
                desembolso, Perfil, pago.Fecha, $"Pago {pago.Id} - {proveedor.Nombre}", pago.ReferenciaBancaria, pago.Id, usuarioId);

            return $"Pago {pago.Id} (proveedor {proveedor.Nombre}) registrado, monto {pago.MontoTotal}";
        });

        return CreatedAtAction(nameof(ListarPagos), new { }, pago);
    }
}
