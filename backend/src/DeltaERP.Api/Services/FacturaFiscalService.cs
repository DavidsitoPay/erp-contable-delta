using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public sealed record ResultadoFiscal(ResumenFiscal Resumen, int? CuentaIvaId);

public class FacturaFiscalService
{
    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly ConfiguracionFiscalService _configuracion;

    public FacturaFiscalService(DeltaErpDbContext db, ValidacionContable validacion, ConfiguracionFiscalService configuracion)
    {
        _db = db;
        _validacion = validacion;
        _configuracion = configuracion;
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
        var error = ValidarDte(documento, tercero, perfil, resumen)
            ?? await ValidarOrigenAsync(documento, perfil);
        if (error is not null)
        {
            return (null, error);
        }

        var (cuentaIvaId, errorCuenta) = await ResolverCuentaIvaAsync(perfil, resumen);
        return errorCuenta is null ? (new ResultadoFiscal(resumen, cuentaIvaId), null) : (null, errorCuenta);
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

    private async Task<ErrorValidacion?> ValidarOrigenAsync(IDocumentoFactura documento, PerfilFactura perfil)
    {
        if (documento.TipoDocumento != TiposDocumento.NotaCredito || documento.DocumentoOrigenId is not { } origenId)
        {
            return null;
        }
        var origen = await _validacion.ObtenerDocumentoAsync(perfil.EsVenta, origenId);
        if (origen is null)
        {
            return ErrorValidacion.Solicitud("El documento de origen no existe.");
        }
        if (origen.TerceroId != documento.TerceroId)
        {
            return ErrorValidacion.Solicitud($"El documento de origen pertenece a otro {perfil.TipoTercero.ToLowerInvariant()}.");
        }
        if (origen.TipoDocumento != TiposDocumento.Factura || origen.Estado != "Vigente")
        {
            return ErrorValidacion.Solicitud("El documento de origen debe ser una factura vigente.");
        }
        return null;
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
        if (cuentaId is null)
        {
            return (null, ErrorValidacion.Conflicto(
                $"Falta configurar la cuenta de {perfil.EtiquetaCuentaIva} en Catálogo > Configuración fiscal. Solicite al Administrador del sistema que la configure antes de registrar facturas con IVA."));
        }
        var error = await _validacion.ValidarCuentasPorIdAsync([cuentaId.Value]);
        return error is null
            ? (cuentaId, null)
            : (null, ErrorValidacion.Conflicto($"La cuenta de {perfil.EtiquetaCuentaIva} configurada no es utilizable: {error}"));
    }
}
