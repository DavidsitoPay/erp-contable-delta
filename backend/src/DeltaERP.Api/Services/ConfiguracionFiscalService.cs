using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;

namespace DeltaERP.Api.Services;

public class ConfiguracionFiscalService
{
    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;

    public ConfiguracionFiscalService(DeltaErpDbContext db, ValidacionContable validacion)
    {
        _db = db;
        _validacion = validacion;
    }

    public async Task<ConfiguracionFiscal> ObtenerAsync() =>
        await _db.ConfiguracionesFiscales.FindAsync(ConfiguracionFiscal.IdUnico)
        ?? throw new InvalidOperationException("Falta la fila única de configuración fiscal.");

    public async Task<(int? CuentaId, ErrorValidacion? Error)> ResolverCuentaAsync(int? cuentaId, string etiqueta, string contexto)
    {
        if (cuentaId is not { } id)
        {
            return (null, ErrorValidacion.Conflicto(
                $"Falta configurar la cuenta de {etiqueta} en Catálogo > Configuración fiscal. Solicite al Administrador del sistema que la configure antes de registrar {contexto}."));
        }
        var error = await _validacion.ValidarCuentasPorIdAsync([id]);
        return error is null
            ? (id, null)
            : (null, ErrorValidacion.Conflicto($"La cuenta de {etiqueta} configurada no es utilizable: {error}"));
    }
}
