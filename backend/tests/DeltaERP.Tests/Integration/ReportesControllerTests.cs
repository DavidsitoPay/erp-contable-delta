using System.Net;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ReportesControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private const string BalanceGeneral = "balance-general";
    private const string EstadoResultados = "estado-resultados";
    private readonly PostgresFixture _pg;

    public ReportesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static async Task<JsonElement> ConsultarAsync(HttpClient client, string reporte, int periodoId)
    {
        var response = await client.GetAsync($"/api/reportes/{reporte}?periodoId={periodoId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.LeerJsonAsync();
    }

    private async Task CerrarAsync(int usuarioId, int periodoId)
    {
        var response = await _pg.CreateApiClient(usuarioId).PostAsync($"/api/periodos/{periodoId}/cerrar", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task ReabrirAsync(int adminId, int periodoId)
    {
        var response = await _pg.CreateApiClient(adminId).PostAsync($"/api/periodos/{periodoId}/reabrir", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<int> SembrarAsync(PeriodoSembrado periodo, int usuarioId, int debito, int credito, decimal monto, string estado = "Confirmado") =>
        _pg.Data.SembrarAsientoAsync(periodo.Id, usuarioId, debito, credito, monto, periodo.Inicio.AddDays(10), new OpcionesAsiento(Estado: estado));

    private async Task SembrarMovimientosBaseAsync(PeriodoSembrado periodo, CuentasReporte c)
    {
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 1000m);
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Pasivo, 300m);
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Ingreso, 500m);
        await SembrarAsync(periodo, c.Usuario, c.Gasto, c.Activo, 200m);
    }

    private static JsonElement Fila(JsonElement seccion, int cuentaId) =>
        seccion.GetProperty("cuentas").EnumerateArray().Single(f => f.GetProperty("cuentaId").GetInt32() == cuentaId);

    private static decimal Saldo(JsonElement seccion, int cuentaId) => Fila(seccion, cuentaId).GetProperty("saldo").GetDecimal();

    private static decimal Monto(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetDecimal();

    private static HashSet<int> IdsCuentas(JsonElement seccion) =>
        seccion.GetProperty("cuentas").EnumerateArray().Select(f => f.GetProperty("cuentaId").GetInt32()).ToHashSet();

    private static void AssertBalanceCuadrado(JsonElement balance)
    {
        Assert.Equal(0m, Monto(balance, "diferencia"));
        Assert.True(balance.GetProperty("cuadra").GetBoolean());
    }

    [Fact]
    public async Task TC13_BalanceGeneral_ClasificaPorTipoYUsaElCierreEnPeriodoCerrado()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        await SembrarMovimientosBaseAsync(periodo, c);
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 999m, "Borrador");
        await CerrarAsync(c.Usuario, periodo.Id);

        var balance = await ConsultarAsync(_pg.CreateApiClient(c.Usuario), BalanceGeneral, periodo.Id);

        Assert.Equal("Cierre", balance.GetProperty("fuente").GetString());
        Assert.Equal(1, balance.GetProperty("periodo").GetProperty("cierres").GetInt32());
        Assert.False(balance.GetProperty("incluyePeriodosAbiertos").GetBoolean());
        var activo = balance.GetProperty("activo");
        var pasivo = balance.GetProperty("pasivo");
        var capital = balance.GetProperty("capital");
        Assert.Equal(1600m, Saldo(activo, c.Activo));
        Assert.Equal(300m, Saldo(pasivo, c.Pasivo));
        Assert.Equal(1000m, Saldo(capital, c.Capital));
        Assert.Equal(1600m, Monto(activo, "total"));
        Assert.Equal(300m, Monto(pasivo, "total"));
        Assert.Equal(1000m, Monto(capital, "total"));
        Assert.Equal(300m, Monto(balance, "resultadoEjercicio"));
        Assert.Equal(1300m, Monto(balance, "totalCapital"));
        Assert.Equal(1600m, Monto(balance, "totalPasivoCapital"));
        AssertBalanceCuadrado(balance);

        var enBalance = IdsCuentas(activo).Concat(IdsCuentas(pasivo)).Concat(IdsCuentas(capital)).ToHashSet();
        Assert.DoesNotContain(c.Ingreso, enBalance);
        Assert.DoesNotContain(c.Gasto, enBalance);
        Assert.DoesNotContain(c.Pasivo, IdsCuentas(activo));
        var codigos = activo.GetProperty("cuentas").EnumerateArray().Select(f => f.GetProperty("codigo").GetString()).ToArray();
        Assert.Equal(codigos.OrderBy(x => x, StringComparer.Ordinal).ToArray(), codigos);
    }

    [Fact]
    public async Task TC13_BalanceGeneral_PeriodoCerrado_NoCambiaSiSeAlteraElMovimientoEnVivo()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        var asiento = await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 100m);
        await CerrarAsync(c.Usuario, periodo.Id);
        // Manipulacion directa: ningun trigger impide volver a Borrador un asiento de un periodo cerrado.
        await _pg.Data.EjecutarAsync("UPDATE asientocontable SET estado = 'Borrador' WHERE id = $1", asiento);

        var enVivo = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(saldo), 0) FROM vw_balance_saldos WHERE cuenta_id = $1", c.Activo);
        var balance = await ConsultarAsync(_pg.CreateApiClient(c.Usuario), BalanceGeneral, periodo.Id);

        Assert.Equal(0m, enVivo);
        Assert.Equal("Cierre", balance.GetProperty("fuente").GetString());
        Assert.Equal(100m, Saldo(balance.GetProperty("activo"), c.Activo));
        Assert.Equal(100m, Saldo(balance.GetProperty("capital"), c.Capital));
        AssertBalanceCuadrado(balance);
    }

    [Fact]
    public async Task TC14_EstadoResultados_SoloIncluyeIngresosYGastosDelPeriodo()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodos = await _pg.Data.CrearPeriodosHistoricosAsync(2);
        var (anterior, actual) = (periodos[0], periodos[1]);
        await SembrarAsync(anterior, c.Usuario, c.Activo, c.Ingreso, 800m);
        await SembrarAsync(anterior, c.Usuario, c.Gasto, c.Activo, 100m);
        await SembrarAsync(actual, c.Usuario, c.Activo, c.Ingreso, 300m);
        await SembrarAsync(actual, c.Usuario, c.Gasto, c.Activo, 50m);
        await SembrarAsync(actual, c.Usuario, c.Gasto, c.Activo, 999m, "Borrador");
        await CerrarAsync(c.Usuario, anterior.Id);
        var client = _pg.CreateApiClient(c.Usuario);

        var abierto = await ConsultarAsync(client, EstadoResultados, actual.Id);

        Assert.Equal("Preliminar", abierto.GetProperty("fuente").GetString());
        Assert.Equal(actual.Inicio.ToString("yyyy-MM-dd"), abierto.GetProperty("periodo").GetProperty("fechaInicio").GetString());
        var ingresos = abierto.GetProperty("ingresos");
        var gastos = abierto.GetProperty("gastos");
        Assert.Equal(300m, Saldo(ingresos, c.Ingreso));
        Assert.Equal(300m, Monto(ingresos, "total"));
        Assert.Equal(50m, Saldo(gastos, c.Gasto));
        Assert.Equal(50m, Monto(gastos, "total"));
        Assert.Equal(250m, Monto(abierto, "utilidadNeta"));
        Assert.DoesNotContain(c.Activo, IdsCuentas(ingresos));
        Assert.DoesNotContain(c.Activo, IdsCuentas(gastos));
        Assert.DoesNotContain(c.Gasto, IdsCuentas(ingresos));

        var cerrado = await ConsultarAsync(client, EstadoResultados, anterior.Id);

        Assert.Equal("Cierre", cerrado.GetProperty("fuente").GetString());
        Assert.Equal(800m, Monto(cerrado.GetProperty("ingresos"), "total"));
        Assert.Equal(100m, Monto(cerrado.GetProperty("gastos"), "total"));
        Assert.Equal(700m, Monto(cerrado, "utilidadNeta"));
    }

    [Fact]
    public async Task EstadoResultados_ConGastosMayoresQueIngresos_UtilidadNetaNegativa()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Ingreso, 100m);
        await SembrarAsync(periodo, c.Usuario, c.Gasto, c.Activo, 250m);

        var estado = await ConsultarAsync(_pg.CreateApiClient(c.Usuario), EstadoResultados, periodo.Id);

        Assert.Equal(-150m, Monto(estado, "utilidadNeta"));
    }

    [Fact]
    public async Task BalanceGeneral_ConPeriodoAbierto_EsPreliminarExcluyeBorradoresYNetaReversas()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 200m);
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 999m, "Borrador");
        var original = await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 70m);
        await _pg.Data.ReversarAsientoAsync(original, c.Usuario);

        var balance = await ConsultarAsync(_pg.CreateApiClient(c.Usuario), BalanceGeneral, periodo.Id);

        Assert.Equal("Preliminar", balance.GetProperty("fuente").GetString());
        Assert.Equal(0, balance.GetProperty("periodo").GetProperty("cierres").GetInt32());
        Assert.False(balance.GetProperty("incluyePeriodosAbiertos").GetBoolean());
        Assert.Equal(200m, Saldo(balance.GetProperty("activo"), c.Activo));
        Assert.Equal(200m, Saldo(balance.GetProperty("capital"), c.Capital));
        AssertBalanceCuadrado(balance);
    }

    [Fact]
    public async Task BalanceGeneral_AcumulaPeriodosCerradosYAbiertos_YAvisaCuandoHayAbiertosAnteriores()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodos = await _pg.Data.CrearPeriodosHistoricosAsync(3);
        var (p1, p2, p3) = (periodos[0], periodos[1], periodos[2]);
        await SembrarAsync(p1, c.Usuario, c.Activo, c.Capital, 1000m);
        await SembrarAsync(p1, c.Usuario, c.Activo, c.Ingreso, 400m);
        await SembrarAsync(p2, c.Usuario, c.Gasto, c.Activo, 150m);
        await SembrarAsync(p2, c.Usuario, c.Activo, c.Capital, 999m, "Borrador");
        await CerrarAsync(c.Usuario, p1.Id);
        var client = _pg.CreateApiClient(c.Usuario);

        var primero = await ConsultarAsync(client, BalanceGeneral, p1.Id);
        var segundo = await ConsultarAsync(client, BalanceGeneral, p2.Id);
        var tercero = await ConsultarAsync(client, BalanceGeneral, p3.Id);

        Assert.Equal("Cierre", primero.GetProperty("fuente").GetString());
        Assert.False(primero.GetProperty("incluyePeriodosAbiertos").GetBoolean());
        Assert.Equal(1400m, Saldo(primero.GetProperty("activo"), c.Activo));
        Assert.Equal(400m, Monto(primero, "resultadoEjercicio"));
        AssertBalanceCuadrado(primero);

        Assert.Equal("Preliminar", segundo.GetProperty("fuente").GetString());
        Assert.False(segundo.GetProperty("incluyePeriodosAbiertos").GetBoolean());
        Assert.Equal(1250m, Saldo(segundo.GetProperty("activo"), c.Activo));
        Assert.Equal(250m, Monto(segundo, "resultadoEjercicio"));
        AssertBalanceCuadrado(segundo);

        Assert.True(tercero.GetProperty("incluyePeriodosAbiertos").GetBoolean());
        Assert.Equal(1250m, Monto(tercero.GetProperty("activo"), "total"));
        Assert.Equal(1250m, Monto(tercero, "totalCapital"));
        AssertBalanceCuadrado(tercero);
    }

    [Fact]
    public async Task BalanceGeneral_ConPeriodoReabiertoYRecerrado_UsaElCierreVigente()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var admin = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 100m);
        await CerrarAsync(c.Usuario, periodo.Id);
        await ReabrirAsync(admin, periodo.Id);
        await SembrarAsync(periodo, c.Usuario, c.Activo, c.Capital, 50m);
        var client = _pg.CreateApiClient(c.Usuario);

        var reabierto = await ConsultarAsync(client, BalanceGeneral, periodo.Id);

        Assert.Equal("Preliminar", reabierto.GetProperty("fuente").GetString());
        Assert.Equal(1, reabierto.GetProperty("periodo").GetProperty("cierres").GetInt32());
        Assert.Equal(150m, Saldo(reabierto.GetProperty("activo"), c.Activo));

        await CerrarAsync(c.Usuario, periodo.Id);
        var recerrado = await ConsultarAsync(client, BalanceGeneral, periodo.Id);

        Assert.Equal("Cierre", recerrado.GetProperty("fuente").GetString());
        Assert.Equal(2, recerrado.GetProperty("periodo").GetProperty("cierres").GetInt32());
        Assert.Equal(150m, Saldo(recerrado.GetProperty("activo"), c.Activo));
        Assert.Equal(150m, Saldo(recerrado.GetProperty("capital"), c.Capital));
        AssertBalanceCuadrado(recerrado);
    }

    [Fact]
    public async Task BalanceGeneral_ConSubcuentas_AcumulaEnElPadreSinDobleConteoEnElTotal()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var hijoA = await _pg.Data.CrearSubcuentaAsync(c.Activo, "Activo", "Deudora");
        var hijoB = await _pg.Data.CrearSubcuentaAsync(c.Activo, "Activo", "Deudora");
        var periodo = (await _pg.Data.CrearPeriodosHistoricosAsync(1))[0];
        await SembrarAsync(periodo, c.Usuario, hijoA, c.Capital, 100m);
        await SembrarAsync(periodo, c.Usuario, hijoB, c.Capital, 50m);

        var balance = await ConsultarAsync(_pg.CreateApiClient(c.Usuario), BalanceGeneral, periodo.Id);

        var activo = balance.GetProperty("activo");
        var padre = Fila(activo, c.Activo);
        Assert.Equal(150m, padre.GetProperty("saldo").GetDecimal());
        Assert.Equal(1, padre.GetProperty("nivel").GetInt32());
        Assert.False(padre.GetProperty("esHoja").GetBoolean());
        var filaHijo = Fila(activo, hijoA);
        Assert.Equal(100m, filaHijo.GetProperty("saldo").GetDecimal());
        Assert.Equal(2, filaHijo.GetProperty("nivel").GetInt32());
        Assert.True(filaHijo.GetProperty("esHoja").GetBoolean());
        Assert.Equal(150m, Monto(activo, "total"));
        AssertBalanceCuadrado(balance);
    }

    [Theory]
    [InlineData(BalanceGeneral)]
    [InlineData(EstadoResultados)]
    public async Task Reporte_ConPeriodoInexistenteOSinPeriodo_Responde400(string reporte)
    {
        var client = _pg.CreateApiClient(0);

        var inexistente = await client.GetAsync($"/api/reportes/{reporte}?periodoId={IdInexistente}");
        var sinParametro = await client.GetAsync($"/api/reportes/{reporte}");

        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Contains("no existe", await inexistente.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinParametro.StatusCode);
    }

    [Theory]
    [InlineData(BalanceGeneral)]
    [InlineData(EstadoResultados)]
    public async Task Reporte_SinToken_Responde401(string reporte)
    {
        var response = await _pg.Factory.CreateClient().GetAsync($"/api/reportes/{reporte}?periodoId=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(BalanceGeneral, Roles.Vendedor)]
    [InlineData(BalanceGeneral, Roles.Tecnico)]
    [InlineData(EstadoResultados, Roles.Vendedor)]
    [InlineData(EstadoResultados, Roles.Tecnico)]
    public async Task Reporte_ConRolSinPermiso_Responde403(string reporte, string rol)
    {
        var response = await _pg.CreateApiClient(0, rol).GetAsync($"/api/reportes/{reporte}?periodoId={IdInexistente}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reportes_SonDeSoloLectura_NoModificanPeriodosNiAsientos()
    {
        var c = await _pg.Data.SembrarCuentasReporteAsync();
        var periodos = await _pg.Data.CrearPeriodosHistoricosAsync(2);
        await SembrarMovimientosBaseAsync(periodos[0], c);
        await SembrarMovimientosBaseAsync(periodos[1], c);
        await CerrarAsync(c.Usuario, periodos[0].Id);
        var ids = periodos.Select(p => p.Id).ToArray();
        var antes = await FotoAsync(ids);
        var client = _pg.CreateApiClient(c.Usuario);

        foreach (var periodo in periodos)
        {
            await ConsultarAsync(client, BalanceGeneral, periodo.Id);
            await ConsultarAsync(client, EstadoResultados, periodo.Id);
        }

        Assert.Equal(antes, await FotoAsync(ids));
    }

    private async Task<(long Asientos, long Lineas, long Saldos, string Periodos)> FotoAsync(int[] periodoIds) => (
        await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE periodo_id = ANY($1)", periodoIds),
        await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento l JOIN asientocontable a ON a.id = l.asiento_id WHERE a.periodo_id = ANY($1)", periodoIds),
        await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM saldocuentaperiodo WHERE periodo_id = ANY($1)", periodoIds),
        await _pg.Data.ScalarAsync<string>("SELECT string_agg(estado || cierres::text, ',' ORDER BY id) FROM periodocontable WHERE id = ANY($1)", periodoIds));
}
