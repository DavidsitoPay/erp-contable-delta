using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using Npgsql;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ParametrosRetencionControllerTests
{
    private const string Ruta = "/api/retenciones/parametros";
    private const int IdInexistente = 2_000_000_000;
    private const string SqlIdSembrado = "SELECT id FROM parametroretencion WHERE codigo = 'ISR_PROVEEDOR_ROS' AND vigente_desde = DATE '2014-01-01'";
    private static readonly string[] CodigosIvaCliente = { "IVA_CLIENTE_AGENTE_PRIVADO", "IVA_CLIENTE_SECTOR_PUBLICO" };

    private readonly PostgresFixture _pg;

    public ParametrosRetencionControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static string CodigoNuevo() => $"X{TestData.Sufijo().ToUpperInvariant()}";

    private static Dictionary<string, object?> Cuerpo(string codigo, string desde = "2030-01-01", decimal tasa = 5m) => new()
    {
        ["codigo"] = codigo,
        ["nombre"] = "Parámetro de prueba",
        ["concepto"] = "ISR_PROVEEDOR_ROS",
        ["tasaBase"] = tasa,
        ["tramoLimite"] = 30000m,
        ["tasaExcedente"] = 7m,
        ["umbralMinimo"] = 2500m,
        ["umbralSobre"] = "BASE",
        ["articuloLegal"] = "Decreto de prueba",
        ["vigenteDesde"] = desde,
        ["vigenteHasta"] = null,
    };

    private async Task<HttpClient> ClienteAsync(string rol) => _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(rol), rol);

    private static async Task<JsonElement> ListarAsync(HttpClient client, string consulta) =>
        await client.GetFromJsonAsync<JsonElement>($"{Ruta}{consulta}");

    [Theory]
    [InlineData(false, HttpStatusCode.Created)]
    [InlineData(true, HttpStatusCode.BadRequest)]
    public async Task Crear_IsrNoResidente_SoloAceptaTasaSinTramo(bool conTramo, HttpStatusCode esperado)
    {
        var client = await ClienteAsync(Roles.Administrador);
        var cuerpo = Cuerpo(CodigoNuevo());
        cuerpo["concepto"] = "ISR_NO_RESIDENTE";
        if (!conTramo)
        {
            cuerpo["tramoLimite"] = null;
            cuerpo["tasaExcedente"] = null;
        }

        var response = await client.PostAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(esperado, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ComoAdministrador_Responde201AuditaYSeListaConLaNotaReferencial()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var client = _pg.CreateApiClient(usuario, Roles.Administrador);
        var codigo = CodigoNuevo();

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(codigo));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var creado = await response.LeerJsonAsync();
        Assert.Equal((codigo, false, true, "Referencial, validar con asesor"), (
            creado.GetProperty("codigo").GetString(), creado.GetProperty("enUso").GetBoolean(), creado.GetProperty("activo").GetBoolean(),
            creado.GetProperty("nota").GetString()));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(usuario, "crear_parametro_retencion"));
        var lista = await ListarAsync(client, $"?codigo={codigo}");
        Assert.Equal(creado.GetProperty("id").GetInt32(), Assert.Single(lista.EnumerateArray()).GetProperty("id").GetInt32());
        var obtenido = await client.GetAsync($"{Ruta}/{creado.GetProperty("id").GetInt32()}");
        Assert.Equal(HttpStatusCode.OK, obtenido.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Contador)]
    [InlineData(Roles.Vendedor)]
    public async Task Crear_SinSerAdministrador_Responde403(string rol)
    {
        var client = await ClienteAsync(rol);

        var response = await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listar_SegunElPerfil_ContadorLeeYVendedorYAnonimoNo()
    {
        var contador = await ClienteAsync(Roles.Contador);
        var vendedor = await ClienteAsync(Roles.Vendedor);

        var lista = await contador.GetAsync(Ruta);
        var prohibido = await vendedor.GetAsync(Ruta);
        var anonimo = await _pg.Factory.CreateClient().GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
    }

    [Fact]
    public async Task Listar_ConFiltros_DevuelveLosParametrosSembrados()
    {
        var client = await ClienteAsync(Roles.Contador);

        var plazo = await ListarAsync(client, "?codigo=PLAZO_CONSTANCIA_ISR");
        var iva = await ListarAsync(client, "?concepto=IVA_RETENIDO_CLIENTE&vigenteEn=2025-01-01");
        var futuro = await ListarAsync(client, "?codigo=IVA_CLIENTE_SECTOR_PUBLICO&vigenteEn=2000-01-01");

        var fila = Assert.Single(plazo.EnumerateArray());
        Assert.Equal((5, JsonValueKind.Null, "Referencial, validar con asesor"), (
            fila.GetProperty("diaLimite").GetInt32(), fila.GetProperty("tasaBase").ValueKind, fila.GetProperty("nota").GetString()));
        Assert.Equal(
            CodigosIvaCliente,
            iva.EnumerateArray().Select(p => p.GetProperty("codigo").GetString()).Order().ToArray());
        Assert.Empty(futuro.EnumerateArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Crear_ConValoresInvalidos_Responde400(int caso)
    {
        var client = await ClienteAsync(Roles.Administrador);
        var cuerpo = Cuerpo(CodigoNuevo());
        switch (caso)
        {
            case 0:
                cuerpo["tasaExcedente"] = null;
                break;
            case 1:
                cuerpo["tasaBase"] = 0m;
                break;
            case 2:
                cuerpo["concepto"] = "OTRO";
                break;
            default:
                cuerpo["vigenteHasta"] = "2029-12-31";
                break;
        }

        var response = await client.PostAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConVigenciaTraslapada_Responde409()
    {
        var client = await ClienteAsync(Roles.Administrador);
        var codigo = CodigoNuevo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, Cuerpo(codigo, "2030-01-01"))).StatusCode);

        var traslapada = await client.PostAsJsonAsync(Ruta, Cuerpo(codigo, "2031-01-01"));

        Assert.Equal(HttpStatusCode.Conflict, traslapada.StatusCode);
        Assert.Contains("se traslapa", await traslapada.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConLaMismaFechaDeInicio_Responde409()
    {
        var client = await ClienteAsync(Roles.Administrador);
        var codigo = CodigoNuevo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, Cuerpo(codigo))).StatusCode);

        var repetida = await client.PostAsJsonAsync(Ruta, Cuerpo(codigo));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("Ya existe una versión de ese parámetro de retención con la misma fecha de inicio.", await repetida.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_SoloCambiaNotaActivoYVigenciaFinal()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var client = _pg.CreateApiClient(usuario, Roles.Administrador);
        var creado = await (await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()))).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"{Ruta}/{id}", new { nota = "Validado por el asesor", activo = false, vigenteHasta = "2030-12-31", tasaBase = 99m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cuerpo = await response.LeerJsonAsync();
        Assert.Equal(("Validado por el asesor", false, "2030-12-31", 5m), (
            cuerpo.GetProperty("nota").GetString(), cuerpo.GetProperty("activo").GetBoolean(),
            cuerpo.GetProperty("vigenteHasta").GetString(), cuerpo.GetProperty("tasaBase").GetDecimal()));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(usuario, "actualizar_parametro_retencion"));
        var activos = await ListarAsync(client, $"?codigo={creado.GetProperty("codigo").GetString()}");
        var todos = await ListarAsync(client, $"?codigo={creado.GetProperty("codigo").GetString()}&incluirInactivos=true");
        Assert.Empty(activos.EnumerateArray());
        Assert.Single(todos.EnumerateArray());
    }

    [Theory]
    [InlineData("2029-12-31", HttpStatusCode.BadRequest)]
    [InlineData(null, HttpStatusCode.OK)]
    public async Task Actualizar_ValidaLaVigenciaFinalContraLaInicial(string? vigenteHasta, HttpStatusCode esperado)
    {
        var client = await ClienteAsync(Roles.Administrador);
        var creado = await (await client.PostAsJsonAsync(Ruta, Cuerpo(CodigoNuevo()))).LeerJsonAsync();

        var response = await client.PutAsJsonAsync($"{Ruta}/{creado.GetProperty("id").GetInt32()}", new { nota = (string?)null, activo = (bool?)null, vigenteHasta });

        Assert.Equal(esperado, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ConPerfilOIdInvalidos_Responde403Y404()
    {
        var contador = await ClienteAsync(Roles.Contador);
        var administrador = await ClienteAsync(Roles.Administrador);
        var cambios = new { nota = "x", activo = true, vigenteHasta = (string?)null };

        var prohibido = await contador.PutAsJsonAsync($"{Ruta}/{IdInexistente}", cambios);
        var inexistente = await administrador.PutAsJsonAsync($"{Ruta}/{IdInexistente}", cambios);
        var obtener = await administrador.GetAsync($"{Ruta}/{IdInexistente}");

        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, obtener.StatusCode);
    }

    [Fact]
    public async Task ParametroUsado_NoPermiteCambiarSuTasaNiCerrarLaVigenciaAntesDeLaUltimaFactura()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        var administrador = await ClienteAsync(Roles.Administrador);
        var id = await _pg.Data.ScalarAsync<int>(SqlIdSembrado);

        var cierreAnterior = await administrador.PutAsJsonAsync($"{Ruta}/{id}", new { nota = "x", activo = true, vigenteHasta = "2025-03-14" });
        var cambioDeTasa = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE parametroretencion SET tasa_base = 6 WHERE id = $1", id));
        var lista = await ListarAsync(administrador, "?codigo=ISR_PROVEEDOR_ROS&vigenteEn=2025-03-15");

        Assert.Equal(HttpStatusCode.Conflict, cierreAnterior.StatusCode);
        Assert.Contains("No se puede cerrar la vigencia", await cierreAnterior.LeerErrorAsync());
        Assert.Equal("55000", cambioDeTasa.SqlState);
        Assert.True(lista.EnumerateArray().Single().GetProperty("enUso").GetBoolean());
    }

    [Fact]
    public async Task NuevaVersion_NoAlteraLasRetencionesRegistradasYSeUsaDesdeSuVigencia()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var anterior = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        var administrador = await ClienteAsync(Roles.Administrador);
        var id = await _pg.Data.ScalarAsync<int>(SqlIdSembrado);
        var periodo = await _pg.Data.CrearPeriodoEnRangoAsync(new DateOnly(3000, 1, 1), new DateOnly(3000, 12, 31));
        var nueva = Cuerpo("ISR_PROVEEDOR_ROS", "3000-01-01", 6m);

        var cierre = await administrador.PutAsJsonAsync($"{Ruta}/{id}", new { nota = "Reforma", activo = true, vigenteHasta = "2999-12-31" });
        var alta = await administrador.PostAsJsonAsync(Ruta, nueva);
        var posterior = await client.RegistrarCompraAsync(
            CompraExenta(e with { PeriodoId = periodo }, proveedor, 10000m).EnFecha(new DateOnly(3000, 3, 15)));

        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        Assert.Equal(500m, anterior.Cuerpo.GetProperty("retencionIsr").GetProperty("monto").GetDecimal());
        Assert.Equal((600m, 6m), (
            posterior.Cuerpo.GetProperty("retencionIsr").GetProperty("monto").GetDecimal(),
            posterior.Cuerpo.GetProperty("retencionIsr").GetProperty("tasa").GetDecimal()));
        Assert.Equal(5m, await _pg.Data.ScalarAsync<decimal>("SELECT tasa FROM retencion WHERE documento_cxp_id = $1", anterior.FacturaId));
    }
}
