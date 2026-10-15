using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Rules;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class TiposCambioControllerTests
{
    private const string Ruta = "/api/tipos-cambio";
    private const int CodigoUsd = 2;
    private const int CodigoEur = 24;
    private const int IdInexistente = 2_000_000_000;
    private const string MensajeFechaFutura = "No hay tipo de cambio para fechas futuras.";
    private const string MensajeBanguatCaido = "Banguat no respondió; capture la tasa manual o reintente.";
    private const string MensajeOficialExistente = "Ya existe el tipo de cambio oficial de Banguat para esa fecha.";
    private const string MensajeYaUsada = "ya fue usada";
    private const string Capturar = "capturar_tipo_cambio_manual";

    private readonly PostgresFixture _pg;

    public TiposCambioControllerTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private static string Consulta(string moneda, DateOnly fecha) => $"{Ruta}?moneda={moneda}&fecha={fecha.Dia()}";

    private static object Manual(DateOnly fecha, decimal tasa = 7.65m, string motivo = "Banguat sin servicio", string moneda = "USD") =>
        new { monedaCodigo = moneda, fecha, tasa, motivo };

    private static async Task<int> CapturarAsync(HttpClient client, object cuerpo)
    {
        var response = await client.PostAsJsonAsync($"{Ruta}/manual", cuerpo);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    private async Task<HttpClient> ContadorAsync() => _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

    [Fact]
    public async Task Consultar_UsdDiaHabil_DevuelveLaReferenciaDeBanguat()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m);

        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", miercoles));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("disponible").GetBoolean());
        var tc = body.GetProperty("tipoCambio");
        Assert.Equal(7.64157m, tc.GetProperty("tasa").GetDecimal());
        Assert.Equal("Banguat", tc.GetProperty("fuente").GetString());
        Assert.False(tc.GetProperty("arrastrado").GetBoolean());
        Assert.Equal(miercoles.Dia(), tc.GetProperty("fechaReferencia").GetString());
        Assert.Equal(JsonValueKind.Null, tc.GetProperty("cotizacionVenta").ValueKind);
        Assert.Equal(1L, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM historialtipocambio WHERE fecha = $1 AND activa AND moneda_id = (SELECT id FROM moneda WHERE codigo = 'USD')", miercoles));
    }

    [Fact]
    public async Task Consultar_SegundaVez_UsaLaCacheSinLlamarABanguat()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m);
        var client = await ContadorAsync();

        await client.GetAsync(Consulta("USD", miercoles));
        await client.GetAsync(Consulta("USD", miercoles));

        Assert.Equal(1, _pg.Banguat.Llamadas);
    }

    [Fact]
    public async Task Consultar_UsdSabadoConFilaPropia_EsArrastradoConReferenciaDelViernes()
    {
        var viernes = TestData.SiguienteSemana().AddDays(4);
        var sabado = viernes.AddDays(1);
        _pg.Banguat.Sembrar(CodigoUsd, viernes, 7.64136m);
        _pg.Banguat.Sembrar(CodigoUsd, sabado, 7.64136m);

        var tc = await ConsultarTipoCambioAsync("USD", sabado);

        Assert.True(tc.GetProperty("arrastrado").GetBoolean());
        Assert.Equal(viernes.Dia(), tc.GetProperty("fechaReferencia").GetString());
        Assert.Equal(7.64136m, tc.GetProperty("tasa").GetDecimal());
    }

    [Fact]
    public async Task Consultar_UsdSabadoSinFila_UsaLaUltimaAnterior()
    {
        var viernes = TestData.SiguienteSemana().AddDays(4);
        _pg.Banguat.Sembrar(CodigoUsd, viernes, 7.64136m);

        var tc = await ConsultarTipoCambioAsync("USD", viernes.AddDays(1));

        Assert.True(tc.GetProperty("arrastrado").GetBoolean());
        Assert.Equal(viernes.Dia(), tc.GetProperty("fechaReferencia").GetString());
        Assert.Equal(7.64136m, tc.GetProperty("tasa").GetDecimal());
    }

    [Fact]
    public async Task Consultar_EurDiaHabil_DerivaDeVentaPorReferenciaUsd()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m, 7.64157m);
        _pg.Banguat.Sembrar(CodigoEur, miercoles, 1.1197m, 1.1196m);

        var tc = await ConsultarTipoCambioAsync("EUR", miercoles);

        Assert.Equal(8.556266m, tc.GetProperty("tasa").GetDecimal());
        Assert.Equal(1.1197m, tc.GetProperty("cotizacionVenta").GetDecimal());
        Assert.Equal(1.1196m, tc.GetProperty("cotizacionCompra").GetDecimal());
        Assert.Equal(7.64157m, tc.GetProperty("referenciaUsd").GetDecimal());
        Assert.False(tc.GetProperty("arrastrado").GetBoolean());
        Assert.Equal(2, _pg.Banguat.Llamadas);
    }

    [Fact]
    public async Task Consultar_EurSabado_UsaLaUltimaFilaEurYMarcaArrastrado()
    {
        var viernes = TestData.SiguienteSemana().AddDays(4);
        var sabado = viernes.AddDays(1);
        _pg.Banguat.Sembrar(CodigoUsd, viernes, 7.64157m);
        _pg.Banguat.Sembrar(CodigoUsd, sabado, 7.64157m);
        _pg.Banguat.Sembrar(CodigoEur, viernes, 1.1197m);

        var tc = await ConsultarTipoCambioAsync("EUR", sabado);

        Assert.Equal(8.556266m, tc.GetProperty("tasa").GetDecimal());
        Assert.True(tc.GetProperty("arrastrado").GetBoolean());
        Assert.Equal(viernes.Dia(), tc.GetProperty("fechaReferencia").GetString());
    }

    [Fact]
    public async Task Consultar_FechaFutura_Responde409()
    {
        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MensajeFechaFutura, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Consultar_BanguatCaidoConValorPrevio_OfreceLaPropuesta()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var previa = miercoles.AddDays(-3);
        await _pg.Data.SembrarTipoCambioAsync("USD", previa, 7.60m);
        _pg.Banguat.Falla = true;

        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", miercoles));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.False(body.GetProperty("disponible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("tipoCambio").ValueKind);
        Assert.Equal(7.60m, body.GetProperty("propuesta").GetProperty("tasa").GetDecimal());
        Assert.Equal(previa.Dia(), body.GetProperty("propuesta").GetProperty("fecha").GetString());
        Assert.Equal(MensajeBanguatCaido, body.GetProperty("mensaje").GetString());
    }

    [Fact]
    public async Task Consultar_BanguatCaidoSinValorPrevio_PropuestaNula()
    {
        var fecha = new DateOnly(1800, 6, 4);
        _pg.Banguat.Falla = true;

        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", fecha));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.False(body.GetProperty("disponible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("propuesta").ValueKind);
    }

    [Fact]
    public async Task Consultar_BanguatSinDatosYSinTasaPrevia_Responde409()
    {
        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", new DateOnly(1800, 6, 4)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Banguat no tiene un tipo de cambio de USD anterior al 1800-06-04", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Consultar_BanguatSinDatosPeroConTasaPrevia_OfreceLaPropuesta()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        await _pg.Data.SembrarTipoCambioAsync("USD", miercoles.AddDays(-3), 7.60m);

        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", miercoles));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.False(body.GetProperty("disponible").GetBoolean());
        Assert.Equal(JsonValueKind.Object, body.GetProperty("propuesta").ValueKind);
    }

    [Theory]
    [InlineData("/api/tipos-cambio?fecha=2026-10-07")]
    [InlineData("/api/tipos-cambio?moneda=USD")]
    [InlineData("/api/tipos-cambio?moneda=GTQ&fecha=2026-10-07")]
    [InlineData("/api/tipos-cambio?moneda=XXX&fecha=2026-10-07")]
    public async Task Consultar_ParametrosInvalidos_Responde400(string ruta)
    {
        var response = await (await ContadorAsync()).GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Consultar_ComoVendedor_Funciona()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        await _pg.Data.SembrarTipoCambioAsync("USD", miercoles, 7.64m);
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var response = await vendedor.GetAsync(Consulta("USD", miercoles));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CapturarManual_ConMotivo_Responde201YAudita()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuario);

        var response = await client.PostAsJsonAsync($"{Ruta}/manual", Manual(miercoles));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Manual", body.GetProperty("fuente").GetString());
        Assert.Equal(7.65m, body.GetProperty("tasa").GetDecimal());
        Assert.Equal(1L, await _pg.Data.ContarAuditoriaAsync(usuario, Capturar));
        Assert.Equal(0, _pg.Banguat.Llamadas);
    }

    [Theory]
    [InlineData("", 7.65, "USD")]
    [InlineData("Banguat sin servicio", 0d, "USD")]
    [InlineData("Banguat sin servicio", 1.1234567d, "USD")]
    [InlineData("Banguat sin servicio", 100000d, "USD")]
    [InlineData("Banguat sin servicio", 7.65, "GTQ")]
    public async Task CapturarManual_DatosInvalidos_Responde400(string motivo, double tasa, string moneda)
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);

        var response = await (await ContadorAsync()).PostAsJsonAsync(
            $"{Ruta}/manual", Manual(miercoles, (decimal)tasa, motivo, moneda));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CapturarManual_ComoVendedor_Responde403()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var response = await vendedor.PostAsJsonAsync($"{Ruta}/manual", Manual(miercoles));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CapturarManual_FechaFutura_Responde409()
    {
        var response = await (await ContadorAsync()).PostAsJsonAsync(
            $"{Ruta}/manual", Manual(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MensajeFechaFutura, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task CapturarManual_ConTasaOficialDeBanguat_Responde409()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m);
        var client = await ContadorAsync();
        await client.GetAsync(Consulta("USD", miercoles));

        var response = await client.PostAsJsonAsync($"{Ruta}/manual", Manual(miercoles));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MensajeOficialExistente, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task CapturarManual_SobreManualNoUsada_ReemplazaYDejaUnaSolaActiva()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuario);
        await CapturarAsync(client, Manual(miercoles, 7.65m));

        var response = await client.PostAsJsonAsync($"{Ruta}/manual", Manual(miercoles, 7.70m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM historialtipocambio WHERE fecha = $1 AND activa AND moneda_id = (SELECT id FROM moneda WHERE codigo = 'USD')", miercoles));
        Assert.Equal(7.70m, await _pg.Data.ScalarAsync<decimal>(
            "SELECT tasa FROM historialtipocambio WHERE fecha = $1 AND activa AND moneda_id = (SELECT id FROM moneda WHERE codigo = 'USD')", miercoles));
        Assert.Contains("reemplaza la tasa", await _pg.Data.DetalleAuditoriaAsync(usuario, Capturar));
    }

    [Fact]
    public async Task TasaUsada_NoSeEditaNiSeDaDeBajaNiSeSustituye()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var id = await CapturarAsync(client, Manual(m.Miercoles, 7.60m));
        await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 100m));
        _pg.Banguat.Sembrar(CodigoUsd, m.Miercoles, 7.64157m);

        var recaptura = await client.PostAsJsonAsync($"{Ruta}/manual", Manual(m.Miercoles, 7.61m));
        Assert.Equal(HttpStatusCode.Conflict, recaptura.StatusCode);
        Assert.Contains(MensajeYaUsada, await recaptura.LeerErrorAsync());

        var baja = await client.PostAsJsonAsync($"{Ruta}/{id}/baja", new { motivo = "Error de captura" });
        Assert.Equal(HttpStatusCode.Conflict, baja.StatusCode);

        var sustitucion = await client.PostAsJsonAsync($"{Ruta}/sustituir-por-banguat", new { monedaCodigo = "USD", fecha = m.Miercoles });
        Assert.Equal(HttpStatusCode.Conflict, sustitucion.StatusCode);
        Assert.Contains(MensajeYaUsada, await sustitucion.LeerErrorAsync());
    }

    [Fact]
    public async Task Sustituir_ManualNoUsada_ReemplazaPorLaDeBanguat()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuario);
        var manual = await CapturarAsync(client, Manual(miercoles));
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m);

        var response = await client.PostAsJsonAsync($"{Ruta}/sustituir-por-banguat", new { monedaCodigo = "USD", fecha = miercoles });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Banguat", body.GetProperty("fuente").GetString());
        Assert.Equal(7.64157m, body.GetProperty("tasa").GetDecimal());
        Assert.False(await _pg.Data.ScalarAsync<bool>("SELECT activa FROM historialtipocambio WHERE id = $1", manual));
        Assert.Equal(1L, await _pg.Data.ContarAuditoriaAsync(usuario, "sustituir_tipo_cambio_banguat"));
    }

    [Fact]
    public async Task Sustituir_SinManual_Responde409()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);

        var response = await (await ContadorAsync()).PostAsJsonAsync($"{Ruta}/sustituir-por-banguat", new { monedaCodigo = "USD", fecha = miercoles });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Sustituir_BanguatCaido_Responde409()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var client = await ContadorAsync();
        await CapturarAsync(client, Manual(miercoles));
        _pg.Banguat.Falla = true;

        var response = await client.PostAsJsonAsync($"{Ruta}/sustituir-por-banguat", new { monedaCodigo = "USD", fecha = miercoles });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Banguat no respondió", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Sustituir_BanguatSinDatos_Responde409()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var client = await ContadorAsync();
        await CapturarAsync(client, Manual(miercoles));

        var response = await client.PostAsJsonAsync($"{Ruta}/sustituir-por-banguat", new { monedaCodigo = "USD", fecha = miercoles });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DarDeBaja_NoUsada_Responde200YAudita()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuario);
        var id = await CapturarAsync(client, Manual(miercoles));

        var baja = await client.PostAsJsonAsync($"{Ruta}/{id}/baja", new { motivo = "Error de captura" });

        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False((await baja.LeerJsonAsync()).GetProperty("activa").GetBoolean());
        Assert.False(await _pg.Data.ScalarAsync<bool>("SELECT activa FROM historialtipocambio WHERE id = $1", id));
        Assert.Equal(1L, await _pg.Data.ContarAuditoriaAsync(usuario, "baja_tipo_cambio"));

        var segunda = await client.PostAsJsonAsync($"{Ruta}/{id}/baja", new { motivo = "Otra vez" });
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Contains("ya está dada de baja", await segunda.LeerErrorAsync());

        var llamadasAntes = _pg.Banguat.Llamadas;
        _pg.Banguat.Sembrar(CodigoUsd, miercoles, 7.64157m);
        await client.GetAsync(Consulta("USD", miercoles));
        Assert.True(_pg.Banguat.Llamadas > llamadasAntes);
    }

    [Fact]
    public async Task DarDeBaja_ValidacionesYPermisos()
    {
        var miercoles = TestData.SiguienteSemana().AddDays(2);
        var client = await ContadorAsync();
        var id = await CapturarAsync(client, Manual(miercoles));
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Ruta}/{IdInexistente}/baja", new { motivo = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Ruta}/{id}/baja", new { motivo = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsJsonAsync($"{Ruta}/{id}/baja", new { motivo = "x" })).StatusCode);
    }

    [Fact]
    public async Task Historial_FiltraPorMonedaYFecha()
    {
        var lunes = TestData.SiguienteSemana();
        var martes = lunes.AddDays(1);
        var miercoles = lunes.AddDays(2);
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var client = _pg.CreateApiClient(usuario);
        await CapturarAsync(client, Manual(lunes));
        await _pg.Data.SembrarTipoCambioAsync("USD", miercoles, 7.66m);
        await _pg.Data.EjecutarAsync(
            "INSERT INTO historialtipocambio (moneda_id, fecha, tasa, fuente, fecha_referencia, activa, usuario_id) " +
            "VALUES ((SELECT id FROM moneda WHERE codigo = 'USD'), $1, 7.67, 'Banguat', $1, FALSE, $2)",
            martes, usuario);

        var activas = await client.GetAsync($"{Ruta}/historial?moneda=USD&desde={lunes.Dia()}&hasta={miercoles.Dia()}");

        Assert.Equal(HttpStatusCode.OK, activas.StatusCode);
        var filasActivas = (await activas.LeerJsonAsync()).EnumerateArray().ToList();
        Assert.Equal(new[] { miercoles.Dia(), lunes.Dia() }, filasActivas.Select(f => f.GetProperty("fecha").GetString()));
        Assert.Equal(JsonValueKind.String, filasActivas[1].GetProperty("usuarioNombre").ValueKind);
        Assert.All(filasActivas, f => Assert.True(f.GetProperty("activa").GetBoolean()));
        Assert.All(filasActivas, f => Assert.False(f.GetProperty("usada").GetBoolean()));

        var conInactivas = await client.GetAsync($"{Ruta}/historial?moneda=USD&desde={lunes.Dia()}&hasta={miercoles.Dia()}&incluirInactivas=true");
        Assert.Equal(3, (await conInactivas.LeerJsonAsync()).GetArrayLength());

        var invertido = await client.GetAsync($"{Ruta}/historial?moneda=USD&desde={miercoles.Dia()}&hasta={lunes.Dia()}");
        Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);

        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync($"{Ruta}/historial?moneda=USD")).StatusCode);
    }

    [Fact]
    public async Task Monedas_ListaLasActivasConLaFuncionalPrimero()
    {
        var response = await (await ContadorAsync()).GetAsync("/api/monedas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codigos = (await response.LeerJsonAsync()).EnumerateArray().Select(m => m.GetProperty("codigo").GetString()).ToList();
        Assert.Equal("GTQ", codigos[0]);
        Assert.Contains("USD", codigos);
        Assert.Contains("EUR", codigos);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _pg.Factory.CreateClient().GetAsync("/api/monedas")).StatusCode);
    }

    [Fact]
    public async Task Consultar_ArrastradaDeHoy_VuelveAConsultarBanguat()
    {
        var hoy = CambioRules.HoyGuatemala(DateTimeOffset.UtcNow);
        await _pg.Data.SembrarTipoCambioAsync("USD", hoy, 7.60m, arrastrado: true);
        _pg.Banguat.Sembrar(CodigoUsd, hoy, 7.61m);

        var response = await (await ContadorAsync()).GetAsync(Consulta("USD", hoy));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _pg.Banguat.Llamadas);
    }

    private async Task<JsonElement> ConsultarTipoCambioAsync(string moneda, DateOnly fecha)
    {
        var response = await (await ContadorAsync()).GetAsync(Consulta(moneda, fecha));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.LeerJsonAsync()).GetProperty("tipoCambio");
    }
}
