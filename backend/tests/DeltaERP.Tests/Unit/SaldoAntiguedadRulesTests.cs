using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class SaldoAntiguedadRulesTests
{
    private static readonly DateOnly Corte = new(2026, 10, 31);

    [Theory]
    [InlineData(-5, "NO_VENCIDO")]
    [InlineData(0, "NO_VENCIDO")]
    [InlineData(1, "1-30")]
    [InlineData(30, "1-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(90, "61-90")]
    [InlineData(91, "91+")]
    [InlineData(400, "91+")]
    public void Tramo_ClasificaPorDiasDeAtraso(int diasDeAtraso, string tramoEsperado)
    {
        var vencimiento = Corte.AddDays(-diasDeAtraso);

        Assert.Equal(tramoEsperado, SaldoAntiguedadRules.Tramo(vencimiento, Corte));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(45)]
    public void DiasVencido_NuncaEsNegativo(int diasDeAtraso)
    {
        var vencimiento = Corte.AddDays(-diasDeAtraso);

        Assert.True(SaldoAntiguedadRules.DiasVencido(vencimiento, Corte) >= 0);
    }
}
