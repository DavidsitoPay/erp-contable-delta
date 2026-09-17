using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsientosController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public AsientosController(DeltaErpDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Registra un asiento contable. Aplica RN-01 (partida doble) antes de guardar,
    /// y RN-08 (bitácora de auditoría) al confirmar — ambas responsabilidad de la API,
    /// no de triggers de base de datos. Ver docs/architecture.md.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] AsientoContable asiento)
    {
        // RN-01
        if (!PartidaDobleValidator.EsValido(asiento, out var error))
        {
            return BadRequest(new { error });
        }

        // TODO: RN-02 — validar que asiento.PeriodoId corresponda a un periodo abierto.
        // TODO: obtener usuario_id del token JWT (M8) en vez de confiar en el body.

        _db.AsientosContables.Add(asiento);
        await _db.SaveChangesAsync();

        // TODO RN-08: insertar registro en BitacoraAuditoria dentro de la misma transacción,
        // usando el usuario_id resuelto desde el token, no un trigger de base de datos.

        return CreatedAtAction(nameof(Registrar), new { id = asiento.Id }, asiento);
    }
}
