using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

public sealed record ResultadoFiscal(
    ResumenFiscal Resumen, int? CuentaIvaId, RetencionIsrPreparada? Retencion, IReadOnlyList<string> Advertencias);

public class FacturaFiscalService
{
    private readonly ValidacionContable _validacion;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly RetencionService _retenciones;

    public FacturaFiscalService(ValidacionContable validacion, ConfiguracionFiscalService configuracion, RetencionService retenciones)
    {
        _validacion = validacion;
        _configuracion = configuracion;
        _retenciones = retenciones;
    }

    public async Task<(ResultadoFiscal? Resultado, ErrorValidacion? Error)> PrepararAsync(
        IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil)
    {
        var (uuid, errorUuid) = DteRules.InterpretarUuid(documento.DteUuidTexto);
        if (errorUuid is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorUuid));
        }
        documento.DteUuid = uuid;

        var (impuestos, errorImpuestos) = await CargarImpuestosAsync(documento, perfil);
        var errorRegimen = errorImpuestos
            ?? ErrorValidacion.SolicitudSi(ImpuestoRules.ValidarConsistenciaRegimen(impuestos.Values, tercero));
        if (errorRegimen is not null)
        {
            return (null, errorRegimen);
        }

        var resumen = MotorFiscal.Calcular(documento.Lineas.Select(l => new EntradaLinea(
            l.Cantidad, l.PrecioUnitario, ImpuestoRules.ComoParametro(impuestos[l.ImpuestoId]), l.TipoBienServicio)));
        var error = ValidarDte(documento, tercero, perfil, resumen);
        var (errorOrigen, advertenciaOrigen) = await ValidarOrigenAsync(documento, perfil);
        error ??= errorOrigen;
        if (error is not null)
        {
            return (null, error);
        }

        var (cuentaIvaId, errorCuenta) = await ResolverCuentaIvaAsync(perfil, resumen);
        if (errorCuenta is not null)
        {
            return (null, errorCuenta);
        }
        var advertencias = advertenciaOrigen is null ? new List<string>() : [advertenciaOrigen];
        return perfil.EsVenta || documento is not DocumentoCxP compra
            ? (new ResultadoFiscal(resumen, cuentaIvaId, null, advertencias), null)
            : await PrepararRetencionAsync(compra, tercero, resumen, cuentaIvaId, advertencias);
    }

    private async Task<(ResultadoFiscal? Resultado, ErrorValidacion? Error)> PrepararRetencionAsync(
        DocumentoCxP compra, Contraparte proveedor, ResumenFiscal resumen, int? cuentaIvaId, List<string> advertencias)
    {
        var (retencion, error) = await _retenciones.PrepararIsrAsync(compra, proveedor, resumen);
        if (error is not null)
        {
            return (null, error);
        }
        compra.RevisionRetencion = retencion.Revision;
        compra.RevisionMotivo = retencion.RevisionMotivo;
        if ((await _configuracion.ObtenerAsync()).AgenteRetencionIva)
        {
            advertencias.Add(RetencionService.AvisoAgenteIva);
        }
        advertencias.AddRange(retencion.Advertencias);
        return (new ResultadoFiscal(resumen, cuentaIvaId, retencion, advertencias), null);
    }

    private async Task<(Dictionary<int, Impuesto> Impuestos, ErrorValidacion? Error)> CargarImpuestosAsync(
        IDocumentoFactura documento, PerfilFactura perfil)
    {
        var impuestos = await _validacion.ObtenerImpuestosAsync(documento.Lineas);
        foreach (var id in documento.Lineas.Select(l => l.ImpuestoId).Distinct())
        {
            if (!impuestos.TryGetValue(id, out var impuesto))
            {
                return (impuestos, ErrorValidacion.Solicitud($"El impuesto {id} no existe en el catálogo."));
            }
            var mensaje = ImpuestoRules.ValidarParaFactura(impuesto, perfil.AplicaImpuestoA, perfil.Sigla, documento.Fecha);
            if (mensaje is not null)
            {
                return (impuestos, ErrorValidacion.Solicitud(mensaje));
            }
        }
        return (impuestos, null);
    }

    private static ErrorValidacion? ValidarDte(IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil, ResumenFiscal resumen)
    {
        var exigeSoporte = !perfil.DteObligatorio && resumen.IvaAcreditable > 0;
        var dte = new DatosDte(documento.DteUuid, documento.DteSerie, documento.DteNumero, documento.DteFechaCertificacion);
        var error = DteRules.Validar(dte, perfil.DteObligatorio, exigeSoporte, documento.Fecha, DateTimeOffset.UtcNow);
        if (error is null && exigeSoporte && NitRules.Normalizar(tercero.Nit) is null)
        {
            error = "El proveedor no tiene NIT registrado; es necesario para tomar crédito fiscal.";
        }
        return ErrorValidacion.SolicitudSi(error);
    }

    private async Task<(ErrorValidacion? Error, string? Advertencia)> ValidarOrigenAsync(IDocumentoFactura documento, PerfilFactura perfil)
    {
        if (documento.TipoDocumento != TiposDocumento.NotaCredito || documento.DocumentoOrigenId is not { } origenId)
        {
            return (null, null);
        }
        var origen = await _validacion.ObtenerDocumentoAsync(perfil.EsVenta, origenId);
        if (origen is null)
        {
            return (ErrorValidacion.Solicitud("El documento de origen no existe."), null);
        }
        if (origen.TerceroId != documento.TerceroId)
        {
            return (ErrorValidacion.Solicitud($"El documento de origen pertenece a otro {perfil.TipoTercero.ToLowerInvariant()}."), null);
        }
        if (origen.TipoDocumento != TiposDocumento.Factura || origen.Estado != "Vigente")
        {
            return (ErrorValidacion.Solicitud("El documento de origen debe ser una factura vigente."), null);
        }
        return (null, await AdvertirRetencionOrigenAsync(perfil, origenId));
    }

    private async Task<string?> AdvertirRetencionOrigenAsync(PerfilFactura perfil, int origenId)
    {
        if (perfil.EsVenta)
        {
            return null;
        }
        var retenido = await _validacion.ObtenerRetencionIsrAsync(origenId);
        return retenido is null
            ? null
            : $"La factura de origen tiene retención de ISR registrada ({RetencionRules.Quetzales(retenido.MontoRetenido)}); no se recalcula automáticamente.";
    }

    private async Task<(int? CuentaIvaId, ErrorValidacion? Error)> ResolverCuentaIvaAsync(PerfilFactura perfil, ResumenFiscal resumen)
    {
        var iva = perfil.EsVenta ? resumen.Iva : resumen.IvaAcreditable;
        if (iva <= 0)
        {
            return (null, null);
        }
        var configuracion = await _configuracion.ObtenerAsync();
        var cuentaId = perfil.EsVenta ? configuracion.CuentaIvaDebitoId : configuracion.CuentaIvaCreditoId;
        return await _configuracion.ResolverCuentaAsync(cuentaId, perfil.EtiquetaCuentaIva, "facturas con IVA");
    }
}
