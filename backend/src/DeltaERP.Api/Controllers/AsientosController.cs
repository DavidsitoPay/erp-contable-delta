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

    /// <summary>
    /// Registra un asiento contable. Aplica RN-01 (partida doble) antes de guardar,
    /// y RN-08 (bitácora de auditoría) al confirmar. RN-01 se valida aquí en la API
    /// para dar un 400 claro al cliente, pero YA NO es la única red de seguridad:
    /// trg_validar_partida_doble (database/03_triggers.sql, AFTER INSERT OR UPDATE
    /// DEFERRABLE INITIALLY DEFERRED) revalida a nivel de base de datos como respaldo
    /// defense-in-depth. RN-09 (control de acceso por perfil) se aplica con el
    /// [Authorize(Roles = ...)] de abajo, igual que en los demás controladores de
    /// mutación. Ver docs/architecture.md.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Registrar([FromBody] AsientoContable asiento)
    {
        // RN-01
        if (!PartidaDobleValidator.EsValido(asiento, out var error))
        {
            return BadRequest(new { error });
        }

        // Un solo pase sobre las cuentas distintas usadas en las líneas cubre tres
        // validaciones a la vez (una FK inexistente en el body es BadRequest, no un
        // 500 crudo — mismo criterio que "la cuenta padre indicada no existe" en
        // CuentasContablesController):
        //   1) la cuentaId debe existir (si no, FK violation -> PostgresException/500);
        //   2) la cuenta debe estar activa (no se puede contabilizar sobre una cuenta
        //      desactivada);
        //   3) solo cuentas "hoja" (sin subcuentas) pueden recibir movimientos: una
        //      cuenta de mayor/resumen (con hijos) nunca debe recibir un abono/cargo
        //      directo.
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

        // Mismo criterio que arriba, pero para centroCostoId (FK opcional en cada línea):
        // un id inexistente debe dar un 400 claro, no un 500 crudo por FK violation.
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

        // RN-02: pre-chequeo amigable en la API. trg_bloquear_periodo_cerrado_linea
        // (database/03_triggers.sql, fn_bloquear_periodo_cerrado) YA bloquea a nivel
        // de base de datos el INSERT en lineaasiento si el periodo está cerrado — esto
        // no sustituye esa red de seguridad, solo evita que el cliente reciba un
        // PostgresException/500 crudo cuando podemos rechazar la petición antes con un
        // 400 claro. Igual que "la cuenta padre indicada no existe" en
        // CuentasContablesController, una FK inexistente en el body es BadRequest, no NotFound.
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

        // El estado de un asiento nuevo solo puede ser "Confirmado": no existe un
        // flujo de borrador en la aplicación, así que cualquier otro valor enviado
        // por el cliente se rechaza explícitamente en lugar de aceptarse en silencio.
        if (asiento.Estado != "Confirmado")
        {
            return BadRequest(new { error = "El estado de un asiento nuevo debe ser 'Confirmado'; no existe un flujo de borrador." });
        }
        asiento.Estado = "Confirmado"; // se ignora cualquier valor enviado por el cliente.

        // El monto se recalcula en el servidor a partir de las líneas: nunca se
        // confía en el valor enviado por el cliente. PartidaDobleValidator ya
        // garantizó arriba que la suma de débitos == suma de créditos.
        asiento.Monto = asiento.Lineas.Sum(l => l.Debito);

        // RN-08: el registro de auditoría debe quedar en la MISMA transacción que el
        // alta del asiento (ver el comentario sobre sp_registrar_auditoria en
        // database/04_procedures.sql: "de modo que si esta se revierte, el registro de
        // auditoría también"). SaveChangesAsync por sí solo abre su propia transacción
        // implícita por llamada; con BeginTransactionAsync forzamos que el INSERT de
        // AsientoContable/LineaAsiento y el CALL a sp_registrar_auditoria vivan o mueran
        // juntos. No se atrapa ninguna excepción aquí a propósito: si el CALL falla, todo
        // el request debe fallar (RN-08) y el `await using` revierte la transacción al
        // salir por la excepción.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.AsientosContables.Add(asiento);
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"CALL sp_registrar_auditoria({usuarioId}, {"registrar_asiento"}, {"asientocontable"}, {$"Asiento {asiento.Numero} (id {asiento.Id}) registrado en periodo {asiento.PeriodoId}"})");

        await transaction.CommitAsync();

        return CreatedAtAction(nameof(Registrar), new { id = asiento.Id }, asiento);
    }
}
