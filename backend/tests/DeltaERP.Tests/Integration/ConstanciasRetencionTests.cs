using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ConstanciasRetencionTests
{
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public ConstanciasRetencionTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static string RutaConstancia(int retencionId) => $"/api/retenciones/{retencionId}/constancia";

    private static object Constancia(string numero, string fecha = "2025-03-15") => new { numero, fecha };

    private async Task<(Escenario E, HttpClient Client, int FacturaId, int RetencionId)> RegistrarRetencionIsrAsync()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var (facturaId, retencionId, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        Assert.NotNull(retencionId);
        return (e, client, facturaId, retencionId.Value);
    }

    private static string NumeroNuevo() => $"C-{TestData.Sufijo()}";

    [Fact]
    public async Task Registrar_ConLaFechaDeLaFactura_Responde201SinAdvertenciasYAudita()
    {
        var (e, client, _, retencionId) = await RegistrarRetencionIsrAsync();
        var numero = NumeroNuevo();

        var response = await client.PostAsJsonAsync(
            RutaConstancia(retencionId),
            new { numero = $" {numero} ", fecha = "2025-03-15", formulario = "SAT-1331", fechaPagoFormulario = "2025-04-10", periodoDeclarado = "2025-03-01" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuerpo = await response.LeerJsonAsync();
        Assert.Equal(("ISR", numero, "EMITIDA", "SAT-1331", "2025-03-01"), (
            cuerpo.GetProperty("tipo").GetString(), cuerpo.GetProperty("numero").GetString(), cuerpo.GetProperty("estado").GetString(),
            cuerpo.GetProperty("formulario").GetString(), cuerpo.GetProperty("periodoDeclarado").GetString()));
        Assert.Empty(cuerpo.GetProperty("advertencias").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("retenedorContraparteId").ValueKind);
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "registrar_constancia_retencion"));
    }

    [Fact]
    public async Task Registrar_ConFechaDistintaDeLaFactura_Responde201ConAdvertencia()
    {
        var (_, client, _, retencionId) = await RegistrarRetencionIsrAsync();

        var response = await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(NumeroNuevo(), "2025-04-01"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var advertencia = Assert.Single((await response.LeerJsonAsync()).GetProperty("advertencias").EnumerateArray());
        Assert.Contains("fecha de la factura", advertencia.GetString());
    }

    [Fact]
    public async Task Registrar_ConNumeroRepetidoDelMismoTipo_Responde409()
    {
        var (e, client, _, primera) = await RegistrarRetencionIsrAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var (_, segunda, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        Assert.NotNull(segunda);
        var numero = NumeroNuevo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(RutaConstancia(primera), Constancia(numero))).StatusCode);

        var repetida = await client.PostAsJsonAsync(RutaConstancia(segunda.Value), Constancia(numero));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Contains("Ya existe una constancia emitida con ese número", await repetida.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConstanciaDeIva_AceptaElMismoNumeroQueUnaDeIsr()
    {
        var (e, client, _, retencionIsr) = await RegistrarRetencionIsrAsync();
        var cliente = await _pg.Data.CrearClienteAgenteAsync("SECTOR_PUBLICO");
        var factura = await client.RegistrarVentaAsync(VentaConIva(e, cliente, 33600m));
        var banco = await _pg.Data.CrearCuentaBancariaAsync();
        var cobro = await client.EnviarAsync(RutaPagosCxC, Cobro(cliente, banco.Id, "2025-03-20", Aplicacion(factura, 33600m, RetencionIva(900m))));
        Assert.Equal(HttpStatusCode.Created, cobro.Estado);
        var retencionIva = cobro.Cuerpo.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32();
        var numero = NumeroNuevo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(RutaConstancia(retencionIsr), Constancia(numero))).StatusCode);

        var response = await client.PostAsJsonAsync(RutaConstancia(retencionIva), Constancia(numero));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuerpo = await response.LeerJsonAsync();
        Assert.Equal(("IVA", cliente), (cuerpo.GetProperty("tipo").GetString(), cuerpo.GetProperty("retenedorContraparteId").GetInt32()));
        Assert.Empty(cuerpo.GetProperty("advertencias").EnumerateArray());
    }

    [Fact]
    public async Task Registrar_UnaSegundaConstanciaVigenteParaLaMismaRetencion_Responde409()
    {
        var (_, client, _, retencionId) = await RegistrarRetencionIsrAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(NumeroNuevo()))).StatusCode);

        var segunda = await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(NumeroNuevo()));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("La retención ya tiene una constancia emitida.", await segunda.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConElPeriodoContableCerrado_Responde201SinTocarLosAsientos()
    {
        var (e, client, facturaId, retencionId) = await RegistrarRetencionIsrAsync();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM documentocxp WHERE id = $1", facturaId);
        var lineasAntes = await _pg.Data.LineasDeAsientoAsync(asientoId);
        await _pg.Data.CerrarPeriodoDirectoAsync(e.PeriodoId);

        var response = await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(NumeroNuevo()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(lineasAntes, await _pg.Data.LineasDeAsientoAsync(asientoId));
    }

    [Theory]
    [InlineData(0, "número de la constancia es obligatorio")]
    [InlineData(1, "admite hasta 30 caracteres")]
    [InlineData(2, "Formulario inválido")]
    [InlineData(3, "día 1 del mes")]
    public async Task Registrar_ConDatosInvalidos_Responde400(int caso, string fragmento)
    {
        var (_, client, _, retencionId) = await RegistrarRetencionIsrAsync();
        var cuerpo = caso switch
        {
            0 => new { numero = " ", fecha = "2025-03-15", formulario = (string?)null, periodoDeclarado = (string?)null },
            1 => new { numero = new string('N', 31), fecha = "2025-03-15", formulario = (string?)null, periodoDeclarado = (string?)null },
            2 => new { numero = NumeroNuevo(), fecha = "2025-03-15", formulario = (string?)"SAT-0000", periodoDeclarado = (string?)null },
            _ => new { numero = NumeroNuevo(), fecha = "2025-03-15", formulario = (string?)null, periodoDeclarado = (string?)"2025-03-15" },
        };

        var response = await client.PostAsJsonAsync(RutaConstancia(retencionId), cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(fragmento, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConRetencionInexistenteOComoVendedor_Responde404Y403()
    {
        var (e, client, _, retencionId) = await RegistrarRetencionIsrAsync();
        var vendedor = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);

        var inexistente = await client.PostAsJsonAsync(RutaConstancia(IdInexistente), Constancia(NumeroNuevo()));
        var prohibida = await vendedor.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(NumeroNuevo()));

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
    }

    [Fact]
    public async Task Anular_ExigeMotivoYPermiteRegistrarOtraConstancia()
    {
        var (e, client, _, retencionId) = await RegistrarRetencionIsrAsync();
        var numero = NumeroNuevo();
        var constanciaId = (await (await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(numero))).LeerJsonAsync()).GetProperty("id").GetInt32();
        var ruta = $"/api/retenciones/constancias/{constanciaId}/anular";

        var sinMotivo = await client.PostAsJsonAsync(ruta, new { motivo = " " });
        var anulada = await client.PostAsJsonAsync(ruta, new { motivo = "Número digitado con error" });
        var repetida = await client.PostAsJsonAsync(ruta, new { motivo = "Otra vez" });
        var nueva = await client.PostAsJsonAsync(RutaConstancia(retencionId), Constancia(numero));

        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, anulada.StatusCode);
        var cuerpo = await anulada.LeerJsonAsync();
        Assert.Equal(("ANULADA", "Número digitado con error"), (cuerpo.GetProperty("estado").GetString(), cuerpo.GetProperty("motivoAnulacion").GetString()));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.Created, nueva.StatusCode);
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "anular_constancia_retencion"));
    }

    [Fact]
    public async Task Anular_ConConstanciaInexistenteOComoVendedor_Responde404Y403()
    {
        var (e, client, _, _) = await RegistrarRetencionIsrAsync();
        var vendedor = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);

        var inexistente = await client.PostAsJsonAsync($"/api/retenciones/constancias/{IdInexistente}/anular", new { motivo = "x" });
        var prohibida = await vendedor.PostAsJsonAsync($"/api/retenciones/constancias/{IdInexistente}/anular", new { motivo = "x" });

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
    }
}
