using System.Security.Claims;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AsientosController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public AsientosController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Registrar([FromBody] AsientoContable asiento)
    {
        // RN-01
        if (!PartidaDobleValidator.EsValido(asiento, out var error))
        {
            return BadRequest(new { error });
        }

        // Valida existencia/estado activo/cuenta-hoja en un solo pase para dar un
        // 400 claro en vez de un 500 por FK violation.
        var idsCuentaUsados = asiento.Lineas.Select(l => l.CuentaId).Distinct().ToList();
        var cuentasUsadas = await _db.CuentasContables
            .Where(c => idsCuentaUsados.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        var idsConHijos = (await _db.CuentasContables
            .Where(c => c.CuentaPadreId != null && idsCuentaUsados.Contains(c.CuentaPadreId!.Value))
            .Select(c => c.CuentaPadreId!.Value)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var cuentasInexistentes = new List<string>();
        var cuentasInactivas = new List<string>();
        var cuentasDeMayorAfectadas = new List<string>();
        foreach (var cuentaId in idsCuentaUsados)
        {
            if (!cuentasUsadas.TryGetValue(cuentaId, out var cuenta))
            {
                cuentasInexistentes.Add(cuentaId.ToString());
                continue;
            }
            if (!cuenta.Activa)
            {
                cuentasInactivas.Add(cuenta.Codigo);
            }
            if (idsConHijos.Contains(cuentaId))
            {
                cuentasDeMayorAfectadas.Add(cuenta.Codigo);
            }
        }
        if (cuentasInexistentes.Count > 0)
        {
            return BadRequest(new
            {
                error = $"Las siguientes cuentas no existen: {string.Join(", ", cuentasInexistentes)}."
            });
        }
        if (cuentasInactivas.Count > 0)
        {
            return BadRequest(new
            {
                error = $"Las siguientes cuentas están inactivas y no pueden recibir movimientos: {string.Join(", ", cuentasInactivas)}."
            });
        }
        if (cuentasDeMayorAfectadas.Count > 0)
        {
            return BadRequest(new
            {
                error = $"Las siguientes cuentas son de mayor (tienen subcuentas) y no pueden recibir movimientos directos: {string.Join(", ", cuentasDeMayorAfectadas)}."
            });
        }

        // Mismo criterio que cuentaId: evitar un 500 por FK violation en centroCostoId.
        var idsCentroCostoUsados = asiento.Lineas
            .Where(l => l.CentroCostoId is not null)
            .Select(l => l.CentroCostoId!.Value)
            .Distinct()
            .ToList();
        if (idsCentroCostoUsados.Count > 0)
        {
            var idsCentroCostoExistentes = (await _db.CentrosCosto
                .Where(c => idsCentroCostoUsados.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync())
                .ToHashSet();
            var centrosCostoInexistentes = idsCentroCostoUsados
                .Where(id => !idsCentroCostoExistentes.Contains(id))
                .ToList();
            if (centrosCostoInexistentes.Count > 0)
            {
                return BadRequest(new
                {
                    error = $"Los siguientes centros de costo no existen: {string.Join(", ", centrosCostoInexistentes)}."
                });
            }
        }

        // RN-02: pre-chequeo amigable; trg_bloquear_periodo_cerrado_linea ya lo
        // garantiza a nivel de base de datos como respaldo.
        var periodo = await _db.PeriodosContables.FindAsync(asiento.PeriodoId);
        if (periodo is null)
        {
            return BadRequest(new { error = "El periodo indicado no existe." });
        }
        if (periodo.Estado != PeriodoContable.EstadoAbierto)
        {
            return BadRequest(new { error = $"El periodo '{periodo.Nombre}' está en estado '{periodo.Estado}'; no se pueden registrar asientos en un periodo que no esté Abierto." });
        }

        // El usuarioId se resuelve del token, no del body: un cliente no puede
        // registrar un asiento a nombre de otro usuario (RN-08, RN-09).
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        asiento.UsuarioId = usuarioId;

        if (asiento.Estado != "Confirmado")
        {
            return BadRequest(new { error = "El estado de un asiento nuevo debe ser 'Confirmado'; no existe un flujo de borrador." });
        }
        asiento.Estado = "Confirmado";

        // Monto recalculado en servidor; no se confía en el valor del cliente.
        asiento.Monto = asiento.Lineas.Sum(l => l.Debito);

        // RN-08: alta del asiento y registro de auditoría deben ser atómicos, por eso
        // BeginTransactionAsync en vez de dejar que SaveChangesAsync use su propia
        // transacción implícita. No se atrapa la excepción: si el CALL falla, debe
        // fallar todo el request.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.AsientosContables.Add(asiento);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_asiento"}, {"asientocontable"}, {$"Asiento {asiento.Numero} (id {asiento.Id}) registrado en periodo {asiento.PeriodoId}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(Registrar), new { id = asiento.Id }, asiento);
    }
}
