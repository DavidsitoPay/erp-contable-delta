using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ImpuestosControllerTests
{
    private const string Ruta = "/api/impuestos";
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public ImpuestosControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static string CodigoNuevo() => $"X{TestData.Sufijo().ToUpperInvariant()}";

    private static Dictionary<string, object?> Cuerpo(string codigo, string desde = "2030-01-01", string tipo = "IVA_GENERAL", decimal tasa = 12m) => new()
    {
        ["codigo"] = codigo,
        ["nombre"] = "Impuesto de prueba",
        ["tipo"] = tipo,
        ["tasa"] = tasa,
        ["aplicaA"] = "AMBOS",
        ["articuloLegal"] = "Decreto de prueba",
        ["vigenteDesde"] = desde,
        ["vigenteHasta"] = null,
    };

    private static Dictionary<string, object?> CuerpoDesde(JsonElement impuesto) => new()
    {
        ["codigo"] = impuesto.GetProperty("codigo").GetString(),
        ["nombre"] = impuesto.GetProperty("nombre").GetString(),
        ["tipo"] = impuesto.GetProperty("tipo").GetString(),
        ["tasa"] = impuesto.GetProperty("tasa").GetDecimal(),
        ["aplicaA"] = impuesto.GetProperty("aplicaA").GetString(),
        ["articuloLegal"] = impuesto.GetProperty("articuloLegal").GetString(),
        ["vigenteDesde"] = impuesto.GetProperty("vigenteDesde").GetString(),
        ["vigenteHasta"] = null,
    };

    private async Task<HttpClient> ClienteAsync(string rol = Roles.Contador) =>
        _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), rol);

    private async Task<HashSet<string?>> CodigosAsync(HttpClient client, string consulta)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"{Ruta}{consulta}");
        return body.EnumerateArray().Select(i => i.GetProperty("codigo").GetString()).ToHashSet();
    }

    [Fact]
    public async Task Listar_PorDefecto_DevuelveElCatalogoSembradoSinHistoricos()
    {
        var client = await ClienteAsync(Roles.Vendedor);

        var body = await client.GetFromJsonAsync<JsonElement>(Ruta);

        var codigos = body.EnumerateArray().Select(i => i.GetProperty("codigo").GetString()).ToHashSet();
        Assert.Superset(new HashSet<string?> { "IVA_GENERAL", "EXENTO_EXPORTACION", "NO_AFECTO", "PEQUENO_CONTRIBUYENTE" }, codigos);
        Assert.All(body.EnumerateArray(), i => Assert.NotEqual("LEGADO", i.GetProperty("tipo").GetString()));
    }

    [Theory]
    [InlineData("?aplicaA=VENTAS", "IVA_GENERAL", "PEQUENO_CONTRIBUYENTE")]
    [InlineData("?vigenteEn=2000-01-01", "EXENTO_EXPORTACION", "IVA_GENERAL")]
    [InlineData("?tipo=EXENTO", "EXENTO_EXPORTACION", "IVA_GENERAL")]
    public async Task Listar_ConFiltros_RespetaAmbitoVigenciaYTipo(string consulta, string presente, string ausente)
    {
        var client = await ClienteAsync();

        var codigos = await CodigosAsync(client, consulta);

        Assert.Contains(presente, codigos);
        Assert.DoesNotContain(ausente, codigos);
    }

    [Fact]
    public async Task Listar_ConImpuestoInactivo_SoloLoIncluyeSiSePide()
    {
        var inactivoId = await _pg.Data.CrearImpuestoAsync(new OpcionesImpuesto(Activo: false));
        var client = await ClienteAsync();

        var porDefecto = (await client.GetFromJsonAsync<JsonElement>(Ruta)).Ids();
        var conInactivos = (await client.GetFromJsonAsync<JsonElement>($"{Ruta}?incluirInactivos=true")).Ids();

        Assert.DoesNotContain(inactivoId, porDefecto);
        Assert.Contains(inactivoId, conInactivos);
    }

    [Fact]
    public async Task Obtener_DevuelveElImpuestoOResponde404()
    {
        var id = await _pg.Data.CrearImpuestoAsync();
        var client = await ClienteAsync();

        var response = await client.GetAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal((id, false), (body.GetProperty("id").GetInt32(), body.GetProperty("enUso").GetBoolean()));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Ruta}/{IdInexistente}")).StatusCode);
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201ConElCreditoPorDefecto()
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.LeerJsonAsync();
        Assert.Equal((true, true, false), (body.GetProperty("generaCredito").GetBoolean(), body.GetProperty("activo").GetBoolean(), body.GetProperty("enUso").GetBoolean()));
    }

    [Fact]
    public async Task Crear_ComoVendedor_Responde403()
    {
        var client = await ClienteAsync(Roles.Vendedor);

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConTipoHistorico_Responde400()
    {
        var client = await ClienteAsync();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo(), tipo: "LEGADO"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("Tipo de impuesto inválido.", await response.LeerErrorAsync());
    }

    [Theory]
    [InlineData("2030-01-01", "misma fecha de inicio")]
    [InlineData("2031-01-01", "se traslapa")]
    public async Task Crear_ConVersionDuplicadaOTraslapada_Responde409(string desdeSegunda, string fragmento)
    {
        var client = await ClienteAsync();
        var codigo = CodigoNuevo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, Cuerpo(codigo))).StatusCode);

        var segunda = await client.PostAsJsonAsync(Ruta, Cuerpo(codigo, desdeSegunda));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Contains(fragmento, await segunda.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_SinUso_PermiteCambiarLaTasaYRespondeErroresDeCuerpoOId()
    {
        var client = await ClienteAsync();
        var creado = await (await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()))).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();
        var cuerpo = CuerpoDesde(creado);
        cuerpo["tasa"] = 13m;
        cuerpo["nombre"] = "Nombre editado";

        var response = await client.PutAsJsonAsync($"{Ruta}/{id}", cuerpo);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal((13m, "Nombre editado"), (body.GetProperty("tasa").GetDecimal(), body.GetProperty("nombre").GetString()));
        cuerpo["tipo"] = "OTRO";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Ruta}/{id}", cuerpo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Ruta}/{IdInexistente}", cuerpo)).StatusCode);
    }

    [Fact]
    public async Task Actualizar_ConImpuestoEnUso_BloqueaTasaYCierreAnteriorPeroPermiteCerrarDespues()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var id = await _pg.Data.CrearImpuestoAsync();
        var factura = FacturaPayloads.Cxc(e, $"F-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(id, e.CuentaIngreso) });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/cxc/facturas", factura)).StatusCode);
        var cuerpo = CuerpoDesde(await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}"));

        var cambioTasa = await client.PutAsJsonAsync($"{Ruta}/{id}", new Dictionary<string, object?>(cuerpo) { ["tasa"] = 13m });
        var cierreAnterior = await client.PutAsJsonAsync($"{Ruta}/{id}", new Dictionary<string, object?>(cuerpo) { ["vigenteHasta"] = "2025-03-14" });
        var cierreValido = await client.PutAsJsonAsync($"{Ruta}/{id}", new Dictionary<string, object?>(cuerpo) { ["vigenteHasta"] = "2025-12-31" });

        Assert.Equal(HttpStatusCode.Conflict, cambioTasa.StatusCode);
        Assert.Contains("ya fue usado", await cambioTasa.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.Conflict, cierreAnterior.StatusCode);
        Assert.Contains("No se puede cerrar la vigencia", await cierreAnterior.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.OK, cierreValido.StatusCode);
        var body = await cierreValido.LeerJsonAsync();
        Assert.Equal(("2025-12-31", true), (body.GetProperty("vigenteHasta").GetString(), body.GetProperty("enUso").GetBoolean()));
    }

    [Fact]
    public async Task Desactivar_OcultaElImpuestoYLaFacturaLoRechaza()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var id = await _pg.Data.CrearImpuestoAsync();

        var response = await client.DeleteAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}")).GetProperty("activo").GetBoolean());
        var factura = FacturaPayloads.Cxc(e, $"F-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(id, e.CuentaIngreso) });
        var rechazo = await client.PostAsJsonAsync("/api/cxc/facturas", factura);
        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        Assert.Contains("está inactivo", await rechazo.LeerErrorAsync());
    }

    [Fact]
    public async Task Desactivar_ConIdInexistenteOComoVendedor_Responde404Y403()
    {
        var contador = await ClienteAsync();
        var vendedor = await ClienteAsync(Roles.Vendedor);

        Assert.Equal(HttpStatusCode.NotFound, (await contador.DeleteAsync($"{Ruta}/{IdInexistente}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.DeleteAsync($"{Ruta}/{IdInexistente}")).StatusCode);
    }
}
