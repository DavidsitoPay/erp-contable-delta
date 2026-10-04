using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// Catálogo compartido de clientes y proveedores (M4/M5) — ver
/// docs/data-dictionary.md sección 2. Sin "desactivar": Contraparte no tiene
/// columna "activa" en el modelo de datos entregado, así que una vez creada
/// solo se editan sus datos de contacto (Nombre/Nit/Direccion), nunca el Tipo.
/// </summary>
[ApiController]
[Route("api/contrapartes")]
[Authorize]
public class ContrapartesController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public ContrapartesController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? tipo)
    {
        var query = _db.Contrapartes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            query = query.Where(c => c.Tipo == tipo);
        }
        return Ok(await query.OrderBy(c => c.Nombre).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var contraparte = await _db.Contrapartes.FindAsync(id);
        return contraparte is null ? NotFound() : Ok(contraparte);
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Crear([FromBody] Contraparte contraparte)
    {
        if (!Contraparte.TiposValidos.Contains(contraparte.Tipo))
        {
            return BadRequest(new { error = $"Tipo inválido. Debe ser uno de: {string.Join(", ", Contraparte.TiposValidos)}." });
        }
        if (string.IsNullOrWhiteSpace(contraparte.Nombre))
        {
            return BadRequest(new { error = "El nombre es obligatorio." });
        }

        contraparte.Id = 0;
        _db.Contrapartes.Add(contraparte);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obtener), new { id = contraparte.Id }, contraparte);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] Contraparte cambios)
    {
        var contraparte = await _db.Contrapartes.FindAsync(id);
        if (contraparte is null)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(cambios.Nombre))
        {
            return BadRequest(new { error = "El nombre es obligatorio." });
        }

        // El tipo no se permite editar: ya pudo haberse usado en documentocxc/cxp
        // (Cliente) o documentocxp (Proveedor) — cambiarlo dejaría esos documentos
        // apuntando a una contraparte de un tipo distinto al que dice su propio módulo.
        contraparte.Nombre = cambios.Nombre;
        contraparte.Nit = cambios.Nit;
        contraparte.Direccion = cambios.Direccion;
        await _db.SaveChangesAsync();
        return Ok(contraparte);
    }
}
