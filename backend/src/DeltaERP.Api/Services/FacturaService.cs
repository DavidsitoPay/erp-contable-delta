using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Services;

public class FacturaService
{
    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly FacturaFiscalService _fiscal;

    public FacturaService(DeltaErpDbContext db, AuditoriaService auditoria, FacturaFiscalService fiscal)
    {
        _db = db;
        _auditoria = auditoria;
        _fiscal = fiscal;
    }

    // El asiento se guarda primero porque la factura necesita su AsientoId.
    public async Task<ErrorValidacion?> RegistrarAsync(IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil, int usuarioId)
    {
        var (fiscal, error) = await _fiscal.PrepararAsync(documento, tercero, perfil);
        if (fiscal is null)
        {
            return error;
        }

        var sigla = perfil.Sigla.ToLowerInvariant();
        FacturaRules.PrepararNuevo(documento, fiscal.Resumen);
        var asiento = AsientoFacturaBuilder.Construir(
            perfil.Sigla.ToUpperInvariant(), documento, fiscal.Resumen, perfil.LadoControl, usuarioId, fiscal.CuentaIvaId);

        try
        {
            await _auditoria.EjecutarAsync(usuarioId, $"registrar_factura_{sigla}", $"documento{sigla}", async () =>
            {
                _db.AsientosContables.Add(asiento);
                await _db.SaveChangesAsync();

                documento.AsientoId = asiento.Id;
                _db.Add((object)documento);
                await _db.SaveChangesAsync();

                return $"Factura {documento.Numero} (id {documento.Id}) registrada para {perfil.TipoTercero.ToLowerInvariant()} {tercero.Nombre}, monto {asiento.Monto}, base {documento.MontoBase}, IVA {documento.MontoIva}";
            });
            return null;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException
            && ErroresPostgres.ObtenerPostgres(ex) is { SqlState: "55000" } conflicto)
        {
            return ErrorValidacion.Conflicto(conflicto.MessageText);
        }
        catch (DbUpdateException ex) when (MensajeDteDuplicado(ex, documento) is { } duplicado)
        {
            return ErrorValidacion.Conflicto(duplicado);
        }
    }

    private static string? MensajeDteDuplicado(DbUpdateException ex, IDocumentoFactura documento) =>
        ex.InnerException is PostgresException { SqlState: "23505" } violacion
            ? violacion.ConstraintName switch
            {
                "ux_documentocxc_dte_uuid" or "ux_documentocxp_dte_uuid" =>
                    $"Ya existe una factura con el UUID de DTE {documento.DteUuid}.",
                "ux_documentocxc_dte_serie_numero" =>
                    $"Ya existe una factura de venta con el DTE serie {documento.DteSerie} número {documento.DteNumero}.",
                "ux_documentocxp_proveedor_dte_serie_numero" =>
                    $"El proveedor ya tiene registrado el DTE serie {documento.DteSerie} número {documento.DteNumero}.",
                _ => null,
            }
            : null;
}
