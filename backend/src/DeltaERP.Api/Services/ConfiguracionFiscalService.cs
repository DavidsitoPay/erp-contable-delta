using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;

namespace DeltaERP.Api.Services;

public class ConfiguracionFiscalService
{
    private readonly DeltaErpDbContext _db;

    public ConfiguracionFiscalService(DeltaErpDbContext db)
    {
        _db = db;
    }

    public async Task<ConfiguracionFiscal> ObtenerAsync() =>
        await _db.ConfiguracionesFiscales.FindAsync(ConfiguracionFiscal.IdUnico)
        ?? throw new InvalidOperationException("Falta la fila única de configuración fiscal.");
}
