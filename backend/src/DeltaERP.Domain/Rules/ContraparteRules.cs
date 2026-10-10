using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class ContraparteRules
{
    public const string TipoProveedor = "Proveedor";

    // tipo es el de la contraparte guardada: el Tipo del cuerpo de una actualización se ignora.
    public static string? ValidarFiscal(Contraparte datos, string tipo) =>
        ValidarRegimenes(datos) ?? ValidarNit(tipo, datos.Nit);

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
