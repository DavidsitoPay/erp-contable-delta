using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;

namespace DeltaERP.Api.Services;

public class FacturaService
{
    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;

    public FacturaService(DeltaErpDbContext db, AuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // El asiento se guarda primero porque la factura necesita su AsientoId.
    public async Task RegistrarAsync(IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil, int usuarioId)
    {
        var sigla = perfil.Sigla.ToLowerInvariant();
        var asiento = AsientoFacturaBuilder.Construir(perfil.Sigla.ToUpperInvariant(), documento, perfil.LadoControl, usuarioId);
        FacturaRules.PrepararNuevo(documento, asiento.Monto);

        await _auditoria.EjecutarAsync(usuarioId, $"registrar_factura_{sigla}", $"documento{sigla}", async () =>
        {
            _db.AsientosContables.Add(asiento);
            await _db.SaveChangesAsync();

            documento.AsientoId = asiento.Id;
            _db.Add((object)documento);
            await _db.SaveChangesAsync();

            return $"Factura {documento.Numero} (id {documento.Id}) registrada para {perfil.TipoTercero.ToLowerInvariant()} {tercero.Nombre}, monto {asiento.Monto}";
        });
    }
}
