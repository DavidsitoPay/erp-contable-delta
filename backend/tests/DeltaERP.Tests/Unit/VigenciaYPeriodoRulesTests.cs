using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class VigenciaYPeriodoRulesTests
{
    [Fact]
    public void MensajeMismaFecha_NombraElObjetoDeLaVersion()
    {
        Assert.Equal(
            "Ya existe una versión de ese impuesto con la misma fecha de inicio.",
            VigenciaRules.MensajeMismaFecha("impuesto"));
    }

    [Fact]
    public void Iso_FormateaLaFechaSinCulturaLocal()
    {
        Assert.Equal("2026-10-09", VigenciaRules.Iso(new DateOnly(2026, 10, 9)));
    }

    [Theory]
    [InlineData(2026, 10, null)]
    [InlineData(2026, 0, "El mes debe estar entre 1 y 12.")]
    [InlineData(2026, 13, "El mes debe estar entre 1 y 12.")]
    [InlineData(1999, 6, "El año debe estar entre 2000 y 2100.")]
    [InlineData(2101, 6, "El año debe estar entre 2000 y 2100.")]
    public void PeriodoFiscal_ValidaMesYAnio(int anio, int mes, string? esperado)
    {
        Assert.Equal(esperado, PeriodoFiscalRules.Validar(anio, mes));
    }
}
