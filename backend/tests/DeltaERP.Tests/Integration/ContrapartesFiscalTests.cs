using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ContrapartesFiscalTests
{
    private const string Ruta = "/api/contrapartes";

    private readonly PostgresFixture _pg;

    public ContrapartesFiscalTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static Dictionary<string, object?> Cuerpo(string tipo, string? nit, string regimenIva = "GENERAL", bool esResidente = true) => new()
    {
        ["tipo"] = tipo,
        ["nombre"] = $"{tipo} fiscal {TestData.Sufijo()}",
        ["nit"] = nit,
        ["regimenIva"] = regimenIva,
        ["regimenIsr"] = "SIMPLIFICADO",
        ["esResidente"] = esResidente,
        ["esAgenteRetencionIva"] = true,
    };

    private async Task<HttpClient> ClienteAsync() => _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

    [Fact]
    public async Task Crear_ConDatosFiscales_LosPersisteYNormalizaElNit()
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo("Proveedor", " 12345679 ", "PEQUENO_CONTRIBUYENTE", esResidente: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        var guardada = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        Assert.Equal(("1234567-9", "PEQUENO_CONTRIBUYENTE", "SIMPLIFICADO", false, true), (
            guardada.GetProperty("nit").GetString(),
            guardada.GetProperty("regimenIva").GetString(),
            guardada.GetProperty("regimenIsr").GetString(),
            guardada.GetProperty("esResidente").GetBoolean(),
            guardada.GetProperty("esAgenteRetencionIva").GetBoolean()));
    }

    [Fact]
    public async Task Crear_SinCamposFiscales_UsaLosValoresPorDefecto()
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, new { tipo = "Cliente", nombre = "Consumidor", nit = " " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("nit").ValueKind);
        Assert.Equal(("GENERAL", "UTILIDADES", true, false), (
            body.GetProperty("regimenIva").GetString(),
            body.GetProperty("regimenIsr").GetString(),
            body.GetProperty("esResidente").GetBoolean(),
            body.GetProperty("esAgenteRetencionIva").GetBoolean()));
    }

    [Theory]
    [InlineData("Proveedor", null, "GENERAL", "El NIT del proveedor es obligatorio.")]
    [InlineData("Proveedor", "CF", "GENERAL", "CF solo es válido para clientes.")]
    [InlineData("Cliente", "1234567-8", "GENERAL", "El NIT no es válido (dígito verificador incorrecto).")]
    [InlineData("Cliente", "CF", "OTRO", "Régimen de IVA inválido. Debe ser uno de: GENERAL, PEQUENO_CONTRIBUYENTE, EXENTO.")]
    public async Task Crear_ConDatosFiscalesInvalidos_Responde400(string tipo, string? nit, string regimenIva, string mensaje)
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(tipo, nit, regimenIva));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(mensaje, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ClienteConConsumidorFinal_GuardaCf()
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo("Cliente", "c/f"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("CF", (await response.LeerJsonAsync()).GetProperty("nit").GetString());
    }

    [Fact]
    public async Task Actualizar_ValidaConElTipoGuardadoYReemplazaLosCamposFiscales()
    {
        var id = await _pg.Data.CrearContraparteAsync("Proveedor");
        var client = await ClienteAsync();

        var sinNit = await client.PutAsJsonAsync($"{Ruta}/{id}", Cuerpo("Cliente", null));
        var completo = await client.PutAsJsonAsync($"{Ruta}/{id}", Cuerpo("Cliente", "6-K", "EXENTO"));
        var reemplazo = await client.PutAsJsonAsync($"{Ruta}/{id}", new { tipo = "Proveedor", nombre = "Sin campos fiscales", nit = "1234567-9" });

        Assert.Equal(HttpStatusCode.BadRequest, sinNit.StatusCode);
        Assert.Equal("El NIT del proveedor es obligatorio.", await sinNit.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.OK, completo.StatusCode);
        Assert.Equal(("Proveedor", "6-K", "EXENTO"), (
            (await completo.LeerJsonAsync()).GetProperty("tipo").GetString(),
            (await _pg.Data.ScalarAsync<string>("SELECT nit FROM contraparte WHERE id = $1", id)),
            (await _pg.Data.ScalarAsync<string>("SELECT regimen_iva FROM contraparte WHERE id = $1", id))));
        Assert.Equal(HttpStatusCode.OK, reemplazo.StatusCode);
        Assert.Equal("GENERAL", (await reemplazo.LeerJsonAsync()).GetProperty("regimenIva").GetString());
    }
}
