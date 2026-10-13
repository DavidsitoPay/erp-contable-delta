using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Api.Services;

public sealed record ResultadoRegistro(
    ErrorValidacion? Error,
    RetencionIsrPreparada? Retencion,
    int? RetencionId,
    IReadOnlyList<string> Advertencias)
{
    public static ResultadoRegistro Fallo(ErrorValidacion error) => new(error, null, null, []);
}

public class FacturaService
{
    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly FacturaFiscalService _fiscal;
    private readonly RetencionService _retenciones;

    public FacturaService(DeltaErpDbContext db, AuditoriaService auditoria, FacturaFiscalService fiscal, RetencionService retenciones)
    {
        _db = db;
        _auditoria = auditoria;
        _fiscal = fiscal;
        _retenciones = retenciones;
    }

    // El asiento se guarda primero porque la factura necesita su AsientoId.
    public async Task<ResultadoRegistro> RegistrarAsync(IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil, int usuarioId)
    {
        var (fiscal, error) = await _fiscal.PrepararAsync(documento, tercero, perfil);
        if (fiscal is null)
        {
            return ResultadoRegistro.Fallo(error ?? ErrorValidacion.Solicitud("No se pudo preparar la factura."));
        }

        var sigla = perfil.Sigla.ToLowerInvariant();
        FacturaRules.PrepararNuevo(documento, fiscal.Resumen);
        var asiento = AsientoFacturaBuilder.Construir(
            perfil.Sigla.ToUpperInvariant(), documento, fiscal.Resumen, perfil.LadoControl, usuarioId, fiscal.CuentaIvaId,
            fiscal.Retencion?.ParaAsiento);

        Retencion? registrada = null;
        try
        {
            await _auditoria.EjecutarAsync(usuarioId, $"registrar_factura_{sigla}", $"documento{sigla}", async () =>
            {
                _db.AsientosContables.Add(asiento);
                await _db.SaveChangesAsync();

                documento.AsientoId = asiento.Id;
                _db.Add((object)documento);
                await _db.SaveChangesAsync();

                registrada = await _retenciones.RegistrarIsrAsync(documento, fiscal.Retencion, asiento.Id, usuarioId);

                return $"Factura {documento.Numero} (id {documento.Id}) registrada para {perfil.TipoTercero.ToLowerInvariant()} {tercero.Nombre}, monto {asiento.Monto}, base {documento.MontoBase}, IVA {documento.MontoIva}{DescribirRetencion(fiscal.Retencion)}";
            });
            return new ResultadoRegistro(null, fiscal.Retencion, registrada?.Id, fiscal.Advertencias);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException && ErroresPostgres.MensajeConflicto(ex) is { } conflicto)
        {
            return ResultadoRegistro.Fallo(ErrorValidacion.Conflicto(conflicto));
        }
        catch (DbUpdateException ex) when (MensajeDteDuplicado(ex, documento) is { } duplicado)
        {
            return ResultadoRegistro.Fallo(ErrorValidacion.Conflicto(duplicado));
        }
    }

    private static string DescribirRetencion(RetencionIsrPreparada? retencion)
    {
        if (retencion is null)
        {
            return string.Empty;
        }
        var detalle = retencion.Monto > 0
            ? $", retención ISR Q{retencion.Monto} (base {retencion.Base}, tasa {retencion.Tasa}), parámetro {retencion.Parametro?.Codigo}"
            : $", sin retención ({retencion.Motivo ?? retencion.MotivoAjuste})";
        var motivo = retencion.MotivoAjuste ?? retencion.RevisionMotivo;
        return motivo is null
            ? detalle
            : $"{detalle}, calculado Q{retencion.MontoCalculado}, aplicado Q{retencion.Monto}, motivo: {motivo}";
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
