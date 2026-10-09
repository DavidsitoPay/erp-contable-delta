using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ContrapartesControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private readonly PostgresFixture _pg;

    public ContrapartesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Payload(string tipo, string nombre, string? nit = null, string? direccion = null) =>
        new { tipo, nombre, nit, direccion };

    [Fact]
    public async Task Listar_FiltraPorTipoEIgnoraFiltroEnBlanco()
    {
        var cliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var proveedor = await _pg.Data.CrearContraparteAsync("Proveedor");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var responseCliente = await client.GetAsync("/api/contrapartes?tipo=Cliente");
        Assert.Equal(HttpStatusCode.OK, responseCliente.StatusCode);
        var bodyCliente = await responseCliente.LeerJsonAsync();
        var idsCliente = bodyCliente.Ids();
        Assert.Contains(cliente, idsCliente);
        Assert.DoesNotContain(proveedor, idsCliente);
        foreach (var elem in bodyCliente.EnumerateArray())
        {
            Assert.Equal("Cliente", elem.GetProperty("tipo").GetString());
        }

        var responseProveedor = await client.GetAsync("/api/contrapartes?tipo=Proveedor");
        Assert.Equal(HttpStatusCode.OK, responseProveedor.StatusCode);
        var bodyProveedor = await responseProveedor.LeerJsonAsync();
        var idsProveedor = bodyProveedor.Ids();
        Assert.Contains(proveedor, idsProveedor);
        Assert.DoesNotContain(cliente, idsProveedor);

        var responseVacio = await client.GetAsync("/api/contrapartes?tipo=");
        Assert.Equal(HttpStatusCode.OK, responseVacio.StatusCode);
        var bodyVacio = await responseVacio.LeerJsonAsync();
        var idsVacio = bodyVacio.Ids();
        Assert.Contains(cliente, idsVacio);
        Assert.Contains(proveedor, idsVacio);

        var responseSinFiltro = await client.GetAsync("/api/contrapartes");
        Assert.Equal(HttpStatusCode.OK, responseSinFiltro.StatusCode);
        var bodySinFiltro = await responseSinFiltro.LeerJsonAsync();
        var idsSinFiltro = bodySinFiltro.Ids();
        Assert.Contains(cliente, idsSinFiltro);
        Assert.Contains(proveedor, idsSinFiltro);
    }

    [Fact]
    public async Task Obtener_DevuelveLaContraparteOResponde404()
    {
        var id = await _pg.Data.CrearContraparteAsync("Cliente");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.GetAsync($"/api/contrapartes/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(id, body.GetProperty("id").GetInt32());

        var response404 = await client.GetAsync($"/api/contrapartes/{IdInexistente}");
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PostAsJsonAsync("/api/contrapartes", Payload("Cliente", "Cliente nuevo", "1234567-9", "Zona 1"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("1234567-9", body.GetProperty("nit").GetString());
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_Responde400()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var responseTipoInvalido = await client.PostAsJsonAsync("/api/contrapartes", Payload("Otro", "X"));
        Assert.Equal(HttpStatusCode.BadRequest, responseTipoInvalido.StatusCode);
        var errorTipo = await responseTipoInvalido.LeerErrorAsync();
        Assert.Contains("Tipo inválido", errorTipo);

        var responseNombreVacio = await client.PostAsJsonAsync("/api/contrapartes", Payload("Cliente", ""));
        Assert.Equal(HttpStatusCode.BadRequest, responseNombreVacio.StatusCode);
    }

    [Fact]
    public async Task Actualizar_CambiaContactoPeroNoElTipo()
    {
        var id = await _pg.Data.CrearContraparteAsync("Cliente");
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var response = await client.PutAsJsonAsync($"/api/contrapartes/{id}", Payload("Proveedor", "Nombre nuevo", "1234567-9", "Dir nueva"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Cliente", body.GetProperty("tipo").GetString());
        Assert.Equal("Nombre nuevo", body.GetProperty("nombre").GetString());
        Assert.Equal("1234567-9", body.GetProperty("nit").GetString());
        Assert.Equal("Dir nueva", body.GetProperty("direccion").GetString());

        var responseNombreVacio = await client.PutAsJsonAsync($"/api/contrapartes/{id}", Payload("Cliente", ""));
        Assert.Equal(HttpStatusCode.BadRequest, responseNombreVacio.StatusCode);

        var response404 = await client.PutAsJsonAsync($"/api/contrapartes/{IdInexistente}", Payload("Cliente", "Nombre"));
        Assert.Equal(HttpStatusCode.NotFound, response404.StatusCode);
    }

    [Fact]
    public async Task Escritura_ConVendedorOAnonimo_Responde403Y401()
    {
        var vendedorClient = _pg.CreateApiClient(0, Roles.Vendedor);

        var postResponse = await vendedorClient.PostAsJsonAsync("/api/contrapartes", Payload("Cliente", "Test"));
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        var putResponse = await vendedorClient.PutAsJsonAsync($"/api/contrapartes/{IdInexistente}", Payload("Cliente", "Test"));
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);

        var anonClient = _pg.Factory.CreateClient();
        var getResponse = await anonClient.GetAsync("/api/contrapartes");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
    }
}
