using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class DiaHabilRulesTests
{
    private static HashSet<DateOnly> Set(string csv) =>
        csv.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(DateOnly.Parse).ToHashSet();

    [Theory]
    [InlineData("2026-09-01", "", "2026-10-14")]
    [InlineData("2026-05-01", "", "2026-06-12")]
    [InlineData("2026-09-01", "2026-10-05", "2026-10-15")]
    [InlineData("2026-09-01", "2026-10-03", "2026-10-14")]
    [InlineData("2026-12-01", "", "2027-01-14")]
    [InlineData("2026-12-01", "2027-01-01", "2027-01-15")]
    public void DecimoDiaHabil_CuentaLunesAViernesNoInhabiles(string periodo, string inhabiles, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado), DiaHabilRules.DecimoDiaHabil(DateOnly.Parse(periodo), Set(inhabiles)));

    [Fact]
    public void Inhabiles_ExcluyeLosMediosDias()
    {
        var inhabiles = DiaHabilRules.Inhabiles(new[] { (new DateOnly(2026, 6, 29), false), (new DateOnly(2026, 12, 24), true) });

        Assert.Single(inhabiles, new DateOnly(2026, 6, 29));
    }

    [Theory]
    [InlineData("2026-10-09", 100, "POR_VENCER")]
    [InlineData("2026-10-14", 100, "POR_VENCER")]
    [InlineData("2026-10-08", 100, "AL_DIA")]
    [InlineData("2026-10-15", 100, "VENCIDO")]
    [InlineData("2026-10-15", 0, "SIN_SALDO")]
    public void EstadoVencimiento_AlertaACincoDiasCalendario(string hoy, int saldo, string esperado) =>
        Assert.Equal(esperado, DiaHabilRules.EstadoVencimiento(DateOnly.Parse(hoy), new DateOnly(2026, 10, 14), saldo));

    [Fact]
    public void DiasParaVencer_EsNegativoDespuesDelVencimiento() =>
        Assert.Equal(-1, DiaHabilRules.DiasParaVencer(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14)));
}
