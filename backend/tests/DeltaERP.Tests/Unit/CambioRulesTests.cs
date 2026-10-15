using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class CambioRulesTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly DateOnly Sabado = new(2026, 10, 3);
    private static readonly DateOnly Domingo = new(2026, 10, 4);
    private static readonly DateOnly Viernes = new(2026, 10, 2);

    [Fact]
    public void CalcularUsd_DiaHabilConFilaPropia_NoEsArrastrado()
    {
        var resultado = CambioRules.CalcularUsd(Lunes, [new CotizacionBanguat(Lunes, 7.64157m, 7.62m)]);

        Assert.Equal((7.64157m, Lunes, false, (decimal?)null), (resultado!.Tasa, resultado.FechaReferencia, resultado.Arrastrado, resultado.CotizacionVenta));
    }

    [Fact]
    public void CalcularUsd_SabadoConFilaPropia_EsArrastradoConReferenciaDelViernes()
    {
        var resultado = CambioRules.CalcularUsd(Sabado, [new CotizacionBanguat(Sabado, 7.64136m, 7.62m)]);

        Assert.Equal((7.64136m, Viernes, true), (resultado!.Tasa, resultado.FechaReferencia, resultado.Arrastrado));
    }

    [Fact]
    public void CalcularUsd_DomingoConFilaPropia_ReferenciaViernes()
    {
        var resultado = CambioRules.CalcularUsd(Domingo, [new CotizacionBanguat(Domingo, 7.64136m, 7.62m)]);

        Assert.Equal(Viernes, resultado!.FechaReferencia);
    }

    [Fact]
    public void CalcularUsd_SabadoSinFilaPropia_UsaLaUltimaAnterior()
    {
        var resultado = CambioRules.CalcularUsd(Sabado, [new CotizacionBanguat(Viernes, 7.64136m, 7.62m)]);

        Assert.Equal((7.64136m, Viernes, true), (resultado!.Tasa, resultado.FechaReferencia, resultado.Arrastrado));
    }

    [Fact]
    public void CalcularUsd_FeriadoEntreSemanaSinFila_UsaLaUltimaAnteriorArrastrada()
    {
        var miercoles = new DateOnly(2026, 10, 7);
        var resultado = CambioRules.CalcularUsd(miercoles, [new CotizacionBanguat(new DateOnly(2026, 10, 6), 7.64m, 7.62m)]);

        Assert.Equal((true, new DateOnly(2026, 10, 6)), (resultado!.Arrastrado, resultado.FechaReferencia));
    }

    [Fact]
    public void CalcularUsd_SinFilasAnteriores_DevuelveNull()
    {
        Assert.Null(CambioRules.CalcularUsd(Lunes, []));
        Assert.Null(CambioRules.CalcularUsd(Lunes, [new CotizacionBanguat(Lunes.AddDays(1), 7.64m, 7.62m)]));
    }

    [Fact]
    public void CalcularEur_DiaHabil_MultiplicaVentaPorLaReferenciaUsd()
    {
        var dia = new DateOnly(2026, 10, 9);
        var referencia = new TasaCalculada(7.64157m, dia, false, null, null, null);

        var resultado = CambioRules.CalcularEur(dia, [new CotizacionBanguat(dia, 1.1197m, 1.1196m)], referencia);

        Assert.Equal(
            (8.556266m, false, 1.1197m, 1.1196m, 7.64157m, dia),
            (resultado!.Tasa, resultado.Arrastrado, resultado.CotizacionVenta, resultado.CotizacionCompra, resultado.ReferenciaUsd, resultado.FechaReferencia));
    }

    [Fact]
    public void CalcularEur_Sabado_UsaUltimaFilaEurYMarcaArrastrado()
    {
        var sabado = new DateOnly(2026, 10, 10);
        var viernes = new DateOnly(2026, 10, 9);
        var referencia = new TasaCalculada(7.64157m, viernes, true, null, null, null);

        var resultado = CambioRules.CalcularEur(sabado, [new CotizacionBanguat(viernes, 1.1197m, 1.1196m)], referencia);

        Assert.Equal((8.556266m, true, viernes), (resultado!.Tasa, resultado.Arrastrado, resultado.FechaReferencia));
    }

    [Fact]
    public void CalcularEur_SinFilas_DevuelveNull()
    {
        var referencia = new TasaCalculada(7.64157m, Lunes, false, null, null, null);

        Assert.Null(CambioRules.CalcularEur(Lunes, [], referencia));
    }

    [Fact]
    public void CalcularEur_ReferenciaUsdArrastrada_MarcaArrastradoAunqueLaFilaEurSeaDelDia()
    {
        var referencia = new TasaCalculada(7.64157m, Viernes, true, null, null, null);

        var resultado = CambioRules.CalcularEur(Viernes, [new CotizacionBanguat(Viernes, 1.1197m, 1.1196m)], referencia);

        Assert.True(resultado!.Arrastrado);
    }

    [Theory]
    [InlineData(0, "x")]
    [InlineData(-1, "x")]
    [InlineData(1.1234567, "x")]
    [InlineData(100000, "x")]
    public void ValidarTasaManual_TasaInvalida_DevuelveError(double tasa, string motivo)
    {
        Assert.Equal(
            "La tasa de cambio debe ser mayor a cero, con hasta 6 decimales.",
            CambioRules.ValidarTasaManual((decimal)tasa, motivo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ValidarTasaManual_SinMotivo_DevuelveError(string? motivo)
    {
        Assert.Equal("El motivo es obligatorio.", CambioRules.ValidarTasaManual(7.5m, motivo));
    }

    [Fact]
    public void ValidarTasaManual_MotivoLargo_DevuelveError()
    {
        Assert.Equal("El motivo no puede exceder 300 caracteres.", CambioRules.ValidarTasaManual(7.5m, new string('a', 301)));
    }

    [Theory]
    [InlineData(7.65, "Banguat sin servicio")]
    [InlineData(99999.999999, "aaaa")]
    public void ValidarTasaManual_DatosValidos_DevuelveNull(double tasa, string motivo)
    {
        Assert.Null(CambioRules.ValidarTasaManual((decimal)tasa, motivo));
    }

    [Fact]
    public void EsFechaFutura_ComparaContraHoyGuatemala()
    {
        var hoy = new DateOnly(2026, 10, 13);

        Assert.Equal((true, false, false), (CambioRules.EsFechaFutura(hoy.AddDays(1), hoy), CambioRules.EsFechaFutura(hoy, hoy), CambioRules.EsFechaFutura(hoy.AddDays(-1), hoy)));
    }

    [Fact]
    public void HoyGuatemala_RestaSeisHoras()
    {
        Assert.Equal(
            (new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13)),
            (CambioRules.HoyGuatemala(new DateTimeOffset(2026, 10, 13, 3, 0, 0, TimeSpan.Zero)),
             CambioRules.HoyGuatemala(new DateTimeOffset(2026, 10, 13, 6, 0, 0, TimeSpan.Zero))));
    }

    [Fact]
    public void ConvertirAGtq_RedondeaAwayFromZero()
    {
        Assert.Equal((764.16m, 0.01m), (CambioRules.ConvertirAGtq(100.00m, 7.64157m), CambioRules.ConvertirAGtq(0.005m, 1m)));
    }

    [Fact]
    public void FormatoTasa_SeisDecimalesConPunto()
    {
        Assert.Equal("7.600000", CambioRules.FormatoTasa(7.6m));
    }
}
