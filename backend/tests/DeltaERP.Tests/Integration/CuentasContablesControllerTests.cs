using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CuentasContablesControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;

    public CuentasContablesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Payload(string codigo, string tipo = "Activo", string naturaleza = "Deudora", int? cuentaPadreId = null, string nombre = "Cuenta de prueba") =>
        new { codigo, nombre, tipo, naturaleza, cuentaPadreId, activa = false };

    private static string CodigoNuevo() => $"N{TestData.Sufijo()}";

    [Fact]
    public async Task Listar_ExcluyeInactivasSalvoQueSePidan()
    {
        var activa = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var inactiva = await _pg.Data.CrearCuentaInactivaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var response = await client.GetAsync("/api/cuentas");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        var ids = body.Ids();
        Assert.Contains(activa, ids);
        Assert.DoesNotContain(inactiva, ids);

        var responseConInactivas = await client.GetAsync("/api/cuentas?incluirInactivas=true");
        Assert.Equal(HttpStatusCode.OK, responseConInactivas.StatusCode);
        var bodyConInactivas = await responseConInactivas.LeerJsonAsync();
        var idsConInactivas = bodyConInactivas.Ids();
        Assert.Contains(activa, idsConInactivas);
        Assert.Contains(inactiva, idsConInactivas);
    }

    [Fact]
    public async Task Obtener_DevuelveLaCuentaOReponde404()
    {
        var id = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.GetAsync($"/api/cuentas/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(id, body.GetProperty("id").GetInt32());

        var response404 = await client.GetAsync($"/api/cuentas/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201ForzandoActivaYPermitiendoPadre()
    {
        var codigo = CodigoNuevo();
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/cuentas", Payload(codigo));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("activa").GetBoolean());
        Assert.Equal(codigo, body.GetProperty("codigo").GetString());
        var padreId = body.GetProperty("id").GetInt32();

        var responsePadre = await client.PostAsJsonAsync("/api/cuentas", Payload(CodigoNuevo(), cuentaPadreId: padreId));
        Assert.Equal(HttpStatusCode.Created, responsePadre.StatusCode);
        var bodyPadre = await responsePadre.LeerJsonAsync();
        Assert.Equal(padreId, bodyPadre.GetProperty("cuentaPadreId").GetInt32());
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_Responde400()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());
        var otroTipo = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");

        var casos = new[]
        {
            new { Cuerpo = Payload(CodigoNuevo(), "Otro"), Fragmento = "Tipo inválido" },
            new { Cuerpo = Payload(CodigoNuevo(), naturaleza: "Otra"), Fragmento = "Naturaleza inválida" },
            new { Cuerpo = Payload(CodigoNuevo(), "Activo", "Acreedora"), Fragmento = "debe ser Deudora" },
            new { Cuerpo = Payload(CodigoNuevo(), "Pasivo", "Deudora"), Fragmento = "debe ser Acreedora" },
            new { Cuerpo = Payload(CodigoNuevo(), cuentaPadreId: IdInexistente), Fragmento = "no existe" },
            new { Cuerpo = Payload(CodigoNuevo(), "Activo", "Deudora", otroTipo), Fragmento = "mismo tipo" },
        };

        foreach (var caso in casos)
        {
            var response = await client.PostAsJsonAsync("/api/cuentas", caso.Cuerpo);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.LeerErrorAsync();
            Assert.Contains(caso.Fragmento, error);
        }
    }

    [Fact]
    public async Task Crear_ConCodigoDuplicado_Responde409()
    {
        var existente = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var codigo = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM cuentacontable WHERE id = $1", existente);
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/cuentas", Payload(codigo));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.LeerErrorAsync();
        Assert.Contains("Ya existe", error);
    }

    [Fact]
    public async Task Actualizar_ConDatosValidos_CambiaNombreYPadreSinTocarElCodigo()
    {
        var x = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var y = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var codigoOriginal = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM cuentacontable WHERE id = $1", x);
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PutAsJsonAsync($"/api/cuentas/{x}", Payload("CAMBIADO", nombre: "Nuevo nombre", cuentaPadreId: y));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Nuevo nombre", body.GetProperty("nombre").GetString());
        Assert.Equal(y, body.GetProperty("cuentaPadreId").GetInt32());
        Assert.Equal(codigoOriginal, body.GetProperty("codigo").GetString());

        var activa = await _pg.Data.ScalarAsync<bool>("SELECT activa FROM cuentacontable WHERE id = $1", x);
        Assert.True(activa);
    }

    [Fact]
    public async Task Actualizar_ConCuentaInexistente_Responde404()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());
        var response = await client.PutAsJsonAsync($"/api/cuentas/{IdInexistente}", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ConPadreInvalido_Responde400()
    {
        var (padre, hijo) = await _pg.Data.CrearCuentaConSubcuentasAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var responseNieto = await client.PostAsJsonAsync("/api/cuentas", Payload(CodigoNuevo(), cuentaPadreId: hijo));
        Assert.Equal(HttpStatusCode.Created, responseNieto.StatusCode);
        var nieto = (await responseNieto.LeerJsonAsync()).GetProperty("id").GetInt32();

        var otroTipo = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");

        var casos = new[]
        {
            new { IdEditado = padre, PadreId = padre, Fragmento = "propia cuenta padre" },
            new { IdEditado = padre, PadreId = IdInexistente, Fragmento = "no existe" },
            new { IdEditado = padre, PadreId = otroTipo, Fragmento = "mismo tipo" },
            new { IdEditado = padre, PadreId = hijo, Fragmento = "circular" },
            new { IdEditado = padre, PadreId = nieto, Fragmento = "circular" },
        };

        foreach (var caso in casos)
        {
            var response = await client.PutAsJsonAsync($"/api/cuentas/{caso.IdEditado}", Payload("X", cuentaPadreId: caso.PadreId));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.LeerErrorAsync();
            Assert.Contains(caso.Fragmento, error);
        }
    }

    [Fact]
    public async Task Desactivar_MarcaInactivaDeFormaIdempotenteOResponde404()
    {
        var id = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.DeleteAsync($"/api/cuentas/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var activa = await _pg.Data.ScalarAsync<bool>("SELECT activa FROM cuentacontable WHERE id = $1", id);
        Assert.False(activa);

        var response2 = await client.DeleteAsync($"/api/cuentas/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response2.StatusCode);

        var response404 = await client.DeleteAsync($"/api/cuentas/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Escritura_ConVendedorOAnonimo_Responde403Y401()
    {
        var vendedorClient = _pg.CreateApiClient(0, Roles.Vendedor);

        var postResponse = await vendedorClient.PostAsJsonAsync("/api/cuentas", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        var putResponse = await vendedorClient.PutAsJsonAsync($"/api/cuentas/{IdInexistente}", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);

        var deleteResponse = await vendedorClient.DeleteAsync($"/api/cuentas/{IdInexistente}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var anonClient = _pg.Factory.CreateClient();
        var getResponse = await anonClient.GetAsync("/api/cuentas");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
    }
}
