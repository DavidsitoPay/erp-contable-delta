using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Controllers;

/// <summary>
/// M1 Catálogo: cuentas contables. Sin columna de saldo (ver
/// database/05_views.sql); "eliminar" es desactivar, porque
/// trg_prevenir_eliminacion_cuentacontable bloquea el DELETE físico.
/// </summary>
[ApiController]
[Route("api/cuentas")]
[Authorize]
public class CuentasContablesController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public CuentasContablesController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas = false)
    {
        var query = _db.CuentasContables.AsQueryable();
        if (!incluirInactivas)
        {
            query = query.Where(c => c.Activa);
        }
        return Ok(await query.OrderBy(c => c.Codigo).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var cuenta = await _db.CuentasContables.FindAsync(id);
        return cuenta is null ? NotFound() : Ok(cuenta);
    }

    [HttpPost]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Crear([FromBody] CuentaContable cuenta)
    {
        var error = await ValidarAsync(cuenta, esCreacion: true);
        if (error is not null)
        {
            return error;
        }

        cuenta.Id = 0;
        cuenta.Activa = true;
        _db.CuentasContables.Add(cuenta);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Segunda red de seguridad: la comprobación AnyAsync de arriba tiene una
            // ventana de carrera bajo peticiones concurrentes; el UNIQUE en la base
            // de datos (cuentacontable.codigo) es la garantía real.
            return Conflict(new { error = $"Ya existe una cuenta con el código {cuenta.Codigo}." });
        }
        return CreatedAtAction(nameof(Obtener), new { id = cuenta.Id }, cuenta);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CuentaContable cambios)
    {
        var cuenta = await _db.CuentasContables.FindAsync(id);
        if (cuenta is null)
        {
            return NotFound();
        }

        cambios.Id = id;
        var error = await ValidarAsync(cambios, esCreacion: false);
        if (error is not null)
        {
            return error;
        }

        // El código no se permite editar: es la clave de referencia contable
        // ya usada potencialmente en asientos; solo nombre/tipo/naturaleza/padre.
        cuenta.Nombre = cambios.Nombre;
        cuenta.Tipo = cambios.Tipo;
        cuenta.Naturaleza = cambios.Naturaleza;
        cuenta.CuentaPadreId = cambios.CuentaPadreId;
        await _db.SaveChangesAsync();
        return Ok(cuenta);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Desactivar(int id)
    {
        var cuenta = await _db.CuentasContables.FindAsync(id);
        if (cuenta is null)
        {
            return NotFound();
        }

        cuenta.Activa = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidarAsync(CuentaContable cuenta, bool esCreacion)
    {
        if (!CuentaContable.TiposValidos.Contains(cuenta.Tipo))
        {
            return BadRequest(new { error = $"Tipo inválido. Debe ser uno de: {string.Join(", ", CuentaContable.TiposValidos)}." });
        }
        if (!CuentaContable.NaturalezasValidas.Contains(cuenta.Naturaleza))
        {
            return BadRequest(new { error = $"Naturaleza inválida. Debe ser una de: {string.Join(", ", CuentaContable.NaturalezasValidas)}." });
        }
        // Convención contable: la naturaleza debe corresponder al tipo (Activo/Gasto
        // -> Deudora, Pasivo/Capital/Ingreso -> Acreedora). Una combinación inconsistente
        // invertiría el signo del saldo en vw_balance_saldos (database/05_views.sql).
        if (CuentaContable.NaturalezaEsperadaPorTipo.TryGetValue(cuenta.Tipo, out var naturalezaEsperada)
            && cuenta.Naturaleza != naturalezaEsperada)
        {
            return BadRequest(new { error = $"La naturaleza de una cuenta de tipo {cuenta.Tipo} debe ser {naturalezaEsperada}." });
        }
        if (esCreacion && await _db.CuentasContables.AnyAsync(c => c.Codigo == cuenta.Codigo))
        {
            return Conflict(new { error = $"Ya existe una cuenta con el código {cuenta.Codigo}." });
        }
        if (cuenta.CuentaPadreId == cuenta.Id && cuenta.Id != 0)
        {
            return BadRequest(new { error = "Una cuenta no puede ser su propia cuenta padre." });
        }
        if (cuenta.CuentaPadreId is not null)
        {
            var padre = await _db.CuentasContables.FindAsync(cuenta.CuentaPadreId.Value);
            if (padre is null)
            {
                return BadRequest(new { error = "La cuenta padre indicada no existe." });
            }
            if (padre.Tipo != cuenta.Tipo)
            {
                return BadRequest(new { error = $"La cuenta padre debe ser del mismo tipo ({cuenta.Tipo})." });
            }
            if (!esCreacion)
            {
                var errorCiclo = await ValidarSinCicloAsync(cuenta.Id, cuenta.CuentaPadreId.Value);
                if (errorCiclo is not null)
                {
                    return errorCiclo;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Recorre la cadena de padres a partir de <paramref name="padreIdPropuesto"/>
    /// (padre -> abuelo -> ...) para detectar si <paramref name="idCuentaEditada"/>
    /// aparece en ella, lo que la convertiría en su propio ancestro. Solo aplica en
    /// Actualizar: una cuenta recién creada no puede formar parte de un ciclo existente.
    /// </summary>
    private async Task<IActionResult?> ValidarSinCicloAsync(int idCuentaEditada, int padreIdPropuesto)
    {
        const int maxProfundidad = 50; // guarda contra un ciclo preexistente corrupto
        int? actualId = padreIdPropuesto;
        for (var i = 0; i < maxProfundidad && actualId is not null; i++)
        {
            if (actualId == idCuentaEditada)
            {
                return BadRequest(new { error = "La cuenta padre indicada generaría una jerarquía circular." });
            }
            actualId = await _db.CuentasContables
                .Where(c => c.Id == actualId)
                .Select(c => c.CuentaPadreId)
                .FirstOrDefaultAsync();
        }
        return null;
    }
}
