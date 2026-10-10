using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

// Contraparte no tiene columna "activa": no hay endpoint de desactivar, solo
// se editan datos de contacto y fiscales, nunca el Tipo.
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
        var errorFiscal = ContraparteRules.ValidarFiscal(contraparte, contraparte.Tipo);
        if (errorFiscal is not null)
        {
            return BadRequest(new { error = errorFiscal });
        }

        contraparte.Id = 0;
        contraparte.Nit = NitRules.Normalizar(contraparte.Nit);
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
        var errorFiscal = ContraparteRules.ValidarFiscal(cambios, contraparte.Tipo);
        if (errorFiscal is not null)
        {
            return BadRequest(new { error = errorFiscal });
        }

        // Tipo no se actualiza: cambiarlo rompería referencias ya creadas en CxC/CxP.
        contraparte.Nombre = cambios.Nombre;
        contraparte.Nit = NitRules.Normalizar(cambios.Nit);
        contraparte.Direccion = cambios.Direccion;
        contraparte.RegimenIva = cambios.RegimenIva;
        contraparte.RegimenIsr = cambios.RegimenIsr;
        contraparte.EsResidente = cambios.EsResidente;
        contraparte.EsAgenteRetencionIva = cambios.EsAgenteRetencionIva;
        await _db.SaveChangesAsync();
        return Ok(contraparte);
    }
}
