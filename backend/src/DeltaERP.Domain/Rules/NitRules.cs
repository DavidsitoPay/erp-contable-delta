namespace DeltaERP.Domain.Rules;

public static class NitRules
{
    public const string ConsumidorFinal = "CF";
    private const int LongitudMaximaCuerpo = 12;

    public static string? Normalizar(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit))
        {
            return null;
        }
        var limpio = new string(nit.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (limpio is ConsumidorFinal or "C/F")
        {
            return ConsumidorFinal;
        }
        var sinGuion = limpio.Replace("-", string.Empty);
        return sinGuion.Length < 2 ? sinGuion : $"{sinGuion[..^1]}-{sinGuion[^1]}";
    }

    public static bool EsValido(string? nit, bool permiteConsumidorFinal)
    {
        var normalizado = Normalizar(nit);
        if (normalizado is null)
        {
            return false;
        }
        if (normalizado == ConsumidorFinal)
        {
            return permiteConsumidorFinal;
        }

        var partes = normalizado.Split('-');
        return partes.Length == 2
            && partes[0].Length is > 0 and <= LongitudMaximaCuerpo
            && partes[0].All(char.IsAsciiDigit)
            && partes[1] == CalcularDigito(partes[0]);
    }

    // Módulo 11: factor n+1 decreciente hasta 2; resto 10 se escribe "K".
    public static string CalcularDigito(string cuerpo)
    {
        var suma = 0;
        var factor = cuerpo.Length + 1;
        foreach (var digito in cuerpo)
        {
            suma += (digito - '0') * factor;
            factor--;
        }
        var verificador = (11 - (suma % 11)) % 11;
        return verificador == 10 ? "K" : verificador.ToString();
    }
}
