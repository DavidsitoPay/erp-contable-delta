using System.Globalization;

namespace DeltaERP.Domain.Rules;

public static class VigenciaRules
{
    public static bool EstaVigente(DateOnly desde, DateOnly? hasta, DateOnly fecha) =>
        fecha >= desde && (hasta is null || fecha <= hasta);

    public static string Iso(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string MensajeMismaFecha(string objeto) => $"Ya existe una versión de ese {objeto} con la misma fecha de inicio.";

    public static string? Validar(DateOnly? desde, DateOnly? hasta)
    {
        if (desde is null)
        {
            return "La vigencia inicial es obligatoria.";
        }
        return hasta < desde ? "La vigencia final no puede ser anterior a la inicial." : null;
    }
}
