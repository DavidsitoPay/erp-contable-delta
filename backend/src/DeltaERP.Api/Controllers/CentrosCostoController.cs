using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// M1 Catálogo: centros de costo, usados para distribuir líneas de asiento
/// (LineaAsiento.CentroCostoId).
/// </summary>
[ApiController]
[Route("api/centros-costo")]
[Authorize]
public class CentrosCostoController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public CentrosCostoController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInactivos = false)
    {
        var query = _db.CentrosCosto.AsQueryable();
        if (!incluirInactivos)
        {
            query = query.Where(c => c.Activo);
        }
        return Ok(await query.OrderBy(c => c.Codigo).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var centro = await _db.CentrosCosto.FindAsync(id);
        return centro is null ? NotFound() : Ok(centro);
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Crear([FromBody] CentroCosto centro)
    {
        if (await _db.CentrosCosto.AnyAsync(c => c.Codigo == centro.Codigo))
        {
            return Conflict(new { error = $"Ya existe un centro de costo con el código {centro.Codigo}." });
        }

        centro.Id = 0;
        centro.Activo = true;
        _db.CentrosCosto.Add(centro);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Segunda red de seguridad: la comprobación AnyAsync de arriba tiene una
            // ventana de carrera bajo peticiones concurrentes; el UNIQUE en la base
            // de datos (centrocosto.codigo) es la garantía real.
            return Conflict(new { error = $"Ya existe un centro de costo con el código {centro.Codigo}." });
        }
        return CreatedAtAction(nameof(Obtener), new { id = centro.Id }, centro);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CentroCosto cambios)
    {
        var centro = await _db.CentrosCosto.FindAsync(id);
        if (centro is null)
        {
            return NotFound();
        }

        centro.Nombre = cambios.Nombre;
        await _db.SaveChangesAsync();
        return Ok(centro);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Desactivar(int id)
    {
        var centro = await _db.CentrosCosto.FindAsync(id);
        if (centro is null)
        {
            return NotFound();
        }

        centro.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
