namespace DeltaERP.Domain.Rules;

public static class SaldoAntiguedadRules
{
    public const string NoVencido = "NO_VENCIDO";
    public static readonly string[] Tramos = { NoVencido, "1-30", "31-60", "61-90", "91+" };

    public static int DiasVencido(DateOnly fechaVencimiento, DateOnly corte) => Math.Max(0, corte.DayNumber - fechaVencimiento.DayNumber);

    public static string Tramo(DateOnly fechaVencimiento, DateOnly corte) => DiasVencido(fechaVencimiento, corte) switch
    {
        0 => Tramos[0],
        <= 30 => Tramos[1],
        <= 60 => Tramos[2],
        <= 90 => Tramos[3],
        _ => Tramos[4],
    };
}
