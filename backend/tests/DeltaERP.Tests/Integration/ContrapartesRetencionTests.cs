using System.Net;
using System.Net.Http.Json;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ContrapartesRetencionTests
{
    private const string Ruta = "/api/contrapartes";

    private readonly PostgresFixture _pg;

    public ContrapartesRetencionTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static Dictionary<string, object?> Proveedor(string regimenIsr, string? fundamento = null) => new()
    {
        ["tipo"] = "Proveedor",
        ["nombre"] = $"Proveedor {TestData.Sufijo()}",
        ["nit"] = TestData.NitValido,
        ["regimenIva"] = "GENERAL",
        ["regimenIsr"] = regimenIsr,
        ["isrFundamento"] = fundamento,
        ["esResidente"] = true,
        ["esAgenteRetencionIva"] = false,
    };

    private async Task<(HttpClient Client, int UsuarioId)> ClienteAsync()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();
        return (_pg.CreateApiClient(usuario), usuario);
    }

    [Theory]
    [InlineData("EXENTO")]
    [InlineData("PAGO_DIRECTO")]
    public async Task Crear_ProveedorExentoOPagoDirectoSinFundamento_Responde400(string regimen)
    {
        var (client, _) = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Proveedor(regimen));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("exige el fundamento legal", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ProveedorPagoDirectoConFundamento_LoGuardaRecortado()
    {
        var (client, _) = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Proveedor("PAGO_DIRECTO", "  Resolución SAT-123  "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuerpo = await response.LeerJsonAsync();
        Assert.Equal(("PAGO_DIRECTO", "Resolución SAT-123"), (cuerpo.GetProperty("regimenIsr").GetString(), cuerpo.GetProperty("isrFundamento").GetString()));
    }

    [Fact]
    public async Task Crear_ConFundamentoEnUnRegimenQueNoLoExige_LoDescarta()
    {
        var (client, _) = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Proveedor("SIMPLIFICADO", "Dato sobrante"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await response.LeerJsonAsync()).GetProperty("isrFundamento").ValueKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("OTRO")]
    public async Task Crear_ClienteAgenteSinTipoValido_Responde400(string? tipoAgente)
    {
        var (client, _) = await ClienteAsync();
        var cuerpo = Proveedor("NO_DEFINIDO").Con("tipo", "Cliente").Con("nit", null).Con("esAgenteRetencionIva", true).Con("tipoAgenteIva", tipoAgente);

        var response = await client.PostAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("debe indicar el tipo de agente", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_CambiaElRegimenYElTipoDeAgenteYAudita()
    {
        var (client, usuario) = await ClienteAsync();
        var id = await _pg.Data.CrearContraparteAsync("Proveedor");

        var response = await client.PutAsJsonAsync(
            $"{Ruta}/{id}",
            Proveedor("EXENTO", "Art. 8 LISR").Con("esAgenteRetencionIva", true).Con("tipoAgenteIva", "SECTOR_PUBLICO"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("EXENTO", "Art. 8 LISR", "SECTOR_PUBLICO"), (
            await _pg.Data.ScalarAsync<string>("SELECT regimen_isr FROM contraparte WHERE id = $1", id),
            await _pg.Data.ScalarAsync<string>("SELECT isr_fundamento FROM contraparte WHERE id = $1", id),
            await _pg.Data.ScalarAsync<string>("SELECT tipo_agente_iva FROM contraparte WHERE id = $1", id)));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(usuario, "actualizar_contraparte"));

        var sinAgente = await client.PutAsJsonAsync($"{Ruta}/{id}", Proveedor("UTILIDADES", "Dato sobrante").Con("tipoAgenteIva", "SECTOR_PUBLICO"));

        Assert.Equal(HttpStatusCode.OK, sinAgente.StatusCode);
        Assert.Equal(
            (0L, 0L),
            (await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM contraparte WHERE id = $1 AND isr_fundamento IS NOT NULL", id),
                await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM contraparte WHERE id = $1 AND tipo_agente_iva IS NOT NULL", id)));
    }
}
