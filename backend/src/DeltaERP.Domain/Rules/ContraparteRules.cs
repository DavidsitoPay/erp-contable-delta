using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class ContraparteRules
{
    public const string TipoProveedor = "Proveedor";
    private const int LongitudMaximaFundamento = 200;

    // tipo es el de la contraparte guardada: el Tipo del cuerpo de una actualización se ignora.
    public static string? ValidarFiscal(Contraparte datos, string tipo) =>
        ValidarRegimenes(datos) ?? ValidarFundamento(datos) ?? ValidarAgente(datos) ?? ValidarNit(tipo, datos.Nit);

    // RN-29
    public static string? ValidarTipoRentaNr(Contraparte datos, string tipo) =>
        tipo == TipoProveedor && !datos.EsResidente && datos.RegimenIsr != Contraparte.RegimenIsrExento && string.IsNullOrWhiteSpace(datos.TipoRentaNrCodigo)
            ? "Un proveedor no residente exige tipo de renta (art. 104) o régimen de ISR EXENTO con fundamento."
            : null;

    public static string? TipoRentaNrNormalizado(Contraparte datos, string tipo) =>
        tipo == TipoProveedor && !datos.EsResidente && !string.IsNullOrWhiteSpace(datos.TipoRentaNrCodigo)
            ? datos.TipoRentaNrCodigo.Trim().ToUpperInvariant()
            : null;

    public static string? FundamentoNormalizado(Contraparte datos) =>
        RequiereFundamento(datos.RegimenIsr) ? datos.IsrFundamento?.Trim() : null;

    public static string? TipoAgenteNormalizado(Contraparte datos) =>
        datos.EsAgenteRetencionIva ? datos.TipoAgenteIva : null;

    private static bool RequiereFundamento(string regimenIsr) =>
        regimenIsr is Contraparte.RegimenIsrExento or Contraparte.RegimenIsrPagoDirecto;

    private static string? ValidarRegimenes(Contraparte datos)
    {
        if (!Contraparte.RegimenesIvaValidos.Contains(datos.RegimenIva))
        {
            return $"Régimen de IVA inválido. Debe ser uno de: {string.Join(", ", Contraparte.RegimenesIvaValidos)}.";
        }
        return Contraparte.RegimenesIsrValidos.Contains(datos.RegimenIsr)
            ? null
            : $"Régimen de ISR inválido. Debe ser uno de: {string.Join(", ", Contraparte.RegimenesIsrValidos)}.";
    }

    private static string? ValidarFundamento(Contraparte datos)
    {
        if (RequiereFundamento(datos.RegimenIsr) && string.IsNullOrWhiteSpace(datos.IsrFundamento))
        {
            return "El régimen de ISR EXENTO o PAGO_DIRECTO exige el fundamento legal o el número de resolución.";
        }
        return datos.IsrFundamento?.Trim().Length > LongitudMaximaFundamento
            ? "El fundamento del régimen de ISR admite hasta 200 caracteres."
            : null;
    }

    private static string? ValidarAgente(Contraparte datos)
    {
        if (!datos.EsAgenteRetencionIva)
        {
            return null;
        }
        return datos.TipoAgenteIva is { } tipo && Contraparte.TiposAgenteIvaValidos.Contains(tipo)
            ? null
            : $"Si es agente de retención de IVA debe indicar el tipo de agente: {string.Join(", ", Contraparte.TiposAgenteIvaValidos)}.";
    }

    private static string? ValidarNit(string tipo, string? nit)
    {
        var esProveedor = tipo == TipoProveedor;
        var normalizado = NitRules.Normalizar(nit);
        if (normalizado is null)
        {
            return esProveedor ? "El NIT del proveedor es obligatorio." : null;
        }
        if (normalizado == NitRules.ConsumidorFinal && esProveedor)
        {
            return "CF solo es válido para clientes.";
        }
        return NitRules.EsValido(normalizado, true) ? null : "El NIT no es válido (dígito verificador incorrecto).";
    }
}
