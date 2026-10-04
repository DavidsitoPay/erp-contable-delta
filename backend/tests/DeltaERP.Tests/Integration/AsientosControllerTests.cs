using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

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
}
