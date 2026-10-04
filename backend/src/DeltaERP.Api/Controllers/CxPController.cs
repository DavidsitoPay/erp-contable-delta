using System.Security.Claims;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// M5 Cuentas por pagar: facturas de proveedores y aplicación de pagos
/// (RN-05), simétrico a CxCController. La única asimetría real es el asiento
/// generado: aquí se CREDITA la cuenta de control (Pasivo/Acreedora, ej.
/// "Cuentas por pagar") y se DEBITA la cuenta de cada línea (gasto/activo
/// adquirido) — lo inverso de una factura de venta.
/// </summary>
[ApiController]
[Route("api/cxp")]
[Authorize]
public class CxPController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public CxPController(DeltaErpDbContext db)
    {
        _db = db;
    }

    // ---- Facturas ----------------------------------------------------------

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
                ProveedorNombre = c.Nombre,
                d.Fecha,
                d.FechaVencimiento,
                d.MontoTotal,
                d.Estado,
                d.AsientoId,
                s.SaldoPendiente,
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

        var saldo = await _db.SaldosDocumentoCxP.FirstOrDefaultAsync(s => s.DocumentoId == id);
        var idsCuenta = documento.Lineas.Select(l => l.CuentaContableId).Distinct().ToList();
        var cuentas = await _db.CuentasContables.Where(c => idsCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        return Ok(new
        {
            documento.Id,
            documento.Numero,
            documento.TipoDocumento,
            documento.ProveedorId,
            documento.Fecha,
            documento.FechaVencimiento,
            documento.MontoTotal,
            documento.TipoCambioAplicado,
            documento.Estado,
            documento.AsientoId,
            SaldoPendiente = saldo?.SaldoPendiente ?? 0,
            Lineas = documento.Lineas.Select(l =>
            {
                cuentas.TryGetValue(l.CuentaContableId, out var cuenta);
                return new
                {
                    l.Descripcion,
                    l.Cantidad,
                    l.PrecioUnitario,
                    l.PorcentajeImpuesto,
                    l.CentroCostoId,
                    l.CuentaContableId,
                    CuentaCodigo = cuenta?.Codigo,
                    CuentaNombre = cuenta?.Nombre,
                };
            }),
        });
    }

    [HttpPost("facturas")]
    [Authorize(Roles = Roles.GestionCxP)]
    public async Task<IActionResult> CrearFactura([FromBody] DocumentoCxP documento)
    {
        if (!DocumentoCxP.TiposDocumentoValidos.Contains(documento.TipoDocumento))
        {
            return BadRequest(new { error = $"Tipo de documento inválido. Debe ser uno de: {string.Join(", ", DocumentoCxP.TiposDocumentoValidos)}." });
        }
        if (documento.Lineas.Count == 0)
        {
            return BadRequest(new { error = "La factura debe tener al menos una línea." });
        }
        if (documento.Lineas.Any(l => l.Cantidad <= 0 || l.PrecioUnitario < 0 || l.PorcentajeImpuesto < 0))
        {
            return BadRequest(new { error = "Cada línea debe tener cantidad mayor a cero, precio unitario y porcentaje de impuesto no negativos." });
        }

        var proveedor = await _db.Contrapartes.FindAsync(documento.ProveedorId);
        if (proveedor is null || proveedor.Tipo != "Proveedor")
        {
            return BadRequest(new { error = "El proveedor indicado no existe en el catálogo de contrapartes." });
        }

        var periodo = await _db.PeriodosContables.FindAsync(documento.PeriodoId);
        if (periodo is null)
        {
            return BadRequest(new { error = "El periodo indicado no existe." });
        }
        if (periodo.Estado != PeriodoContable.EstadoAbierto)
        {
            return BadRequest(new { error = $"El periodo '{periodo.Nombre}' está en estado '{periodo.Estado}'; no se pueden registrar facturas en un periodo que no esté Abierto." });
        }

        var idsCuentaUsados = documento.Lineas.Select(l => l.CuentaContableId).Append(documento.CuentaControlId).Distinct().ToList();
        var errorCuentas = await ValidarCuentasAsync(idsCuentaUsados);
        if (errorCuentas is not null)
        {
            return errorCuentas;
        }

        var cuentaControl = await _db.CuentasContables.FindAsync(documento.CuentaControlId);
        if (cuentaControl!.Tipo != "Pasivo" || cuentaControl.Naturaleza != "Acreedora")
        {
            return BadRequest(new { error = "La cuenta de control de CxP debe ser de tipo Pasivo y naturaleza Acreedora (ej. \"Cuentas por pagar\")." });
        }

        var idsCentroCostoUsados = documento.Lineas.Where(l => l.CentroCostoId is not null).Select(l => l.CentroCostoId!.Value).Distinct().ToList();
        var errorCentros = await ValidarCentrosCostoAsync(idsCentroCostoUsados);
        if (errorCentros is not null)
        {
            return errorCentros;
        }

        var montosLinea = documento.Lineas
            .Select(l => Math.Round(l.Cantidad * l.PrecioUnitario * (1 + l.PorcentajeImpuesto / 100m), 2, MidpointRounding.AwayFromZero))
            .ToList();
        var montoTotal = montosLinea.Sum();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var asiento = new AsientoContable
        {
            Numero = $"CXP-{documento.Numero}",
            Fecha = documento.Fecha,
            PeriodoId = documento.PeriodoId,
            Estado = "Confirmado",
            UsuarioId = usuarioId,
            TipoCambioAplicado = documento.TipoCambioAplicado,
            Monto = montoTotal,
            Lineas = new List<LineaAsiento> { new() { CuentaId = documento.CuentaControlId, Debito = 0, Credito = montoTotal } },
        };
        for (var i = 0; i < documento.Lineas.Count; i++)
        {
            asiento.Lineas.Add(new LineaAsiento
            {
                CuentaId = documento.Lineas[i].CuentaContableId,
                CentroCostoId = documento.Lineas[i].CentroCostoId,
                Debito = montosLinea[i],
                Credito = 0,
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.AsientosContables.Add(asiento);
        await _db.SaveChangesAsync();

        documento.Id = 0;
        documento.Estado = "Vigente";
        documento.MontoTotal = montoTotal;
        documento.AsientoId = asiento.Id;
        foreach (var linea in documento.Lineas)
        {
            linea.Id = 0;
        }
        _db.DocumentosCxP.Add(documento);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_factura_cxp"}, {"documentocxp"}, {$"Factura {documento.Numero} (id {documento.Id}) registrada para proveedor {proveedor.Nombre}, monto {montoTotal}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(ObtenerFactura), new { id = documento.Id }, documento);
    }

    // ---- Pagos ---------------------------------------------------------------

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
    [Authorize(Roles = Roles.GestionCxP)]
    public async Task<IActionResult> CrearPago([FromBody] PagoProveedorCabecera pago)
    {
        if (pago.Aplicaciones.Count == 0)
        {
            return BadRequest(new { error = "El pago debe aplicarse al menos a una factura." });
        }
        if (pago.Aplicaciones.Any(a => a.MontoAplicado <= 0))
        {
            return BadRequest(new { error = "El monto aplicado a cada factura debe ser mayor a cero." });
        }
        if (string.IsNullOrWhiteSpace(pago.MetodoPago))
        {
            return BadRequest(new { error = "El método de pago es obligatorio." });
        }

        var proveedor = await _db.Contrapartes.FindAsync(pago.ProveedorId);
        if (proveedor is null || proveedor.Tipo != "Proveedor")
        {
            return BadRequest(new { error = "El proveedor indicado no existe en el catálogo de contrapartes." });
        }

        var idsDocumento = pago.Aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();
        var documentos = await _db.DocumentosCxP.Where(d => idsDocumento.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var saldos = await _db.SaldosDocumentoCxP.Where(s => idsDocumento.Contains(s.DocumentoId)).ToDictionaryAsync(s => s.DocumentoId);

        var documentosInexistentes = idsDocumento.Where(id => !documentos.ContainsKey(id)).ToList();
        if (documentosInexistentes.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no existen: {string.Join(", ", documentosInexistentes)}." });
        }
        var documentosDeOtroProveedor = documentos.Values.Where(d => d.ProveedorId != pago.ProveedorId).Select(d => d.Numero).ToList();
        if (documentosDeOtroProveedor.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no pertenecen al proveedor indicado: {string.Join(", ", documentosDeOtroProveedor)}." });
        }
        var documentosAnulados = documentos.Values.Where(d => d.Estado != "Vigente").Select(d => d.Numero).ToList();
        if (documentosAnulados.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no están vigentes: {string.Join(", ", documentosAnulados)}." });
        }

        var aplicadoPorDocumento = pago.Aplicaciones.GroupBy(a => a.DocumentoId).ToDictionary(g => g.Key, g => g.Sum(a => a.MontoAplicado));
        var excedidos = aplicadoPorDocumento
            .Where(kv => kv.Value > saldos[kv.Key].SaldoPendiente)
            .Select(kv => $"{documentos[kv.Key].Numero} (saldo: {saldos[kv.Key].SaldoPendiente})")
            .ToList();
        if (excedidos.Count > 0)
        {
            return BadRequest(new { error = $"RN-05: el monto aplicado excede el saldo pendiente de: {string.Join(", ", excedidos)}." });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        pago.Id = 0;
        pago.MontoTotal = pago.Aplicaciones.Sum(a => a.MontoAplicado);
        foreach (var aplicacion in pago.Aplicaciones)
        {
            aplicacion.Id = 0;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.PagosProveedor.Add(pago);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_pago_cxp"}, {"pagoproveedorcabecera"}, {$"Pago {pago.Id} (proveedor {proveedor.Nombre}) registrado, monto {pago.MontoTotal}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(ListarPagos), new { }, pago);
    }

    // ---- Validaciones compartidas --------------------------------------------

    private async Task<IActionResult?> ValidarCuentasAsync(List<int> idsCuentaUsados)
    {
        var cuentasUsadas = await _db.CuentasContables.Where(c => idsCuentaUsados.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var idsConHijos = (await _db.CuentasContables
            .Where(c => c.CuentaPadreId != null && idsCuentaUsados.Contains(c.CuentaPadreId!.Value))
            .Select(c => c.CuentaPadreId!.Value)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var inexistentes = new List<string>();
        var inactivas = new List<string>();
        var deMayor = new List<string>();
        foreach (var cuentaId in idsCuentaUsados)
        {
            if (!cuentasUsadas.TryGetValue(cuentaId, out var cuenta))
            {
                inexistentes.Add(cuentaId.ToString());
                continue;
            }
            if (!cuenta.Activa)
            {
                inactivas.Add(cuenta.Codigo);
            }
            if (idsConHijos.Contains(cuentaId))
            {
                deMayor.Add(cuenta.Codigo);
            }
        }
        if (inexistentes.Count > 0)
        {
            return new BadRequestObjectResult(new { error = $"Las siguientes cuentas no existen: {string.Join(", ", inexistentes)}." });
        }
        if (inactivas.Count > 0)
        {
            return new BadRequestObjectResult(new { error = $"Las siguientes cuentas están inactivas: {string.Join(", ", inactivas)}." });
        }
        if (deMayor.Count > 0)
        {
            return new BadRequestObjectResult(new { error = $"Las siguientes cuentas son de mayor (tienen subcuentas) y no pueden recibir movimientos directos: {string.Join(", ", deMayor)}." });
        }
        return null;
    }

    private async Task<IActionResult?> ValidarCentrosCostoAsync(List<int> idsCentroCostoUsados)
    {
        if (idsCentroCostoUsados.Count == 0)
        {
            return null;
        }
        var existentes = (await _db.CentrosCosto.Where(c => idsCentroCostoUsados.Contains(c.Id)).Select(c => c.Id).ToListAsync()).ToHashSet();
        var inexistentes = idsCentroCostoUsados.Where(id => !existentes.Contains(id)).ToList();
        if (inexistentes.Count > 0)
        {
            return new BadRequestObjectResult(new { error = $"Los siguientes centros de costo no existen: {string.Join(", ", inexistentes)}." });
        }
        return null;
    }
}
