using System.Security.Claims;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

// CrearFactura genera su AsientoContable (débito CuentaControlId, crédito por
// línea) en la misma transacción. CrearPago NO genera asiento: recibopagocliente
// no tiene columna asiento_id; el impacto en bancos queda para Tesorería.
[ApiController]
[Route("api/cxc")]
[Authorize]
public class CxCController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public CxCController(DeltaErpDbContext db)
    {
        _db = db;
    }

    // ---- Facturas ----------------------------------------------------------

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
                ClienteNombre = c.Nombre,
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
        var documento = await _db.DocumentosCxC.Include(d => d.Lineas).FirstOrDefaultAsync(d => d.Id == id);
        if (documento is null)
        {
            return NotFound();
        }

        var saldo = await _db.SaldosDocumentoCxC.FirstOrDefaultAsync(s => s.DocumentoId == id);
        var idsCuenta = documento.Lineas.Select(l => l.CuentaContableId).Distinct().ToList();
        var cuentas = await _db.CuentasContables.Where(c => idsCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        return Ok(new
        {
            documento.Id,
            documento.Numero,
            documento.TipoDocumento,
            documento.ClienteId,
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
    [Authorize(Roles = Roles.GestionCxC)]
    public async Task<IActionResult> CrearFactura([FromBody] DocumentoCxC documento)
    {
        if (!DocumentoCxC.TiposDocumentoValidos.Contains(documento.TipoDocumento))
        {
            return BadRequest(new { error = $"Tipo de documento inválido. Debe ser uno de: {string.Join(", ", DocumentoCxC.TiposDocumentoValidos)}." });
        }
        if (documento.Lineas.Count == 0)
        {
            return BadRequest(new { error = "La factura debe tener al menos una línea." });
        }
        if (documento.Lineas.Any(l => l.Cantidad <= 0 || l.PrecioUnitario < 0 || l.PorcentajeImpuesto < 0))
        {
            return BadRequest(new { error = "Cada línea debe tener cantidad mayor a cero, precio unitario y porcentaje de impuesto no negativos." });
        }

        // RN-04
        var cliente = await _db.Contrapartes.FindAsync(documento.ClienteId);
        if (cliente is null || cliente.Tipo != "Cliente")
        {
            return BadRequest(new { error = "El cliente indicado no existe en el catálogo de contrapartes." });
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

        // Valida cuenta de control y cuentas de línea juntas: ambas terminan en
        // LineaAsiento del asiento generado.
        var idsCuentaUsados = documento.Lineas.Select(l => l.CuentaContableId).Append(documento.CuentaControlId).Distinct().ToList();
        var errorCuentas = await ValidarCuentasAsync(idsCuentaUsados);
        if (errorCuentas is not null)
        {
            return errorCuentas;
        }

        var cuentaControl = await _db.CuentasContables.FindAsync(documento.CuentaControlId);
        if (cuentaControl!.Tipo != "Activo" || cuentaControl.Naturaleza != "Deudora")
        {
            return BadRequest(new { error = "La cuenta de control de CxC debe ser de tipo Activo y naturaleza Deudora (ej. \"Cuentas por cobrar\")." });
        }

        var idsCentroCostoUsados = documento.Lineas.Where(l => l.CentroCostoId is not null).Select(l => l.CentroCostoId!.Value).Distinct().ToList();
        var errorCentros = await ValidarCentrosCostoAsync(idsCentroCostoUsados);
        if (errorCentros is not null)
        {
            return errorCentros;
        }

        // Redondeo por línea antes de sumar: el total coincide centavo a centavo con
        // la suma de créditos, evitando falsos "no cuadra" en trg_validar_partida_doble.
        var montosLinea = documento.Lineas
            .Select(l => Math.Round(l.Cantidad * l.PrecioUnitario * (1 + l.PorcentajeImpuesto / 100m), 2, MidpointRounding.AwayFromZero))
            .ToList();
        var montoTotal = montosLinea.Sum();

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // RN-01: cuadra por construcción (débito = crédito = montoTotal).
        var asiento = new AsientoContable
        {
            Numero = $"CXC-{documento.Numero}",
            Fecha = documento.Fecha,
            PeriodoId = documento.PeriodoId,
            Estado = "Confirmado",
            UsuarioId = usuarioId,
            TipoCambioAplicado = documento.TipoCambioAplicado,
            Monto = montoTotal,
            Lineas = new List<LineaAsiento> { new() { CuentaId = documento.CuentaControlId, Debito = montoTotal, Credito = 0 } },
        };
        for (var i = 0; i < documento.Lineas.Count; i++)
        {
            asiento.Lineas.Add(new LineaAsiento
            {
                CuentaId = documento.Lineas[i].CuentaContableId,
                CentroCostoId = documento.Lineas[i].CentroCostoId,
                Debito = 0,
                Credito = montosLinea[i],
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
        _db.DocumentosCxC.Add(documento);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_factura_cxc"}, {"documentocxc"}, {$"Factura {documento.Numero} (id {documento.Id}) registrada para cliente {cliente.Nombre}, monto {montoTotal}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(ObtenerFactura), new { id = documento.Id }, documento);
    }

    // ---- Pagos ---------------------------------------------------------------

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
    [Authorize(Roles = Roles.GestionCxC)]
    public async Task<IActionResult> CrearPago([FromBody] ReciboPagoCliente recibo)
    {
        if (recibo.Aplicaciones.Count == 0)
        {
            return BadRequest(new { error = "El pago debe aplicarse al menos a una factura." });
        }
        if (recibo.Aplicaciones.Any(a => a.MontoAplicado <= 0))
        {
            return BadRequest(new { error = "El monto aplicado a cada factura debe ser mayor a cero." });
        }
        if (string.IsNullOrWhiteSpace(recibo.MetodoPago))
        {
            return BadRequest(new { error = "El método de pago es obligatorio." });
        }

        var cliente = await _db.Contrapartes.FindAsync(recibo.ClienteId);
        if (cliente is null || cliente.Tipo != "Cliente")
        {
            return BadRequest(new { error = "El cliente indicado no existe en el catálogo de contrapartes." });
        }

        var idsDocumento = recibo.Aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();
        var documentos = await _db.DocumentosCxC.Where(d => idsDocumento.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var saldos = await _db.SaldosDocumentoCxC.Where(s => idsDocumento.Contains(s.DocumentoId)).ToDictionaryAsync(s => s.DocumentoId);

        var documentosInexistentes = idsDocumento.Where(id => !documentos.ContainsKey(id)).ToList();
        if (documentosInexistentes.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no existen: {string.Join(", ", documentosInexistentes)}." });
        }
        var documentosDeOtroCliente = documentos.Values.Where(d => d.ClienteId != recibo.ClienteId).Select(d => d.Numero).ToList();
        if (documentosDeOtroCliente.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no pertenecen al cliente indicado: {string.Join(", ", documentosDeOtroCliente)}." });
        }
        var documentosAnulados = documentos.Values.Where(d => d.Estado != "Vigente").Select(d => d.Numero).ToList();
        if (documentosAnulados.Count > 0)
        {
            return BadRequest(new { error = $"Las siguientes facturas no están vigentes: {string.Join(", ", documentosAnulados)}." });
        }

        // RN-05: varias aplicaciones a la misma factura se suman antes de comparar
        // contra el saldo (trg_limite_pago_cxc revalida en la BD como respaldo).
        var aplicadoPorDocumento = recibo.Aplicaciones.GroupBy(a => a.DocumentoId).ToDictionary(g => g.Key, g => g.Sum(a => a.MontoAplicado));
        var excedidos = aplicadoPorDocumento
            .Where(kv => kv.Value > saldos[kv.Key].SaldoPendiente)
            .Select(kv => $"{documentos[kv.Key].Numero} (saldo: {saldos[kv.Key].SaldoPendiente})")
            .ToList();
        if (excedidos.Count > 0)
        {
            return BadRequest(new { error = $"RN-05: el monto aplicado excede el saldo pendiente de: {string.Join(", ", excedidos)}." });
        }

        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        recibo.Id = 0;
        recibo.MontoTotal = recibo.Aplicaciones.Sum(a => a.MontoAplicado);
        foreach (var aplicacion in recibo.Aplicaciones)
        {
            aplicacion.Id = 0;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.RecibosPagoCliente.Add(recibo);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_pago_cxc"}, {"recibopagocliente"}, {$"Recibo {recibo.Id} (cliente {cliente.Nombre}) registrado, monto {recibo.MontoTotal}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(ListarPagos), new { }, recibo);
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
