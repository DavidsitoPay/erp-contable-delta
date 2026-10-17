using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
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
    private readonly AuditoriaService _auditoria;
    private readonly RetencionService _retenciones;

    public ContrapartesController(DeltaErpDbContext db, AuditoriaService auditoria, RetencionService retenciones)
    {
        _db = db;
        _auditoria = auditoria;
        _retenciones = retenciones;
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

        var errorRenta = await ValidarRentaNrAsync(contraparte, contraparte.Tipo);
        if (errorRenta is not null)
        {
            return BadRequest(new { error = errorRenta });
        }

        contraparte.Id = 0;
        contraparte.Nit = NitRules.Normalizar(contraparte.Nit);
        contraparte.IsrFundamento = ContraparteRules.FundamentoNormalizado(contraparte);
        contraparte.TipoAgenteIva = ContraparteRules.TipoAgenteNormalizado(contraparte);
        contraparte.TipoRentaNrCodigo = ContraparteRules.TipoRentaNrNormalizado(contraparte, contraparte.Tipo);
        contraparte.Pais = ContraparteRules.PaisNormalizado(contraparte);
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

        var errorRenta = await ValidarRentaNrAsync(cambios, contraparte.Tipo);
        if (errorRenta is not null)
        {
            return BadRequest(new { error = errorRenta });
        }

        // Tipo no se actualiza: cambiarlo rompería referencias ya creadas en CxC/CxP.
        var anterior = DescribirFiscal(contraparte);
        contraparte.Nombre = cambios.Nombre;
        contraparte.Nit = NitRules.Normalizar(cambios.Nit);
        contraparte.Direccion = cambios.Direccion;
        contraparte.RegimenIva = cambios.RegimenIva;
        contraparte.RegimenIsr = cambios.RegimenIsr;
        contraparte.IsrFundamento = ContraparteRules.FundamentoNormalizado(cambios);
        contraparte.EsResidente = cambios.EsResidente;
        contraparte.EsAgenteRetencionIva = cambios.EsAgenteRetencionIva;
        contraparte.TipoAgenteIva = ContraparteRules.TipoAgenteNormalizado(cambios);
        contraparte.TipoRentaNrCodigo = ContraparteRules.TipoRentaNrNormalizado(cambios, contraparte.Tipo);
        contraparte.Pais = ContraparteRules.PaisNormalizado(cambios);
        var conflicto = await this.GuardarAuditadoAsync(_auditoria, "actualizar_contraparte", "contraparte", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Contraparte {contraparte.Nombre} (id {id}) actualizada. Antes: {anterior}. Después: {DescribirFiscal(contraparte)}";
        });
        return conflicto ?? Ok(contraparte);
    }

    private async Task<string?> ValidarRentaNrAsync(Contraparte datos, string tipo)
    {
        datos.TipoRentaNrCodigo = ContraparteRules.TipoRentaNrNormalizado(datos, tipo);
        if (datos.TipoRentaNrCodigo is null)
        {
            return ContraparteRules.ValidarTipoRentaNr(datos, tipo);
        }
        return await _retenciones.ValidarTipoRentaNrAsync(datos);
    }

    private static string DescribirFiscal(Contraparte c) =>
        $"régimen ISR {c.RegimenIsr} (fundamento {c.IsrFundamento}), residente {c.EsResidente}, agente IVA {c.EsAgenteRetencionIva} ({c.TipoAgenteIva}), tipo de renta NR {c.TipoRentaNrCodigo}, país {c.Pais}";
}
