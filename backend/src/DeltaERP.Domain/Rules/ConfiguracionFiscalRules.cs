using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record DatosConfiguracionFiscal(
    string? NitEmpresa,
    string? NombreLegal,
    string? RegimenIsr,
    bool AgenteRetencionIva,
    string? TipoAgenteIva,
    int? CuentaIvaDebitoId,
    int? CuentaIvaCreditoId,
    int? CuentaIsrRetenidoPagarId = null,
    int? CuentaIvaRetenidoClientesId = null,
    int? CuentaDifGananciaRealizadaId = null,
    int? CuentaDifPerdidaRealizadaId = null,
    int? CuentaRevalGananciaId = null,
    int? CuentaRevalPerdidaId = null);

public static class ConfiguracionFiscalRules
{
    private const int LongitudMaximaNit = 30;
    private const int LongitudMaximaNombre = 200;

    public static string? TipoAgenteNormalizado(DatosConfiguracionFiscal datos) =>
        datos.AgenteRetencionIva ? datos.TipoAgenteIva : null;

    public static string? Validar(DatosConfiguracionFiscal datos) =>
        ValidarRegimen(datos) ?? ValidarAgente(datos) ?? ValidarCuentasDistintas(datos) ?? ValidarCuentasRetencionDistintas(datos)
        ?? ValidarCuentasCambiariasDistintas(datos) ?? ValidarIdentificacion(datos);

    private static string? ValidarRegimen(DatosConfiguracionFiscal datos) =>
        datos.RegimenIsr is null || !ConfiguracionFiscal.RegimenesIsrValidos.Contains(datos.RegimenIsr)
            ? $"Régimen de ISR inválido. Debe ser uno de: {string.Join(", ", ConfiguracionFiscal.RegimenesIsrValidos)}."
            : null;

    private static string? ValidarAgente(DatosConfiguracionFiscal datos)
    {
        if (!datos.AgenteRetencionIva)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(datos.TipoAgenteIva))
        {
            return "Si la empresa es agente de retención de IVA debe indicar el tipo de agente.";
        }
        return ConfiguracionFiscal.TiposAgenteIvaValidos.Contains(datos.TipoAgenteIva)
            ? null
            : $"Tipo de agente inválido. Debe ser uno de: {string.Join(", ", ConfiguracionFiscal.TiposAgenteIvaValidos)}.";
    }

    private static string? ValidarCuentasDistintas(DatosConfiguracionFiscal datos) =>
        datos.CuentaIvaDebitoId is { } debito && debito == datos.CuentaIvaCreditoId
            ? "Las cuentas de IVA débito y crédito deben ser distintas."
            : null;

    private static string? ValidarCuentasRetencionDistintas(DatosConfiguracionFiscal datos) =>
        TodasDistintas(datos.CuentaIvaDebitoId, datos.CuentaIvaCreditoId, datos.CuentaIsrRetenidoPagarId, datos.CuentaIvaRetenidoClientesId)
            ? null
            : "Las cuentas de IVA y de retención (ISR retenido por pagar, IVA retenido por clientes) deben ser distintas entre sí.";

    private static string? ValidarCuentasCambiariasDistintas(DatosConfiguracionFiscal datos) =>
        TodasDistintas(
            datos.CuentaIvaDebitoId, datos.CuentaIvaCreditoId, datos.CuentaIsrRetenidoPagarId, datos.CuentaIvaRetenidoClientesId,
            datos.CuentaDifGananciaRealizadaId, datos.CuentaDifPerdidaRealizadaId, datos.CuentaRevalGananciaId, datos.CuentaRevalPerdidaId)
            ? null
            : "Las cuentas de diferencial cambiario y de revaluación deben ser distintas entre sí y de las cuentas de IVA y de retención.";

    private static bool TodasDistintas(params int?[] cuentas)
    {
        var presentes = cuentas.OfType<int>().ToList();
        return presentes.Distinct().Count() == presentes.Count;
    }

    private static string? ValidarIdentificacion(DatosConfiguracionFiscal datos)
    {
        if (datos.NitEmpresa?.Length > LongitudMaximaNit || datos.NombreLegal?.Length > LongitudMaximaNombre)
        {
            return "El NIT admite hasta 30 caracteres y el nombre legal hasta 200.";
        }
        return !string.IsNullOrWhiteSpace(datos.NitEmpresa) && !NitRules.EsValido(datos.NitEmpresa, false)
            ? "El NIT de la empresa no es válido."
            : null;
    }
}
