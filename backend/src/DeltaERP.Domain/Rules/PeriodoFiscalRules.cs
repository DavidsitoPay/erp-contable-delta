namespace DeltaERP.Domain.Rules;

public static class PeriodoFiscalRules
{
    public static string? Validar(int anio, int mes)
    {
        if (mes is < 1 or > 12)
        {
            return "El mes debe estar entre 1 y 12.";
        }
        return anio is < 2000 or > 2100 ? "El año debe estar entre 2000 y 2100." : null;
    }
}
