using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/cuentas-bancarias")]
[Authorize(Roles = Roles.GestionTesoreria)]
public class CuentasBancariasController : ControllerBase
{
    private const string UniqueBancoNumero = "uq_cuentabancaria_banco_numero";
    private const string UniqueCuentaContable = "uq_cuentabancaria_cuenta_contable";

    private readonly DeltaErpDbContext _db;
    private readonly TesoreriaService _tesoreria;

    public CuentasBancariasController(DeltaErpDbContext db, TesoreriaService tesoreria)
    {
        _db = db;
        _tesoreria = tesoreria;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas = false) =>
        Ok(await Proyectar(_db.CuentasBancarias.Where(c => incluirInactivas || c.Activa)).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var cuenta = await Proyectar(_db.CuentasBancarias.Where(c => c.Id == id)).FirstOrDefaultAsync();
        return cuenta is null ? NotFound() : Ok(cuenta);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CuentaBancariaRequest solicitud)
    {
        try
        {
            var (cuenta, error) = await _tesoreria.CrearCuentaAsync(solicitud, this.UsuarioId());
            if (error is not null)
            {
                return BadRequest(new { error });
            }

            var cuentaId = cuenta!.Id;
            var dto = await Proyectar(_db.CuentasBancarias.Where(c => c.Id == cuentaId)).FirstAsync();
            return CreatedAtAction(nameof(Obtener), new { id = dto.Id }, dto);
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, UniqueBancoNumero))
        {
            return ConflictoBancoNumero(solicitud);
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, UniqueCuentaContable))
        {
            return Conflict(new { error = "La cuenta contable ya está asociada a otra cuenta bancaria." });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CuentaBancariaRequest cambios)
    {
        var cuenta = await _db.CuentasBancarias.FindAsync(id);
        if (cuenta is null)
        {
            return NotFound();
        }
        var error = TesoreriaRules.ValidarCuentaBancaria(cambios.Banco, cambios.Numero, cambios.Tipo);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        cuenta.Banco = cambios.Banco!.Trim();
        cuenta.Numero = cambios.Numero!.Trim();
        cuenta.Tipo = cambios.Tipo!;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, UniqueBancoNumero))
        {
            return ConflictoBancoNumero(cambios);
        }
        return Ok(await Proyectar(_db.CuentasBancarias.Where(c => c.Id == id)).FirstAsync());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var cuenta = await _db.CuentasBancarias.FindAsync(id);
        if (cuenta is null)
        {
            return NotFound();
        }

        cuenta.Activa = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<CuentaBancariaDto> Proyectar(IQueryable<CuentaBancaria> cuentas) =>
        from c in cuentas
        join cc in _db.CuentasContables on c.CuentaContableId equals cc.Id
        join s in _db.SaldosCuentaBancaria on c.Id equals s.CuentaBancariaId
        orderby c.Banco, c.Numero
        select new CuentaBancariaDto(
            c.Id, c.Banco, c.Numero, c.Tipo, c.Activa, c.CuentaContableId, cc.Codigo, cc.Nombre, c.SaldoApertura, c.FechaApertura, s.Saldo);

    private ConflictObjectResult ConflictoBancoNumero(CuentaBancariaRequest solicitud) =>
        Conflict(new { error = $"Ya existe la cuenta bancaria {solicitud.Banco!.Trim()} {solicitud.Numero!.Trim()}." });
}
