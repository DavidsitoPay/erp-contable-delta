using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class AsientosControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;

    public AsientosControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Linea(int cuentaId, decimal debito, decimal credito, int? centroCostoId = null) =>
        new { cuentaId, centroCostoId, debito, credito };

    private static object[] LineasBalanceadas(Escenario e, decimal monto = 100m, int? centroCostoId = null) =>
        new[] { Linea(e.CuentaCxC, monto, 0m, centroCostoId), Linea(e.CuentaIngreso, 0m, monto, centroCostoId) };

    private static object Payload(string numero, int periodoId, object[] lineas, string estado = "Confirmado", int usuarioIdCuerpo = 0) => new
    {
        numero,
        fecha = "2025-03-15",
        periodoId,
        monto = 9999m,
        estado,
        usuarioId = usuarioIdCuerpo,
        lineas,
    };

    private async Task<(HttpClient Cliente, int AdministradorId)> ClienteAdministradorAsync()
    {
        var administrador = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        return (_pg.CreateApiClient(administrador, Roles.Administrador), administrador);
    }

    private async Task<(Escenario E, int AsientoId)> SembrarAsientoReversibleAsync(OpcionesAsiento? opciones = null)
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var asiento = await _pg.Data.SembrarAsientoAsync(
            e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(2025, 3, 1), opciones);
        return (e, asiento);
    }

    private static Task<HttpResponseMessage> ReversarAsync(HttpClient client, int asientoId, string? motivo = "Motivo de prueba") =>
        client.PostAsJsonAsync($"/api/asientos/{asientoId}/reversar", new { motivo });

    private async Task<string?> ReversarConConflictoAsync(int asientoId)
    {
        var (client, _) = await ClienteAdministradorAsync();
        var response = await ReversarAsync(client, asientoId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return await response.LeerErrorAsync();
    }

    private static async Task<decimal> SaldoAsync(HttpClient client, int cuentaId)
    {
        var balance = await (await client.GetAsync("/api/libros/balance-saldos")).LeerJsonAsync();
        var fila = balance.EnumerateArray().Single(x => x.GetProperty("cuentaId").GetInt32() == cuentaId);
        return fila.GetProperty("saldo").GetDecimal();
    }

    private Task<string> EstadoAsync(int asientoId) =>
        _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", asientoId);

    [Fact]
    public async Task Registrar_ConPartidaDobleValida_Responde201ConMontoYUsuarioDelServidorYAuditoria()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otro = await _pg.Data.CrearUsuarioAsync();
        var numero = $"AS-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/asientos", Payload(numero, e.PeriodoId, LineasBalanceadas(e, 100m, e.CentroCostoId), usuarioIdCuerpo: otro));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal(100m, body.GetProperty("monto").GetDecimal());
        Assert.Equal(e.UsuarioId, body.GetProperty("usuarioId").GetInt32());
        Assert.Equal("Confirmado", body.GetProperty("estado").GetString());

        var lineaCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", body.GetProperty("id").GetInt32());
        Assert.Equal(2, lineaCount);

        var lineaConCentro = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1 AND centro_costo_id = $2", body.GetProperty("id").GetInt32(), e.CentroCostoId);
        Assert.Equal(2, lineaConCentro);

        var usuarioDelAsiento = await _pg.Data.ScalarAsync<int>("SELECT usuario_id FROM asientocontable WHERE numero = $1", numero);
        Assert.Equal(e.UsuarioId, usuarioDelAsiento);

        var auditCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_asiento'", e.UsuarioId);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task Registrar_SegunElRol_AutorizaAdministradorYRechazaVendedorYAnonimo()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();

        var adminClient = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);
        var numero1 = $"AS-{TestData.Sufijo()}";
        var responseAdmin = await adminClient.PostAsJsonAsync("/api/asientos", Payload(numero1, e.PeriodoId, LineasBalanceadas(e)));
        Assert.Equal(HttpStatusCode.Created, responseAdmin.StatusCode);

        var vendedorClient = _pg.CreateApiClient(0, Roles.Vendedor);
        var numero2 = $"AS-{TestData.Sufijo()}";
        var responseVendedor = await vendedorClient.PostAsJsonAsync("/api/asientos", Payload(numero2, e.PeriodoId, LineasBalanceadas(e)));
        Assert.Equal(HttpStatusCode.Forbidden, responseVendedor.StatusCode);

        var anonClient = _pg.Factory.CreateClient();
        var numero3 = $"AS-{TestData.Sufijo()}";
        var responseAnon = await anonClient.PostAsJsonAsync("/api/asientos", Payload(numero3, e.PeriodoId, LineasBalanceadas(e)));
        Assert.Equal(HttpStatusCode.Unauthorized, responseAnon.StatusCode);
    }

    [Fact]
    public async Task Registrar_ConLineasQueIncumplenLaPartidaDoble_Responde400SinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"AS-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var casos = new[]
        {
            new { Lineas = new object[] { Linea(e.CuentaCxC, 100m, 0m) }, Fragmento = "dos líneas" },
            new { Lineas = new object[] { Linea(e.CuentaCxC, 100m, 0m), Linea(e.CuentaIngreso, 0m, 50m) }, Fragmento = "partida doble" },
            new { Lineas = new object[] { Linea(e.CuentaCxC, 10m, 10m), Linea(e.CuentaIngreso, 0m, 0m) }, Fragmento = "simultáneamente" },
            new { Lineas = new object[] { Linea(e.CuentaCxC, -10m, 0m), Linea(e.CuentaIngreso, 0m, -10m) }, Fragmento = "negativo" },
        };

        foreach (var caso in casos)
        {
            var response = await client.PostAsJsonAsync("/api/asientos", Payload(numero, e.PeriodoId, caso.Lineas));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.LeerErrorAsync();
            Assert.Contains(caso.Fragmento, error);
        }

        var asientoCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE numero = $1", numero);
        Assert.Equal(0, asientoCount);
    }

    [Fact]
    public async Task Registrar_ConReferenciasInvalidas_Responde400SinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var inactiva = await _pg.Data.CrearCuentaInactivaAsync("Ingreso", "Acreedora");
        var mayor = await _pg.Data.CrearCuentaConSubcuentasAsync("Ingreso", "Acreedora");
        var periodoCerrado = await _pg.Data.CrearPeriodoAsync("Cerrado");
        var numero = $"AS-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var casos = new[]
        {
            new { Cuerpo = Payload(numero, e.PeriodoId, new object[] { Linea(IdInexistente, 100m, 0m), Linea(e.CuentaIngreso, 0m, 100m) }), Fragmento = "no existen" },
            new { Cuerpo = Payload(numero, e.PeriodoId, new object[] { Linea(e.CuentaCxC, 100m, 0m), Linea(inactiva, 0m, 100m) }), Fragmento = "inactivas" },
            new { Cuerpo = Payload(numero, e.PeriodoId, new object[] { Linea(e.CuentaCxC, 100m, 0m), Linea(mayor.PadreId, 0m, 100m) }), Fragmento = "de mayor" },
            new { Cuerpo = Payload(numero, e.PeriodoId, LineasBalanceadas(e, 100m, IdInexistente)), Fragmento = "centros de costo no existen" },
            new { Cuerpo = Payload(numero, IdInexistente, LineasBalanceadas(e)), Fragmento = "periodo indicado no existe" },
            new { Cuerpo = Payload(numero, periodoCerrado, LineasBalanceadas(e)), Fragmento = "Cerrado" },
            new { Cuerpo = Payload(numero, e.PeriodoId, LineasBalanceadas(e), estado: "Borrador"), Fragmento = "Confirmado" },
        };

        foreach (var caso in casos)
        {
            var response = await client.PostAsJsonAsync("/api/asientos", caso.Cuerpo);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.LeerErrorAsync();
            Assert.Contains(caso.Fragmento, error);
        }

        var asientoCount = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE numero = $1", numero);
        Assert.Equal(0, asientoCount);
    }

    [Fact]
    public async Task Reversar_ComoAdministradorConMotivo_Responde200AnulaOriginalYCreaReversaConfirmada()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        var numeroOriginal = await _pg.Data.ScalarAsync<string>("SELECT numero FROM asientocontable WHERE id = $1", original);
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        var reversa = body.GetProperty("reversaId").GetInt32();
        Assert.Equal(original, body.GetProperty("originalId").GetInt32());
        Assert.Equal(numeroOriginal, body.GetProperty("originalNumero").GetString());
        Assert.Equal($"REV-{numeroOriginal}", body.GetProperty("reversaNumero").GetString());
        Assert.Equal("Anulado", await EstadoAsync(original));
        Assert.Equal("Confirmado", await EstadoAsync(reversa));
        Assert.Equal(original, await _pg.Data.ScalarAsync<int>("SELECT reversa_de_id FROM asientocontable WHERE id = $1", reversa));
        Assert.Equal(e.PeriodoId, await _pg.Data.ScalarAsync<int>("SELECT periodo_id FROM asientocontable WHERE id = $1", reversa));
        Assert.Equal(100m, await _pg.Data.CreditoAsync(reversa, e.CuentaCxC));
        Assert.Equal(0m, await _pg.Data.DebitoAsync(reversa, e.CuentaCxC));
        Assert.Equal(100m, await _pg.Data.DebitoAsync(reversa, e.CuentaIngreso));
        Assert.Equal(0m, await _pg.Data.CreditoAsync(reversa, e.CuentaIngreso));
    }

    [Fact]
    public async Task Reversar_FechaDeLaReversa_EsHoyDentroDelPeriodoOFinDePeriodoSiHoyLoSupera()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversa = (await response.LeerJsonAsync()).GetProperty("reversaId").GetInt32();
        var fechaReversa = await _pg.Data.ScalarAsync<string>("SELECT fecha::text FROM asientocontable WHERE id = $1", reversa);
        var fechaEsperada = await _pg.Data.ScalarAsync<string>(
            "SELECT LEAST(CURRENT_DATE, fecha_fin)::text FROM periodocontable WHERE id = $1", e.PeriodoId);
        Assert.Equal(fechaEsperada, fechaReversa);
        var noAnteriorAlOriginal = await _pg.Data.ScalarAsync<bool>(
            "SELECT r.fecha >= o.fecha FROM asientocontable r JOIN asientocontable o ON o.id = r.reversa_de_id WHERE r.id = $1", reversa);
        Assert.True(noAnteriorAlOriginal);
    }

    [Fact]
    public async Task Reversar_RegistraAuditoriaConElMotivo()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var (client, administrador) = await ClienteAdministradorAsync();
        var motivo = $"Error de captura {TestData.Sufijo()}";

        var response = await ReversarAsync(client, original, motivo);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(administrador, "reversar_asiento"));
        var detalle = await _pg.Data.ScalarAsync<string>(
            "SELECT detalle FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'reversar_asiento'", administrador);
        Assert.Contains(motivo, detalle);
    }

    [Fact]
    public async Task Reversar_AplicaYNetaBalance_DejaSaldoNetoCero()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();
        Assert.Equal(100m, await SaldoAsync(client, e.CuentaCxC));
        Assert.Equal(100m, await SaldoAsync(client, e.CuentaIngreso));

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, await SaldoAsync(client, e.CuentaCxC));
        Assert.Equal(0m, await SaldoAsync(client, e.CuentaIngreso));
    }

    [Fact]
    public async Task Reversar_ConMotivoDe250Caracteres_Responde200()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original, new string('m', 250));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reversar_ComoContador_Responde403SinReversar()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_SinToken_Responde401()
    {
        var client = _pg.Factory.CreateClient();

        var response = await ReversarAsync(client, IdInexistente);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reversar_AsientoInexistente_Responde404()
    {
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, IdInexistente);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no existe", await response.LeerErrorAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reversar_SinMotivoOEnBlanco_Responde400SinReversar(string? motivo)
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original, motivo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("obligatorio", await response.LeerErrorAsync());
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_ConMotivoDeMasDe250Caracteres_Responde400SinReversar()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original, new string('m', 251));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("250", await response.LeerErrorAsync());
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_AsientoAnulado_Responde409()
    {
        var (_, anulado) = await SembrarAsientoReversibleAsync(new OpcionesAsiento(Estado: "Anulado"));

        var error = await ReversarConConflictoAsync(anulado);

        Assert.Contains("no está Confirmado", error);
    }

    [Fact]
    public async Task Reversar_DosVeces_Responde409LaSegunda()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var (client, _) = await ClienteAdministradorAsync();
        var primera = await ReversarAsync(client, original);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var error = await ReversarConConflictoAsync(original);

        Assert.Contains("no está Confirmado", error);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE reversa_de_id = $1", original));
    }

    [Fact]
    public async Task Reversar_UnaReversa_Responde409()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var reversa = await _pg.Data.ReversarAsientoAsync(original);

        var error = await ReversarConConflictoAsync(reversa);

        Assert.Contains("es una reversa", error);
    }

    [Fact]
    public async Task Reversar_PeriodoCerrado_Responde409()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        await _pg.Data.CerrarPeriodoDirectoAsync(e.PeriodoId);

        var error = await ReversarConConflictoAsync(original);

        Assert.Contains("periodo Abierto", error);
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_AsientoDeFacturaCxC_Responde409()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        await _pg.Data.VincularFacturaCxCAsync(e.ClienteId, original, new DateOnly(2025, 3, 1));

        var error = await ReversarConConflictoAsync(original);

        Assert.Contains("cuentas por cobrar", error);
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_AsientoDeFacturaCxP_Responde409()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        await _pg.Data.VincularFacturaCxPAsync(e.ProveedorId, original, new DateOnly(2025, 3, 1));

        var error = await ReversarConConflictoAsync(original);

        Assert.Contains("cuentas por pagar", error);
        Assert.Equal("Confirmado", await EstadoAsync(original));
    }

    [Fact]
    public async Task Reversar_AsientoDeMovimientoTesoreria_Responde409()
    {
        var asientoTesoreria = await _pg.Data.SembrarAsientoDeTesoreriaAsync();

        var error = await ReversarConConflictoAsync(asientoTesoreria);

        Assert.Contains("desincronizaría", error);
        Assert.Equal("Confirmado", await EstadoAsync(asientoTesoreria));
    }

    [Fact]
    public async Task Reversar_ConClaimAdministradorYPerfilContadorEnLaBd_Responde403SinReversar()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("no tiene perfil autorizado", await response.LeerErrorAsync());
        Assert.Equal("Confirmado", await EstadoAsync(original));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE reversa_de_id = $1", original));
    }

    [Fact]
    public async Task Reversar_ConAsientoPosteriorAHoyEnPeriodoFuturo_ConservaLaFechaOriginal()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        var periodoFuturo = await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin);
        var fechaFutura = inicio.AddDays(10);
        var original = await _pg.Data.SembrarAsientoAsync(periodoFuturo, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, fechaFutura);
        var (client, _) = await ClienteAdministradorAsync();

        var response = await ReversarAsync(client, original);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversa = (await response.LeerJsonAsync()).GetProperty("reversaId").GetInt32();
        var fechaReversa = await _pg.Data.ScalarAsync<string>("SELECT fecha::text FROM asientocontable WHERE id = $1", reversa);
        Assert.Equal(fechaFutura.ToString("yyyy-MM-dd"), fechaReversa);
    }

    [Fact]
    public async Task Reversar_ProcedimientoConPerfilNoAdministrador_LanzaP0001()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("CALL sp_reversar_asiento($1, $2, $3)", original, e.UsuarioId, "Motivo de prueba"));

        Assert.Equal("P0001", ex.SqlState);
    }

    [Fact]
    public async Task Reversar_ProcedimientoConMotivoEnBlanco_Lanza22023()
    {
        var (_, original) = await SembrarAsientoReversibleAsync();
        var administrador = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("CALL sp_reversar_asiento($1, $2, $3)", original, administrador, "   "));

        Assert.Equal("22023", ex.SqlState);
    }

    [Fact]
    public async Task Reversar_ProcedimientoDeDosArgumentos_NoExiste42883()
    {
        var (e, original) = await SembrarAsientoReversibleAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("CALL sp_reversar_asiento($1, $2)", original, e.UsuarioId));

        Assert.Equal("42883", ex.SqlState);
    }

    [Fact]
    public async Task Registrar_IgnoraReversaDeIdDelCliente()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otro = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 10m, new DateOnly(2025, 3, 1));
        var numero = $"AS-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);
        var cuerpo = new
        {
            numero,
            fecha = "2025-03-15",
            periodoId = e.PeriodoId,
            estado = "Confirmado",
            reversaDeId = otro,
            lineas = LineasBalanceadas(e),
        };

        var response = await client.PostAsJsonAsync("/api/asientos", cuerpo);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sinReversaDe = await _pg.Data.ScalarAsync<bool>("SELECT reversa_de_id IS NULL FROM asientocontable WHERE numero = $1", numero);
        Assert.True(sinReversaDe);
    }

    [Fact]
    public async Task Bitacora_ConUpdateODeleteDirecto_LaRechazaElTrigger()
    {
        var usuarioId = await _pg.Data.CrearUsuarioAsync();
        var bitacoraId = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO bitacoraauditoria (usuario_id, accion, tabla_afectada) VALUES ($1, 'prueba_inmutabilidad', 'asientocontable') RETURNING id",
            usuarioId);

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE bitacoraauditoria SET accion = 'alterada' WHERE id = $1", bitacoraId));
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM bitacoraauditoria WHERE id = $1", bitacoraId));

        Assert.Equal("P0001", update.SqlState);
        Assert.Contains("inmutable", update.MessageText);
        Assert.Equal("P0001", delete.SqlState);
        Assert.Contains("inmutable", delete.MessageText);
        Assert.Equal("prueba_inmutabilidad", await _pg.Data.ScalarAsync<string>("SELECT accion FROM bitacoraauditoria WHERE id = $1", bitacoraId));
    }
}
