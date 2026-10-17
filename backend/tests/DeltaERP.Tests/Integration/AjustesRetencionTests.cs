using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class AjustesRetencionTests
{
    private const string Ruta = "/api/retenciones/ajustes";

    private readonly PostgresFixture _pg;

    public AjustesRetencionTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static async Task<int> RegistrarNotaAsync(Compra c, decimal monto)
    {
        var nota = FacturaPayloads.CxpEn(c.M, "GTQ", c.M.Viernes, monto, c.ProveedorId)
            .Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", c.FacturaId);
        var (estado, cuerpo) = await c.Client.EnviarAsync(RutaFacturasCxP, nota);
        Assert.Equal(HttpStatusCode.Created, estado);
        return cuerpo.GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> AjusteDeAsync(Compra c, int notaId) =>
        (await c.Client.GetFromJsonAsync<JsonElement>($"{Ruta}?estado=PENDIENTE")).EnumerateArray()
            .Single(a => a.GetProperty("notaCreditoId").GetInt32() == notaId);

    [Fact]
    public async Task NotaDespuesDelEntero_CreaAjustePendienteVisibleEnListaCxpYReporte()
    {
        var c = await _pg.CompraNrPagadaAsync();
        await c.EnterarAsync(50m);

        var notaId = await RegistrarNotaAsync(c, 200m);

        var ajuste = await AjusteDeAsync(c, notaId);
        Assert.Equal((200m, "PENDIENTE"), (ajuste.GetProperty("montoNc").GetDecimal(), ajuste.GetProperty("estado").GetString()));
        Assert.Equal(c.FacturaId, ajuste.GetProperty("documentoOrigenId").GetInt32());
        var facturas = await c.Client.GetFromJsonAsync<JsonElement>("/api/cxp/facturas");
        Assert.True(facturas.EnumerateArray().Single(f => f.GetProperty("id").GetInt32() == c.FacturaId).GetProperty("ajusteRetencionPendiente").GetBoolean());
        var reporte = await c.Client.GetFromJsonAsync<JsonElement>(
            $"/api/retenciones/reporte?anio={c.Periodo.Year}&mes={c.Periodo.Month}&concepto=ISR_NORES");
        var fila = reporte.GetProperty("filas").EnumerateArray().Single(f => f.GetProperty("retencionId").GetInt32() == ajuste.GetProperty("retencionId").GetInt32());
        Assert.Equal(("US", true), (fila.GetProperty("pais").GetString(), fila.GetProperty("ajustePendiente").GetBoolean()));
        var saldo = Assert.Single(reporte.GetProperty("saldosEnterar").EnumerateArray());
        Assert.Equal(c.Periodo.Dia(), saldo.GetProperty("periodo").GetString());
        Assert.Equal(saldo.GetProperty("retenido").GetDecimal(), saldo.GetProperty("enterado").GetDecimal() + saldo.GetProperty("saldoPorEnterar").GetDecimal());
    }

    [Fact]
    public async Task NotaSinEnteroVigenteEnElPeriodo_NoCreaAjuste()
    {
        var c = await _pg.CompraNrPagadaAsync(TestData.SiguienteMesAislado());

        var notaId = await RegistrarNotaAsync(c, 200m);

        var pendientes = await c.Client.GetFromJsonAsync<JsonElement>($"{Ruta}?estado=PENDIENTE");
        Assert.DoesNotContain(notaId, pendientes.EnumerateArray().Select(a => a.GetProperty("notaCreditoId").GetInt32()));
        var facturas = await c.Client.GetFromJsonAsync<JsonElement>("/api/cxp/facturas");
        Assert.False(facturas.EnumerateArray().Single(f => f.GetProperty("id").GetInt32() == c.FacturaId).GetProperty("ajusteRetencionPendiente").GetBoolean());
    }

    [Fact]
    public async Task Resolver_ConMotivoYReferencia_CierraElAjusteYRechazaRepetirlo()
    {
        var c = await _pg.CompraNrPagadaAsync();
        await c.EnterarAsync(50m);
        var id = (await AjusteDeAsync(c, await RegistrarNotaAsync(c, 200m))).GetProperty("id").GetInt32();
        var cuerpoValido = new { motivo = "Se compensa en el siguiente entero", referencia = "Boleta SAT-1352 99" };

        var (sinReferencia, _) = await c.Client.EnviarAsync($"{Ruta}/{id}/resolver", new { motivo = "x", referencia = " " });
        var (estado, cuerpo) = await c.Client.EnviarAsync($"{Ruta}/{id}/resolver", cuerpoValido);
        var (otraVez, _) = await c.Client.EnviarAsync($"{Ruta}/{id}/resolver", cuerpoValido);
        var (inexistente, _) = await c.Client.EnviarAsync($"{Ruta}/0/resolver", cuerpoValido);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.NotFound), (sinReferencia, estado, otraVez, inexistente));
        Assert.Equal(("RESUELTO", "Boleta SAT-1352 99"), (cuerpo.GetProperty("estado").GetString(), cuerpo.GetProperty("referenciaResolucion").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await c.Client.GetAsync($"{Ruta}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Client.GetAsync($"{Ruta}/0")).StatusCode);
    }

    [Fact]
    public async Task Resolver_ComoVendedor_Responde403()
    {
        var c = await _pg.CompraNrPagadaAsync();
        await c.EnterarAsync(50m);
        var id = (await AjusteDeAsync(c, await RegistrarNotaAsync(c, 200m))).GetProperty("id").GetInt32();
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var (estado, _) = await vendedor.EnviarAsync($"{Ruta}/{id}/resolver", new { motivo = "x", referencia = "y" });

        Assert.Equal(HttpStatusCode.Forbidden, estado);
    }
}
