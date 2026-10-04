using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CentrosCostoControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;

    public CentrosCostoControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Payload(string codigo, string nombre = "Centro de prueba") =>
        new { codigo, nombre, activo = false };

    private static string CodigoNuevo() => $"N{TestData.Sufijo()}";

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201ForzandoActivo()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/centros-costo", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Crear_ConCodigoDuplicado_Responde409()
    {
        var id = await _pg.Data.CrearCentroCostoAsync();
        var codigo = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM centrocosto WHERE id = $1", id);
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/centros-costo", Payload(codigo));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.LeerErrorAsync();
        Assert.Contains("Ya existe", error);
    }

    [Fact]
    public async Task Obtener_DevuelveElCentroOResponde404()
    {
        var id = await _pg.Data.CrearCentroCostoAsync();
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.GetAsync($"/api/centros-costo/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(id, body.GetProperty("id").GetInt32());

        var response404 = await client.GetAsync($"/api/centros-costo/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Listar_ExcluyeInactivosSalvoQueSePidan()
    {
        var id = await _pg.Data.CrearCentroCostoAsync();
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var deleteResponse = await client.DeleteAsync($"/api/centros-costo/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/centros-costo");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.LeerJsonAsync();
        var ids = listBody.Ids();
        Assert.DoesNotContain(id, ids);

        var listConInactivosResponse = await client.GetAsync("/api/centros-costo?incluirInactivos=true");
        Assert.Equal(HttpStatusCode.OK, listConInactivosResponse.StatusCode);
        var listConInactivosBody = await listConInactivosResponse.LeerJsonAsync();
        var idsConInactivos = listConInactivosBody.Ids();
        Assert.Contains(id, idsConInactivos);

        var deleteAgainResponse = await client.DeleteAsync($"/api/centros-costo/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteAgainResponse.StatusCode);

        var delete404Response = await client.DeleteAsync($"/api/centros-costo/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, delete404Response.StatusCode);

        var activoId = await _pg.Data.CrearCentroCostoAsync();
        var listResponse2 = await client.GetAsync("/api/centros-costo");
        Assert.Equal(HttpStatusCode.OK, listResponse2.StatusCode);
        var listBody2 = await listResponse2.LeerJsonAsync();
        var ids2 = listBody2.Ids();
        Assert.Contains(activoId, ids2);
    }

    [Fact]
    public async Task Actualizar_CambiaSoloElNombre()
    {
        var id = await _pg.Data.CrearCentroCostoAsync();
        var codigoOriginal = await _pg.Data.ScalarAsync<string>("SELECT codigo FROM centrocosto WHERE id = $1", id);
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PutAsJsonAsync($"/api/centros-costo/{id}", Payload("OTRO", "Nombre nuevo"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Nombre nuevo", body.GetProperty("nombre").GetString());
        Assert.Equal(codigoOriginal, body.GetProperty("codigo").GetString());
        Assert.True(body.GetProperty("activo").GetBoolean());

        var response404 = await client.PutAsJsonAsync($"/api/centros-costo/{IdInexistente}", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Escritura_ConVendedorOAnonimo_Responde403Y401()
    {
        var vendedorClient = _pg.CreateApiClient(0, Roles.Vendedor);

        var postResponse = await vendedorClient.PostAsJsonAsync("/api/centros-costo", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        var putResponse = await vendedorClient.PutAsJsonAsync($"/api/centros-costo/{IdInexistente}", Payload(CodigoNuevo()));
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);

        var deleteResponse = await vendedorClient.DeleteAsync($"/api/centros-costo/{IdInexistente}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var anonClient = _pg.Factory.CreateClient();
        var getResponse = await anonClient.GetAsync("/api/centros-costo");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
    }
}
