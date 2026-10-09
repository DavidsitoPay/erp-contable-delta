using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ReportesEsquemaTests
{
    private const string SaldoAcumulado = "SELECT saldo FROM fn_reporte_saldos($1, true) WHERE cuenta_id = $2";
    private const string SaldoDelPeriodo = "SELECT saldo FROM fn_reporte_saldos($1, false) WHERE cuenta_id = $2";
    private readonly PostgresFixture _pg;

    public ReportesEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task FnReporteSaldos_ExisteYEsStable()
    {
        var volatilidad = await _pg.Data.ScalarAsync<string>(
            "SELECT provolatile::text FROM pg_proc WHERE proname = $1", "fn_reporte_saldos");

        Assert.Equal("s", volatilidad);
    }

    [Fact]
    public async Task FnReporteSaldos_AcumuladoFalseSoloElPeriodoYTrueTodosLosAnteriores()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var activo = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var periodos = await _pg.Data.CrearPeriodosHistoricosAsync(2);
        await _pg.Data.SembrarAsientoAsync(periodos[0].Id, usuario, activo, capital, 100m, periodos[0].Inicio.AddDays(1));
        await _pg.Data.SembrarAsientoAsync(periodos[1].Id, usuario, activo, capital, 40m, periodos[1].Inicio.AddDays(1));

        var delPeriodo = await _pg.Data.ScalarAsync<decimal>(SaldoDelPeriodo, periodos[1].Id, activo);
        var acumulado = await _pg.Data.ScalarAsync<decimal>(SaldoAcumulado, periodos[1].Id, activo);

        Assert.Equal(40m, delPeriodo);
        Assert.Equal(140m, acumulado);
    }

    [Fact]
    public async Task FnReporteSaldos_ConPeriodoInexistente_NoDevuelveFilas()
    {
        var filas = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM fn_reporte_saldos($1, true)", 2_000_000_000);

        Assert.Equal(0, filas);
    }
}
