using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class PeriodosContablesControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;

    public PeriodosContablesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Payload(string nombre, DateOnly inicio, DateOnly fin, string estado = "Abierto") => new
    {
        nombre,
        fechaInicio = inicio,
        fechaFin = fin,
        estado,
    };

    [Fact]
    public async Task Listar_Y_Obtener_DevuelvenElPeriodoSembradoYNotFoundSiNoExiste()
    {
        var id = await _pg.Data.CrearPeriodoAsync();
        var usuarioId = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuarioId, Roles.Vendedor);

        var listResponse = await client.GetAsync("/api/periodos");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.LeerJsonAsync();
        var ids = listBody.Ids();
        Assert.Contains(id, ids);

        var getResponse = await client.GetAsync($"/api/periodos/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.LeerJsonAsync();
        Assert.Equal("Abierto", getBody.GetProperty("estado").GetString());
        Assert.Equal(id, getBody.GetProperty("id").GetInt32());

        var notFoundResponse = await client.GetAsync($"/api/periodos/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201IgnoraEstadoEIdDelCliente()
    {
        var (ini, fin) = TestData.RangoPeriodoUnico();
        var payload = new
        {
            nombre = "Prueba",
            fechaInicio = ini,
            fechaFin = fin,
            id = 999999,
            estado = "Cerrado",
        };
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/periodos", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.LeerJsonAsync();
        Assert.Equal("Abierto", body.GetProperty("estado").GetString());
        Assert.NotEqual(999999, body.GetProperty("id").GetInt32());
        Assert.Equal(ini.ToString("yyyy-MM-dd"), body.GetProperty("fechaInicio").GetString());
    }

    [Fact]
    public async Task Crear_ConFechasInvalidasOSolapadas_Responde400()
    {
        var (ini, fin) = TestData.RangoPeriodoUnico();
        var basePayload = Payload("Base", ini, ini.AddMonths(6).AddDays(-1));
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var baseResponse = await client.PostAsJsonAsync("/api/periodos", basePayload);
        Assert.Equal(HttpStatusCode.Created, baseResponse.StatusCode);

        var response1 = await client.PostAsJsonAsync("/api/periodos", Payload("X", ini, ini));
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        var error1 = await response1.LeerErrorAsync();
        Assert.Contains("posterior", error1);

        var response2 = await client.PostAsJsonAsync("/api/periodos", Payload("X", fin, ini));
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        var error2 = await response2.LeerErrorAsync();
        Assert.Contains("posterior", error2);

        var response3 = await client.PostAsJsonAsync("/api/periodos", Payload("X", ini.AddMonths(6).AddDays(-1), fin));
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
        var error3 = await response3.LeerErrorAsync();
        Assert.Contains("solapa", error3);

        var response4 = await client.PostAsJsonAsync("/api/periodos", Payload("X", ini.AddMonths(6), fin));
        Assert.Equal(HttpStatusCode.Created, response4.StatusCode);
    }

    [Fact]
    public async Task Actualizar_SegunElEstadoYLasFechas_RespondeCorrectamente()
    {
        var a = TestData.RangoPeriodoUnico();
        var b = TestData.RangoPeriodoUnico();
        var c = TestData.RangoPeriodoUnico();

        var p1 = await _pg.Data.CrearPeriodoEnRangoAsync(a.Inicio, a.Fin);
        await _pg.Data.CrearPeriodoEnRangoAsync(b.Inicio, b.Fin);
        var cerrado = await _pg.Data.CrearPeriodoAsync("Cerrado");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response1 = await client.PutAsJsonAsync($"/api/periodos/{IdInexistente}", Payload("N", c.Inicio, c.Fin));
        Assert.Equal(HttpStatusCode.NotFound, response1.StatusCode);

        var response2 = await client.PutAsJsonAsync($"/api/periodos/{cerrado}", Payload("N", c.Inicio, c.Fin));
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        var error2 = await response2.LeerErrorAsync();
        Assert.Contains("cerrado", error2);

        var response3 = await client.PutAsJsonAsync($"/api/periodos/{p1}", Payload("N", c.Fin, c.Inicio));
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
        var error3 = await response3.LeerErrorAsync();
        Assert.Contains("posterior", error3);

        var response4 = await client.PutAsJsonAsync($"/api/periodos/{p1}", Payload("N", b.Inicio, b.Fin));
        Assert.Equal(HttpStatusCode.BadRequest, response4.StatusCode);
        var error4 = await response4.LeerErrorAsync();
        Assert.Contains("solapa", error4);

        var response5 = await client.PutAsJsonAsync($"/api/periodos/{p1}", Payload("Renombrado", c.Inicio, c.Fin, "Cerrado"));
        Assert.Equal(HttpStatusCode.OK, response5.StatusCode);
        var body5 = await response5.LeerJsonAsync();
        Assert.Equal("Renombrado", body5.GetProperty("nombre").GetString());
        Assert.Equal("Abierto", body5.GetProperty("estado").GetString());
        Assert.Equal(c.Inicio.ToString("yyyy-MM-dd"), body5.GetProperty("fechaInicio").GetString());
    }

    [Fact]
    public async Task CrearYActualizar_ConVendedor_Responde403()
    {
        var (ini, fin) = TestData.RangoPeriodoUnico();
        var client = _pg.CreateApiClient(0, Roles.Vendedor);

        var postResponse = await client.PostAsJsonAsync("/api/periodos", Payload("X", ini, fin));
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        var putResponse = await client.PutAsJsonAsync($"/api/periodos/{IdInexistente}", Payload("X", ini, fin));
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
    }

    [Fact]
    public async Task Cerrar_ConContadorYAsientos_CierraElPeriodoYConsolidaSaldos()
    {
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var periodo = await _pg.Data.CrearPeriodoAsync();

        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 100m, new DateOnly(2025, 3, 1));

        var client = _pg.CreateApiClient(contador);
        var response = await client.PostAsync($"/api/periodos/{periodo}/cerrar", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Cerrado", body.GetProperty("estado").GetString());

        var saldoCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM saldocuentaperiodo WHERE periodo_id = $1", periodo);
        Assert.Equal(2, saldoCount);

        var saldoD = await _pg.Data.ScalarAsync<decimal>("SELECT saldo_final FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, d);
        Assert.Equal(100m, saldoD);

        var auditCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'cerrar_periodo'", contador);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task Reabrir_ConAdministradorDeLaBd_ReabreElPeriodo()
    {
        var admin = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var periodo = await _pg.Data.CrearPeriodoAsync("Cerrado");

        var client = _pg.CreateApiClient(admin);
        var response = await client.PostAsync($"/api/periodos/{periodo}/reabrir", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Abierto", body.GetProperty("estado").GetString());

        var auditCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'reabrir_periodo'", admin);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task CerrarYReabrir_ConPerfilNoAutorizadoEnLaBd_Responde403SinCambiarElPeriodo()
    {
        var vendedor = await _pg.Data.CrearUsuarioAsync("Vendedor");
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var abierto = await _pg.Data.CrearPeriodoAsync();
        var cerrado = await _pg.Data.CrearPeriodoAsync("Cerrado");

        var vendedorClient = _pg.CreateApiClient(vendedor, Roles.Contador);
        var responseCerrar = await vendedorClient.PostAsync($"/api/periodos/{abierto}/cerrar", null);
        Assert.Equal(HttpStatusCode.Forbidden, responseCerrar.StatusCode);
        var errorCerrar = await responseCerrar.LeerErrorAsync();
        Assert.Contains("perfil autorizado", errorCerrar);

        var estadoAbierto = await _pg.Data.ScalarAsync<string>("SELECT estado FROM periodocontable WHERE id = $1", abierto);
        Assert.Equal("Abierto", estadoAbierto);

        var contadorClient = _pg.CreateApiClient(contador);
        var responseReabrir = await contadorClient.PostAsync($"/api/periodos/{cerrado}/reabrir", null);
        Assert.Equal(HttpStatusCode.Forbidden, responseReabrir.StatusCode);

        var estadoCerrado = await _pg.Data.ScalarAsync<string>("SELECT estado FROM periodocontable WHERE id = $1", cerrado);
        Assert.Equal("Cerrado", estadoCerrado);
    }

    [Fact]
    public async Task CerrarYReabrir_SinTokenOPeriodoInexistente_RespondeCorrespondiente()
    {
        var anonClient = _pg.Factory.CreateClient();
        var responseAnon = await anonClient.PostAsync($"/api/periodos/{IdInexistente}/cerrar", null);
        Assert.Equal(HttpStatusCode.Unauthorized, responseAnon.StatusCode);

        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var client = _pg.CreateApiClient(contador);

        var responseCerrar = await client.PostAsync($"/api/periodos/{IdInexistente}/cerrar", null);
        Assert.Equal(HttpStatusCode.NotFound, responseCerrar.StatusCode);

        var responseReabrir = await client.PostAsync($"/api/periodos/{IdInexistente}/reabrir", null);
        Assert.Equal(HttpStatusCode.NotFound, responseReabrir.StatusCode);
    }

    [Fact]
    public async Task Cerrar_ConPeriodoYaCerrado_Responde409SinDuplicarSaldosNiBitacora()
    {
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var periodo = await _pg.Data.CrearPeriodoAsync();
        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 100m, new DateOnly(2025, 3, 1));
        var client = _pg.CreateApiClient(contador);

        var primera = await client.PostAsync($"/api/periodos/{periodo}/cerrar", null);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var segunda = await client.PostAsync($"/api/periodos/{periodo}/cerrar", null);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        var error = await segunda.LeerErrorAsync();
        Assert.Contains("no está Abierto", error);

        var saldos = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM saldocuentaperiodo WHERE periodo_id = $1", periodo);
        Assert.Equal(2, saldos);
        var bitacora = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'cerrar_periodo'", contador);
        Assert.Equal(1, bitacora);
    }

    [Fact]
    public async Task Reabrir_ConPeriodoAbierto_Responde409()
    {
        var admin = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var periodo = await _pg.Data.CrearPeriodoAsync();
        var client = _pg.CreateApiClient(admin);

        var response = await client.PostAsync($"/api/periodos/{periodo}/reabrir", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.LeerErrorAsync();
        Assert.Contains("no está Cerrado", error);

        var auditoria = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'reabrir_periodo'", admin);
        Assert.Equal(0, auditoria);
    }

    [Fact]
    public async Task CerrarReabrirYCerrar_VersionaLosSnapshotsYConservaElAnterior()
    {
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var admin = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var periodo = await _pg.Data.CrearPeriodoAsync();
        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 100m, new DateOnly(2025, 3, 1));
        var clienteContador = _pg.CreateApiClient(contador);
        var clienteAdmin = _pg.CreateApiClient(admin);

        Assert.Equal(HttpStatusCode.OK, (await clienteContador.PostAsync($"/api/periodos/{periodo}/cerrar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteAdmin.PostAsync($"/api/periodos/{periodo}/reabrir", null)).StatusCode);
        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 50m, new DateOnly(2025, 3, 2));
        Assert.Equal(HttpStatusCode.OK, (await clienteContador.PostAsync($"/api/periodos/{periodo}/cerrar", null)).StatusCode);

        var versiones = await _pg.Data.ScalarAsync<long>("SELECT COUNT(DISTINCT cierre_numero) FROM saldocuentaperiodo WHERE periodo_id = $1", periodo);
        Assert.Equal(2, versiones);
        var maxima = await _pg.Data.ScalarAsync<int>("SELECT MAX(cierre_numero) FROM saldocuentaperiodo WHERE periodo_id = $1", periodo);
        Assert.Equal(2, maxima);

        var snapshotUno = await _pg.Data.ScalarAsync<decimal>("SELECT saldo_final FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2 AND cierre_numero = 1", periodo, d);
        Assert.Equal(100m, snapshotUno);

        var vigente = await _pg.Data.ScalarAsync<decimal>("SELECT saldo_final FROM vw_saldocuentaperiodo_vigente WHERE periodo_id = $1 AND cuenta_id = $2", periodo, d);
        Assert.Equal(150m, vigente);
        var filasVigentes = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM vw_saldocuentaperiodo_vigente WHERE periodo_id = $1", periodo);
        Assert.Equal(2, filasVigentes);
    }

    [Fact]
    public async Task CerrarPeriodoVacioReabrirYCerrar_NumeraElSegundoCierreComoDos()
    {
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var admin = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var periodo = await _pg.Data.CrearPeriodoAsync();
        var clienteContador = _pg.CreateApiClient(contador);
        var clienteAdmin = _pg.CreateApiClient(admin);

        Assert.Equal(HttpStatusCode.OK, (await clienteContador.PostAsync($"/api/periodos/{periodo}/cerrar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteAdmin.PostAsync($"/api/periodos/{periodo}/reabrir", null)).StatusCode);
        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 100m, new DateOnly(2025, 3, 1));
        Assert.Equal(HttpStatusCode.OK, (await clienteContador.PostAsync($"/api/periodos/{periodo}/cerrar", null)).StatusCode);

        var cierres = await _pg.Data.ScalarAsync<int>("SELECT cierres FROM periodocontable WHERE id = $1", periodo);
        Assert.Equal(2, cierres);
        var filasConCierreDos = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM saldocuentaperiodo WHERE periodo_id = $1 AND cierre_numero = 2", periodo);
        Assert.Equal(2, filasConCierreDos);
        var filasTotales = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM saldocuentaperiodo WHERE periodo_id = $1", periodo);
        Assert.Equal(2, filasTotales);
        var filasVigentes = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM vw_saldocuentaperiodo_vigente WHERE periodo_id = $1", periodo);
        Assert.Equal(2, filasVigentes);
    }

    [Fact]
    public async Task Cerrar_ConAsientoReversadoYBorrador_SaldosNetanCeroYExcluyenBorrador()
    {
        var contador = await _pg.Data.CrearUsuarioAsync("Contador");
        var d = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var k = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var periodo = await _pg.Data.CrearPeriodoAsync();

        var numeroOriginal = $"A-{Guid.NewGuid().ToString("N")[..12]}";
        var asientoId = await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 100m, new DateOnly(2025, 3, 1),
            new OpcionesAsiento(Numero: numeroOriginal));
        await _pg.Data.ReversarAsientoAsync(asientoId, contador);
        await _pg.Data.SembrarAsientoAsync(periodo, contador, d, k, 40m, new DateOnly(2025, 3, 2),
            new OpcionesAsiento(Estado: "Borrador"));

        var client = _pg.CreateApiClient(contador);
        var response = await client.PostAsync($"/api/periodos/{periodo}/cerrar", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var totalDebitoD = await _pg.Data.ScalarAsync<decimal>(
            "SELECT total_debito FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, d);
        Assert.Equal(100m, totalDebitoD);

        var totalCreditoD = await _pg.Data.ScalarAsync<decimal>(
            "SELECT total_credito FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, d);
        Assert.Equal(100m, totalCreditoD);

        var saldoFinalD = await _pg.Data.ScalarAsync<decimal>(
            "SELECT saldo_final FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, d);
        Assert.Equal(0m, saldoFinalD);

        var totalDebitoK = await _pg.Data.ScalarAsync<decimal>(
            "SELECT total_debito FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, k);
        Assert.Equal(100m, totalDebitoK);

        var totalCreditoK = await _pg.Data.ScalarAsync<decimal>(
            "SELECT total_credito FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, k);
        Assert.Equal(100m, totalCreditoK);

        var saldoFinalK = await _pg.Data.ScalarAsync<decimal>(
            "SELECT saldo_final FROM saldocuentaperiodo WHERE periodo_id = $1 AND cuenta_id = $2", periodo, k);
        Assert.Equal(0m, saldoFinalK);
    }
}
