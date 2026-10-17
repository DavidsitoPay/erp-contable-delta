namespace DeltaERP.Domain.Rules;

public static class DiaHabilRules
{
    public const int DiaHabilLimite = 10;
    public const int DiasAlerta = 5;
    public const string AlDia = "AL_DIA";
    public const string PorVencer = "POR_VENCER";
    public const string Vencido = "VENCIDO";
    public const string SinSaldo = "SIN_SALDO";

    // Las filas de medio dia cuentan como habiles: no entran en el conjunto.
    public static IReadOnlySet<DateOnly> Inhabiles(IEnumerable<(DateOnly Fecha, bool MedioDia)> feriados) =>
        feriados.Where(f => !f.MedioDia).Select(f => f.Fecha).ToHashSet();

    // RN-33
    public static DateOnly DecimoDiaHabil(DateOnly periodo, IReadOnlySet<DateOnly> inhabiles)
    {
        var dia = new DateOnly(periodo.Year, periodo.Month, 1).AddMonths(1);
        var contados = 0;
        while (true)
        {
            if (EsHabil(dia, inhabiles) && ++contados == DiaHabilLimite)
            {
                return dia;
            }
            dia = dia.AddDays(1);
        }
    }

    private static bool EsHabil(DateOnly dia, IReadOnlySet<DateOnly> inhabiles) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !inhabiles.Contains(dia);

    public static int DiasParaVencer(DateOnly hoy, DateOnly vence) => vence.DayNumber - hoy.DayNumber;

    public static string EstadoVencimiento(DateOnly hoy, DateOnly vence, decimal saldo)
    {
        if (saldo <= 0m)
        {
            return SinSaldo;
        }
        var dias = DiasParaVencer(hoy, vence);
        if (dias < 0)
        {
            return Vencido;
        }
        return dias <= DiasAlerta ? PorVencer : AlDia;
    }
}
