using System.Net;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class LibrosControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;
    private static readonly decimal[] SaldosAcumuladosMovimientos = { 100m, 70m, 75m };
    private static readonly decimal[] SaldosAcumuladosFiltro = { 100m, 70m };
    private static readonly decimal[] SaldosAcumuladosConsolidado = { 100m, 150m };

    public LibrosControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task BalanceSaldos_SinToken_Responde401()
    {
        var client = _pg.Factory.CreateClient();
        var response = await client.GetAsync("/api/libros/balance-saldos");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BalanceSaldos_ConAsientosConfirmados_ExponeTotalesYSaldoPorNaturaleza()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var d = e.CuentaCxC;
        var k = e.CuentaIngreso;

        await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, d, k, 100m, new DateOnly(2025, 3, 1));
        await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, k, d, 30m, new DateOnly(2025, 3, 2));

        var client = _pg.CreateApiClient(e.UsuarioId);
        var response = await client.GetAsync("/api/libros/balance-saldos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.LeerJsonAsync();
        var elemD = body.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == d);
        Assert.Equal(100m, elemD.GetProperty("totalDebito").GetDecimal());
        Assert.Equal(30m, elemD.GetProperty("totalCredito").GetDecimal());
        Assert.Equal(70m, elemD.GetProperty("saldo").GetDecimal());

        var elemK = body.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == k);
        Assert.Equal(30m, elemK.GetProperty("totalDebito").GetDecimal());
        Assert.Equal(100m, elemK.GetProperty("totalCredito").GetDecimal());
        Assert.Equal(70m, elemK.GetProperty("saldo").GetDecimal());
    }

    [Fact]
    public async Task Diario_ConPeriodoInexistente_Responde400()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.GetAsync($"/api/libros/diario?periodoId={IdInexistente}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.LeerErrorAsync();
        Assert.Contains("no existe", error);

        var response2 = await client.GetAsync("/api/libros/diario");
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task Diario_ConAsientosDelPeriodo_ListaContabilizadosOrdenadosPorFechaYExcluyeBorradores()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var periodoVacio = await _pg.Data.CrearPeriodoAsync();
        var periodo = await _pg.Data.CrearPeriodoAsync();

        var client = _pg.CreateApiClient(e.UsuarioId);
        var responseVacio = await client.GetAsync($"/api/libros/diario?periodoId={periodoVacio}");
        Assert.Equal(HttpStatusCode.OK, responseVacio.StatusCode);
        var bodyVacio = await responseVacio.LeerJsonAsync();
        Assert.Equal(0, bodyVacio.GetArrayLength());

        var numeroA = $"A-{TestData.Sufijo()}";
        var a = await _pg.Data.SembrarAsientoAsync(periodo, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 2, 1), new(Numero: numeroA, CentroCostoId: e.CentroCostoId));
        var numeroB = $"A-{TestData.Sufijo()}";
        var b = await _pg.Data.SembrarAsientoAsync(periodo, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 50m, new DateOnly(2025, 1, 15), new(Numero: numeroB));
        var borrador = await _pg.Data.SembrarAsientoAsync(periodo, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 10m, new DateOnly(2025, 1, 20), new(Estado: "Borrador"));

        var response = await client.GetAsync($"/api/libros/diario?periodoId={periodo}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(2, body.GetArrayLength());

        var ids = body.Ids();
        var idList = ids.ToList();
        Assert.Equal(b, idList[0]);
        Assert.Equal(a, idList[1]);

        var primerAsiento = body.EnumerateArray().First();
        Assert.Equal(50m, primerAsiento.GetProperty("monto").GetDecimal());
        var lineas = primerAsiento.GetProperty("lineas");
        Assert.Equal(2, lineas.GetArrayLength());
        foreach (var linea in lineas.EnumerateArray())
        {
            Assert.NotNull(linea.GetProperty("cuentaCodigo").GetString());
        }

        var asientoA = body.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == a);
        var lineasA = asientoA.GetProperty("lineas");
        foreach (var lineaA in lineasA.EnumerateArray())
        {
            Assert.Equal(e.CentroCostoId, lineaA.GetProperty("centroCostoId").GetInt32());
        }

        Assert.DoesNotContain(borrador, ids);
    }

    [Fact]
    public async Task Mayor_ConParametrosInvalidos_Responde400()
    {
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.GetAsync($"/api/libros/mayor?cuentaId={IdInexistente}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.LeerErrorAsync();
        Assert.Contains("cuenta", error);

        var response2 = await client.GetAsync($"/api/libros/mayor?cuentaId={d}&periodoId={IdInexistente}");
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        var error2 = await response2.LeerErrorAsync();
        Assert.Contains("periodo", error2);
    }

    [Fact]
    public async Task Mayor_ConCuentaDeudoraYAcreedora_CalculaSaldoAcumuladoYFiltraPorPeriodo()
    {
        var u = await _pg.Data.CrearUsuarioAsync();
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var p1 = await _pg.Data.CrearPeriodoAsync();
        var p2 = await _pg.Data.CrearPeriodoAsync();

        var a1 = await _pg.Data.SembrarAsientoAsync(p1, u, d, k, 100m, new DateOnly(2025, 1, 10));
        var a2 = await _pg.Data.SembrarAsientoAsync(p1, u, k, d, 30m, new DateOnly(2025, 1, 20));
        await _pg.Data.SembrarAsientoAsync(p1, u, d, k, 999m, new DateOnly(2025, 1, 25), new(Estado: "Borrador"));
        var a4 = await _pg.Data.SembrarAsientoAsync(p2, u, d, k, 5m, new DateOnly(2025, 2, 1));

        var client = _pg.CreateApiClient(u);

        var responseDeudora = await client.GetAsync($"/api/libros/mayor?cuentaId={d}");
        Assert.Equal(HttpStatusCode.OK, responseDeudora.StatusCode);
        var bodyDeudora = await responseDeudora.LeerJsonAsync();
        var cuenta = bodyDeudora.GetProperty("cuenta");
        Assert.Equal("Deudora", cuenta.GetProperty("naturaleza").GetString());
        Assert.Equal(d, cuenta.GetProperty("id").GetInt32());

        var movimientos = bodyDeudora.GetProperty("movimientos");
        Assert.Equal(3, movimientos.GetArrayLength());
        var movimientosArray = movimientos.EnumerateArray().ToArray();
        Assert.Equal(a1, movimientosArray[0].GetProperty("asientoId").GetInt32());
        Assert.Equal(a2, movimientosArray[1].GetProperty("asientoId").GetInt32());
        Assert.Equal(a4, movimientosArray[2].GetProperty("asientoId").GetInt32());

        var saldosDeudora = movimientos.EnumerateArray().Select(m => m.GetProperty("saldoAcumulado").GetDecimal()).ToArray();
        Assert.Equal(SaldosAcumuladosMovimientos, saldosDeudora);

        Assert.Equal(100m, movimientosArray[0].GetProperty("debito").GetDecimal());
        Assert.Equal(0m, movimientosArray[0].GetProperty("credito").GetDecimal());

        var responseAcreedora = await client.GetAsync($"/api/libros/mayor?cuentaId={k}");
        Assert.Equal(HttpStatusCode.OK, responseAcreedora.StatusCode);
        var bodyAcreedora = await responseAcreedora.LeerJsonAsync();
        var movimientosAcreedora = bodyAcreedora.GetProperty("movimientos");
        Assert.Equal(3, movimientosAcreedora.GetArrayLength());
        var saldosAcreedora = movimientosAcreedora.EnumerateArray().Select(m => m.GetProperty("saldoAcumulado").GetDecimal()).ToArray();
        Assert.Equal(SaldosAcumuladosMovimientos, saldosAcreedora);
        var primerMovAcreedora = movimientosAcreedora.EnumerateArray().First();
        Assert.Equal(0m, primerMovAcreedora.GetProperty("debito").GetDecimal());
        Assert.Equal(100m, primerMovAcreedora.GetProperty("credito").GetDecimal());

        var responseFiltro = await client.GetAsync($"/api/libros/mayor?cuentaId={d}&periodoId={p1}");
        Assert.Equal(HttpStatusCode.OK, responseFiltro.StatusCode);
        var bodyFiltro = await responseFiltro.LeerJsonAsync();
        var movimientosFiltro = bodyFiltro.GetProperty("movimientos");
        Assert.Equal(2, movimientosFiltro.GetArrayLength());
        var movimientosFiltroArray = movimientosFiltro.EnumerateArray().ToArray();
        Assert.Equal(a1, movimientosFiltroArray[0].GetProperty("asientoId").GetInt32());
        Assert.Equal(a2, movimientosFiltroArray[1].GetProperty("asientoId").GetInt32());
        var saldosFiltro = movimientosFiltro.EnumerateArray().Select(m => m.GetProperty("saldoAcumulado").GetDecimal()).ToArray();
        Assert.Equal(SaldosAcumuladosFiltro, saldosFiltro);
    }

    [Fact]
    public async Task Libros_ConAsientoReversado_OriginalYReversaCuentanYNetanCero()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"A-{TestData.Sufijo()}";
        var original = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 3, 1), new OpcionesAsiento(Numero: numero));
        var reversa = await _pg.Data.ReversarAsientoAsync(original, e.UsuarioId);
        var client = _pg.CreateApiClient(e.UsuarioId);

        var balance = await (await client.GetAsync("/api/libros/balance-saldos")).LeerJsonAsync();
        foreach (var cuentaId in new[] { e.CuentaCxC, e.CuentaIngreso })
        {
            var fila = balance.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == cuentaId);
            Assert.Equal(100m, fila.GetProperty("totalDebito").GetDecimal());
            Assert.Equal(100m, fila.GetProperty("totalCredito").GetDecimal());
            Assert.Equal(0m, fila.GetProperty("saldo").GetDecimal());
        }

        var mayor = await (await client.GetAsync($"/api/libros/mayor?cuentaId={e.CuentaCxC}")).LeerJsonAsync();
        var movimientos = mayor.GetProperty("movimientos").EnumerateArray().ToArray();
        Assert.Equal(new[] { original, reversa }, movimientos.Select(m => m.GetProperty("asientoId").GetInt32()).ToArray());
        Assert.Equal(0m, movimientos[^1].GetProperty("saldoAcumulado").GetDecimal());

        var diario = await (await client.GetAsync($"/api/libros/diario?periodoId={e.PeriodoId}")).LeerJsonAsync();
        Assert.Equal(new HashSet<int> { original, reversa }, diario.Ids());
    }

    [Fact]
    public async Task Libros_ConAsientoBorrador_NoLoIncluyenEnBalanceDiarioNiMayor()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 40m, new DateOnly(2025, 3, 1));
        var borrador = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 999m, new DateOnly(2025, 3, 2), new OpcionesAsiento(Estado: "Borrador"));
        var client = _pg.CreateApiClient(e.UsuarioId);

        var balance = await (await client.GetAsync("/api/libros/balance-saldos")).LeerJsonAsync();
        var fila = balance.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == e.CuentaCxC);
        Assert.Equal(40m, fila.GetProperty("totalDebito").GetDecimal());
        Assert.Equal(40m, fila.GetProperty("saldo").GetDecimal());

        var diario = await (await client.GetAsync($"/api/libros/diario?periodoId={e.PeriodoId}")).LeerJsonAsync();
        Assert.Equal(1, diario.GetArrayLength());
        Assert.DoesNotContain(borrador, diario.Ids());

        var mayor = await (await client.GetAsync($"/api/libros/mayor?cuentaId={e.CuentaCxC}")).LeerJsonAsync();
        var movimientos = mayor.GetProperty("movimientos");
        Assert.Equal(1, movimientos.GetArrayLength());
        Assert.NotEqual(borrador, movimientos[0].GetProperty("asientoId").GetInt32());
    }

    [Fact]
    public async Task ReversarAsiento_ConAsientoYaReversadoOReversaOBorrador_FallaConEstadoNoValido()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"A-{TestData.Sufijo()}";
        var original = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 3, 1), new OpcionesAsiento(Numero: numero));
        var reversa = await _pg.Data.ReversarAsientoAsync(original, e.UsuarioId);
        var borrador = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 5m, new DateOnly(2025, 3, 2), new OpcionesAsiento(Estado: "Borrador"));

        foreach (var asientoId in new[] { original, reversa, borrador })
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _pg.Data.EjecutarAsync("CALL sp_reversar_asiento($1, $2)", asientoId, e.UsuarioId));
            Assert.Equal("55000", ex.SqlState);
        }

        var reversas = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE reversa_de_id = $1", original);
        Assert.Equal(1, reversas);
    }

    [Fact]
    public async Task ReversarAsiento_ConNumeroManualConPrefijoRev_SeReversaYQuedaVinculado()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"REV-{TestData.Sufijo()}";
        var original = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 3, 1), new OpcionesAsiento(Numero: numero));

        var reversa = await _pg.Data.ReversarAsientoAsync(original, e.UsuarioId);

        var estadoOriginal = await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", original);
        Assert.Equal("Anulado", estadoOriginal);
        var vinculo = await _pg.Data.ScalarAsync<int>("SELECT reversa_de_id FROM asientocontable WHERE id = $1", reversa);
        Assert.Equal(original, vinculo);
    }

    [Fact]
    public async Task ReversarAsiento_ConNumeroDe30Caracteres_ReversaConNumeroDe30Caracteres()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = Guid.NewGuid().ToString("N")[..30];
        var original = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 3, 1), new OpcionesAsiento(Numero: numero));

        var reversa = await _pg.Data.ReversarAsientoAsync(original, e.UsuarioId);

        var numeroReversa = await _pg.Data.ScalarAsync<string>("SELECT numero FROM asientocontable WHERE id = $1", reversa);
        Assert.Equal(30, numeroReversa.Length);
        Assert.Equal($"REV-{numero[..26]}", numeroReversa);
    }

    private static JsonElement FilaBalance(JsonElement balance, int cuentaId) =>
        balance.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == cuentaId);

    [Fact]
    public async Task BalanceSaldos_ConSubcuentas_AcumulaEnElPadreYExponeLaJerarquia()
    {
        var e = await _pg.Data.SembrarJerarquiaConSaldosAsync();
        var client = _pg.CreateApiClient(e.Usuario);

        var response = await client.GetAsync("/api/libros/balance-saldos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = await response.LeerJsonAsync();

        var padre = FilaBalance(balance, e.Padre);
        Assert.Equal(150m, padre.GetProperty("totalDebito").GetDecimal());
        Assert.Equal(150m, padre.GetProperty("saldo").GetDecimal());
        Assert.Equal(0m, padre.GetProperty("debitoPropio").GetDecimal());
        Assert.Equal(1, padre.GetProperty("nivel").GetInt32());
        Assert.False(padre.GetProperty("esHoja").GetBoolean());

        var hijoA = FilaBalance(balance, e.HijoA);
        Assert.Equal(100m, hijoA.GetProperty("totalDebito").GetDecimal());
        Assert.Equal(100m, hijoA.GetProperty("debitoPropio").GetDecimal());
        Assert.Equal(2, hijoA.GetProperty("nivel").GetInt32());
        Assert.True(hijoA.GetProperty("esHoja").GetBoolean());
        Assert.Equal(e.Padre, hijoA.GetProperty("cuentaPadreId").GetInt32());

        var ingreso = FilaBalance(balance, e.Ingreso);
        Assert.Equal(150m, ingreso.GetProperty("totalCredito").GetDecimal());
    }

    [Fact]
    public async Task BalanceSaldos_FilasDeNivelUno_DebitoIgualaCredito()
    {
        var e = await _pg.Data.SembrarJerarquiaConSaldosAsync();
        var client = _pg.CreateApiClient(e.Usuario);

        var balance = await (await client.GetAsync("/api/libros/balance-saldos")).LeerJsonAsync();
        var raices = new[] { FilaBalance(balance, e.Padre), FilaBalance(balance, e.Ingreso) };

        Assert.Equal(
            raices.Sum(f => f.GetProperty("totalDebito").GetDecimal()),
            raices.Sum(f => f.GetProperty("totalCredito").GetDecimal()));
    }

    [Fact]
    public async Task Mayor_ConCuentaPadre_IncluyeLosMovimientosDeSusSubcuentas()
    {
        var e = await _pg.Data.SembrarJerarquiaConSaldosAsync();
        var codigoA = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM cuentacontable WHERE id = $1", e.HijoA);
        var codigoB = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM cuentacontable WHERE id = $1", e.HijoB);
        var client = _pg.CreateApiClient(e.Usuario);

        var response = await client.GetAsync($"/api/libros/mayor?cuentaId={e.Padre}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();

        var cuenta = body.GetProperty("cuenta");
        Assert.False(cuenta.GetProperty("esHoja").GetBoolean());
        Assert.Equal(2, cuenta.GetProperty("subcuentas").GetInt32());

        var movimientos = body.GetProperty("movimientos").EnumerateArray().ToArray();
        Assert.Equal(new[] { codigoA, codigoB }, movimientos.Select(m => m.GetProperty("cuentaCodigo").GetString()).ToArray());
        Assert.Equal(SaldosAcumuladosConsolidado, movimientos.Select(m => m.GetProperty("saldoAcumulado").GetDecimal()).ToArray());
    }

    [Fact]
    public async Task Mayor_ConCuentaHoja_ExponeEsHojaYSinSubcuentas()
    {
        var e = await _pg.Data.SembrarJerarquiaConSaldosAsync();
        var codigoA = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM cuentacontable WHERE id = $1", e.HijoA);
        var client = _pg.CreateApiClient(e.Usuario);

        var body = await (await client.GetAsync($"/api/libros/mayor?cuentaId={e.HijoA}")).LeerJsonAsync();

        var cuenta = body.GetProperty("cuenta");
        Assert.True(cuenta.GetProperty("esHoja").GetBoolean());
        Assert.Equal(0, cuenta.GetProperty("subcuentas").GetInt32());
        var movimiento = body.GetProperty("movimientos").EnumerateArray().Single();
        Assert.Equal(codigoA, movimiento.GetProperty("cuentaCodigo").GetString());
    }
}
