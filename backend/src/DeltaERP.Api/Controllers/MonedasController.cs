using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/monedas")]
[Authorize]
public class MonedasController : ControllerBase
{
    private readonly DeltaErpDbContext _db;

    public MonedasController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.Monedas.AsNoTracking()
            .Where(m => m.Activa)
            .OrderByDescending(m => m.EsFuncional).ThenBy(m => m.Codigo)
            .Select(m => new { m.Id, m.Codigo, m.Nombre, m.EsFuncional, m.Activa })
            .ToListAsync());
}
