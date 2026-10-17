using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class RevaluacionRulesTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 13);

    private static PartidaRevaluacion Cxc(int cuenta, decimal libros, decimal cierre, string referencia = "F-1") =>
        RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxC, 1, null, cuenta, referencia), 1000m, libros, 7.65m) with { ValorCierreGtq = cierre };

    [Fact]
    public void Ejemplo_CxcCxpBanco_GeneraLasLineasYCuadra()
    {
        var partidas = new[]
        {
            RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxC, 1, null, 10, "F-100"), 1000m, 7600m, 7.65m),
            RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxP, 2, null, 20, "P-50"), 500m, 3830m, 7.65m),
            RevaluacionRules.Crear(new OrigenPartida(TipoPartida.Banco, null, 3, 30, "Banco"), 2000m, 15400m, 7.65m),
        };

        var asiento = RevaluacionRules.ConstruirLineas(partidas, 40, 50);

        var lineas = asiento.Lineas.Select(l => (l.CuentaId, l.Debito, l.Credito)).ToList();
        Assert.Equal(new[] { (10, 50m, 0m), (20, 5m, 0m), (30, 0m, 100m), (40, 0m, 55m), (50, 100m, 0m) }, lineas);
        Assert.Equal((55m, 100m), (asiento.Ganancia, asiento.Perdida));
        Assert.Equal(lineas.Sum(l => l.Item2), lineas.Sum(l => l.Item3));
    }

    [Fact]
    public void Crear_CalculaElValorDeCierreConRedondeo()
    {
        var partida = RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxC, 1, null, 10, "F-100"), 1000m, 7600m, 7.65m);

        Assert.Equal((7650.00m, 50.00m), (partida.ValorCierreGtq, partida.Ajuste));
    }

    [Fact]
    public void EsGanancia_ParaCxpSeInvierte()
    {
        var cxpPositivo = RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxP, 1, null, 1, "P"), 100m, 760m, 7.70m);
        var cxpNegativo = RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxP, 1, null, 1, "P"), 100m, 760m, 7.55m);
        var cxcPositivo = Cxc(1, 760m, 765m);
        var bancoNegativo = RevaluacionRules.Crear(new OrigenPartida(TipoPartida.Banco, null, 1, 1, "B"), 100m, 765m, 7.60m);

        Assert.Equal((false, true, true, false), (cxpPositivo.EsGanancia, cxpNegativo.EsGanancia, cxcPositivo.EsGanancia, bancoNegativo.EsGanancia));
    }

    [Fact]
    public void PartidaConAjusteCero_SeIgnora()
    {
        var partidas = new[] { Cxc(10, 765m, 765m) };

        var asiento = RevaluacionRules.ConstruirLineas(partidas, 40, 50);

        Assert.Empty(asiento.Lineas);
        Assert.Equal((0m, 0m), (asiento.Ganancia, asiento.Perdida));
    }

    [Fact]
    public void VariasPartidasDeLaMismaCuenta_GeneranUnaSolaLinea()
    {
        var partidas = new[] { Cxc(10, 760m, 790m), Cxc(10, 760m, 750m) };

        var asiento = RevaluacionRules.ConstruirLineas(partidas, 40, 50);

        var linea = asiento.Lineas.Single(l => l.CuentaId == 10);
        Assert.Equal((20m, 0m), (linea.Debito, linea.Credito));
    }

    [Fact]
    public void EfectoNetoCeroEnUnaCuenta_NoGeneraLinea()
    {
        var partidas = new[] { Cxc(10, 760m, 770m), Cxc(10, 760m, 750m) };

        var asiento = RevaluacionRules.ConstruirLineas(partidas, 40, 50);

        Assert.DoesNotContain(asiento.Lineas, l => l.CuentaId == 10);
        Assert.Equal((10m, 10m), (asiento.Ganancia, asiento.Perdida));
    }

    [Fact]
    public void SoloGanancia_NoExigeCuentaDePerdida()
    {
        var partidas = new[] { Cxc(10, 760m, 770m) };

        var asiento = RevaluacionRules.ConstruirLineas(partidas, 40, null);

        Assert.Equal(10m, asiento.Ganancia);
    }

    [Fact]
    public void Perdida_SinCuenta_LanzaInvalidOperation()
    {
        var partidas = new[] { Cxc(10, 760m, 750m) };

        var ex = Assert.Throws<InvalidOperationException>(() => RevaluacionRules.ConstruirLineas(partidas, 40, null));

        Assert.Equal("Falta la cuenta de pérdida por revaluación.", ex.Message);
    }

    [Fact]
    public void Ganancia_SinCuenta_Lanza()
    {
        var partidas = new[] { Cxc(10, 760m, 770m) };

        Assert.Throws<InvalidOperationException>(() => RevaluacionRules.ConstruirLineas(partidas, null, 50));
    }

    [Fact]
    public void Invertir_IntercambiaDebitoYCredito()
    {
        var lineas = new[] { new LineaRevaluacion(10, 50m, 0m) };

        Assert.Equal(new LineaRevaluacion(10, 0m, 50m), RevaluacionRules.Invertir(lineas).Single());
    }

    [Fact]
    public void UltimoDiaDelMes_Y_PrimerDiaDelMesSiguiente()
    {
        Assert.Equal(
            (31, 29, new DateOnly(2027, 1, 1)),
            (RevaluacionRules.UltimoDiaDelMes(2026, 10).Day, RevaluacionRules.UltimoDiaDelMes(2024, 2).Day, RevaluacionRules.PrimerDiaDelMesSiguiente(2026, 12)));
    }

    [Theory]
    [InlineData(2026, 13)]
    [InlineData(0, 5)]
    [InlineData(9999, 1)]
    public void ValidarMes_FueraDeRango_EsErrorSinConflicto(int anio, int mes)
    {
        var error = RevaluacionRules.ValidarMes(anio, mes, Hoy);

        Assert.NotNull(error);
        Assert.False(error.EsConflicto);
    }

    [Fact]
    public void ValidarMes_MesSinTerminar_EsConflicto()
    {
        var error = RevaluacionRules.ValidarMes(2026, 10, Hoy);

        Assert.NotNull(error);
        Assert.Equal(
            ("El mes 10/2026 aún no termina; la revaluación se ejecuta desde su último día.", true),
            (error.Mensaje, error.EsConflicto));
    }

    [Fact]
    public void ValidarMes_MesTerminado_EsValido()
    {
        Assert.Null(RevaluacionRules.ValidarMes(2026, 10, new DateOnly(2026, 10, 31)));
        Assert.Null(RevaluacionRules.ValidarMes(2026, 9, Hoy));
    }
}
