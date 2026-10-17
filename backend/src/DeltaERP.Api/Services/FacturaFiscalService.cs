using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

public sealed record ResultadoFiscal(
    ResumenFiscal Resumen, int? CuentaIvaId, RetencionIsrPreparada? Retencion, IReadOnlyList<string> Advertencias, TipoCambioAplicable Cambio);

public class FacturaFiscalService
{
    private readonly ValidacionContable _validacion;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly RetencionService _retenciones;
    private readonly TipoCambioService _tiposCambio;

    public FacturaFiscalService(
        ValidacionContable validacion, ConfiguracionFiscalService configuracion, RetencionService retenciones, TipoCambioService tiposCambio)
    {
        _validacion = validacion;
        _configuracion = configuracion;
        _retenciones = retenciones;
        _tiposCambio = tiposCambio;
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

        var (errorOrigen, advertenciaOrigen, origen) = await ValidarOrigenAsync(documento, perfil);
        if (errorOrigen is not null)
        {
            return (null, errorOrigen);
        }
        var (cambio, errorCambio) = await ResolverCambioAsync(documento, origen);
        if (cambio is null)
        {
            return (null, errorCambio);
        }
        var advertencias = advertenciaOrigen is null ? new List<string>() : [advertenciaOrigen];
        return await CompletarAsync(documento, tercero, perfil, impuestos, cambio, advertencias);
    }

    private async Task<(ResultadoFiscal? Resultado, ErrorValidacion? Error)> CompletarAsync(
        IDocumentoFactura documento, Contraparte tercero, PerfilFactura perfil, Dictionary<int, Impuesto> impuestos,
        TipoCambioAplicable cambio, List<string> advertencias)
    {
        var resumen = MotorFiscal.Calcular(
            documento.Lineas.Select(l => new EntradaLinea(
                l.Cantidad, l.PrecioUnitario, ImpuestoRules.ComoParametro(impuestos[l.ImpuestoId]), l.TipoBienServicio)),
            cambio.Tasa);
        var errorDte = ValidarDte(documento, tercero, perfil, resumen);
        if (errorDte is not null)
        {
            return (null, errorDte);
        }
        var (cuentaIvaId, errorCuenta) = await ResolverCuentaIvaAsync(perfil, resumen);
        if (errorCuenta is not null)
        {
            return (null, errorCuenta);
        }
        return perfil.EsVenta || documento is not DocumentoCxP compra
            ? (new ResultadoFiscal(resumen, cuentaIvaId, null, advertencias, cambio), null)
            : await PrepararRetencionAsync(compra, tercero, resumen, cuentaIvaId, advertencias, cambio);
    }

    private async Task<(ResultadoFiscal? Resultado, ErrorValidacion? Error)> PrepararRetencionAsync(
        DocumentoCxP compra, Contraparte proveedor, ResumenFiscal resumen, int? cuentaIvaId, List<string> advertencias, TipoCambioAplicable cambio)
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
        return (new ResultadoFiscal(resumen, cuentaIvaId, retencion, advertencias, cambio), null);
    }

    private async Task<(TipoCambioAplicable? Cambio, ErrorValidacion? Error)> ResolverCambioAsync(IDocumentoFactura documento, IDocumentoFactura? origen)
    {
        if (origen is null)
        {
            return await _tiposCambio.ResolverParaDocumentoAsync(documento.MonedaCodigo, documento.Fecha);
        }
        TipoCambioAplicable? heredado;
        ErrorValidacion? error;
        if (origen.TipoCambioId is { } tipoCambioId)
        {
            (heredado, error) = await _tiposCambio.ResolverPorIdAsync(tipoCambioId);
        }
        else
        {
            (heredado, error) = (await _tiposCambio.CambioFuncionalAsync(documento.Fecha), null);
        }
        if (heredado is null)
        {
            return (null, error);
        }
        var pedida = documento.MonedaCodigo?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(pedida) && pedida != heredado.MonedaCodigo)
        {
            return (null, ErrorValidacion.Solicitud($"La nota de crédito usa la moneda de su factura de origen ({heredado.MonedaCodigo})."));
        }
        return (heredado, null);
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

    private async Task<(ErrorValidacion? Error, string? Advertencia, IDocumentoFactura? Origen)> ValidarOrigenAsync(
        IDocumentoFactura documento, PerfilFactura perfil)
    {
        if (documento.TipoDocumento != TiposDocumento.NotaCredito || documento.DocumentoOrigenId is not { } origenId)
        {
            return (null, null, null);
        }
        var origen = await _validacion.ObtenerDocumentoAsync(perfil.EsVenta, origenId);
        if (origen is null)
        {
            return (ErrorValidacion.Solicitud("El documento de origen no existe."), null, null);
        }
        if (origen.TerceroId != documento.TerceroId)
        {
            return (ErrorValidacion.Solicitud($"El documento de origen pertenece a otro {perfil.TipoTercero.ToLowerInvariant()}."), null, null);
        }
        if (origen.TipoDocumento != TiposDocumento.Factura || origen.Estado != "Vigente")
        {
            return (ErrorValidacion.Solicitud("El documento de origen debe ser una factura vigente."), null, null);
        }
        return (null, await AdvertirRetencionOrigenAsync(perfil, origenId), origen);
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
