using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CobrosCxCRetencionTests
{
    private const string FechaCobro = "2025-03-20";
    private const string FechaFactura = "2025-03-15";

    private readonly PostgresFixture _pg;

    public CobrosCxCRetencionTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private sealed record Venta(Escenario E, HttpClient Client, int ClienteId, int FacturaId, int BancoId, int BancoContableId);

    private async Task<Venta> PrepararVentaAsync(string? tipoAgente = "SECTOR_PUBLICO", decimal total = 33600m)
    {
        var (e, client) = await _pg.PrepararAsync();
        var cliente = await _pg.Data.CrearClienteAgenteAsync(tipoAgente);
        var factura = await client.RegistrarVentaAsync(VentaConIva(e, cliente, total));
        var banco = await _pg.Data.CrearCuentaBancariaAsync();
        return new Venta(e, client, cliente, factura, banco.Id, banco.CuentaContableId);
    }

    private static object CobroDe(Venta v, decimal aplicado, object? retencion, string fecha = FechaCobro) =>
        Cobro(v.ClienteId, v.BancoId, fecha, Aplicacion(v.FacturaId, aplicado, retencion));

    private Task<long> ContarRetencionesAsync(int facturaId) =>
        _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM retencion WHERE documento_cxc_id = $1", facturaId);

    private static string[] Advertencias(JsonElement cuerpo) =>
        cuerpo.GetProperty("advertencias").EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToArray();

    [Fact]
    public async Task CrearPago_ConIvaRetenidoPorClienteAgente_RegistraAsientoNetoRetencionYConstancia()
    {
        var v = await PrepararVentaAsync();
        var numeroConstancia = $"A-{TestData.Sufijo()}";

        var (estado, cuerpo) = await v.Client.EnviarAsync(
            RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m, numeroConstancia: numeroConstancia, fechaConstancia: FechaFactura)));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((33600m, 32700m), (cuerpo.GetProperty("montoTotal").GetDecimal(), cuerpo.GetProperty("montoNeto").GetDecimal()));
        Assert.Empty(Advertencias(cuerpo));
        var retencion = Assert.Single(cuerpo.GetProperty("retenciones").EnumerateArray());
        Assert.Equal((v.FacturaId, 900m), (retencion.GetProperty("documentoId").GetInt32(), retencion.GetProperty("monto").GetDecimal()));
        Assert.NotEqual(JsonValueKind.Null, retencion.GetProperty("constanciaId").ValueKind);

        var reciboId = cuerpo.GetProperty("id").GetInt32();
        Assert.Equal(32700m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId);
        Assert.Equal(
            (32700m, 900m, 33600m),
            (await _pg.Data.DebitoAsync(asientoId, v.BancoContableId),
                await _pg.Data.DebitoAsync(asientoId, v.E.CuentaIvaRetenidoClientes),
                await _pg.Data.CreditoAsync(asientoId, v.E.CuentaCxC)));
        Assert.Equal(32700m, await _pg.Data.SaldoBancarioAsync(v.BancoId));
        var factura = await v.Client.GetFromJsonAsync<JsonElement>($"/api/cxc/facturas/{v.FacturaId}");
        Assert.Equal(0m, factura.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal(
            ("IVA_CLIENTE", 3600m, 25m, 900m, 900m),
            (await _pg.Data.ScalarAsync<string>("SELECT concepto FROM retencion WHERE documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<decimal>("SELECT base FROM retencion WHERE documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<decimal>("SELECT tasa FROM retencion WHERE documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<decimal>("SELECT monto_calculado FROM retencion WHERE documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE documento_cxc_id = $1", v.FacturaId)));
        Assert.Equal(
            ("IVA", numeroConstancia, "EMITIDA"),
            (await _pg.Data.ScalarAsync<string>("SELECT k.tipo FROM constanciaretencion k JOIN retencion r ON r.id = k.retencion_id WHERE r.documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<string>("SELECT k.numero FROM constanciaretencion k JOIN retencion r ON r.id = k.retencion_id WHERE r.documento_cxc_id = $1", v.FacturaId),
                await _pg.Data.ScalarAsync<string>("SELECT k.estado FROM constanciaretencion k JOIN retencion r ON r.id = k.retencion_id WHERE r.documento_cxc_id = $1", v.FacturaId)));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(v.E.UsuarioId, "registrar_pago_cxc"));
    }

    [Fact]
    public async Task CrearPago_ConMontoDistintoDelSugerido_RegistraYAdvierte()
    {
        var v = await PrepararVentaAsync();

        var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(300m)));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Contains("difiere del sugerido (Q900.00)", Assert.Single(Advertencias(cuerpo)));
        Assert.Equal(900m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_calculado FROM retencion WHERE documento_cxc_id = $1", v.FacturaId));
        Assert.Equal(300m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE documento_cxc_id = $1", v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_ConFechaDeConstanciaDistintaALaFactura_Advierte()
    {
        var v = await PrepararVentaAsync();

        var (estado, cuerpo) = await v.Client.EnviarAsync(
            RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m, numeroConstancia: $"A-{TestData.Sufijo()}", fechaConstancia: FechaCobro)));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Contains("difiere de la fecha de la factura", Assert.Single(Advertencias(cuerpo)));
    }

    [Fact]
    public async Task CrearPago_ConRetencionMayorAlIvaDeLaFactura_Responde409SinRegistrarNada()
    {
        var v = await PrepararVentaAsync();

        var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(3700m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("excede el IVA retenible", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", v.ClienteId));
        Assert.Equal(0L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_ConRetencionMayorAlMontoAplicado_Responde409()
    {
        var v = await PrepararVentaAsync();

        var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 500m, RetencionIva(900m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("excede el monto aplicado", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearPago_ConSegundaRetencionEnLaMismaFactura_Responde409()
    {
        var v = await PrepararVentaAsync();
        Assert.Equal(HttpStatusCode.Created, (await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 10000m, RetencionIva(900m)))).Estado);

        var segunda = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 10000m, RetencionIva(100m)));
        var sinRetencion = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 10000m, null));

        Assert.Equal(HttpStatusCode.Conflict, segunda.Estado);
        Assert.Contains("ya tiene IVA retenido registrado", segunda.Cuerpo.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, sinRetencion.Estado);
        Assert.Equal(1L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_ConDosAplicacionesConRetencionALaMismaFactura_Responde409()
    {
        var v = await PrepararVentaAsync();
        var cobro = Cobro(
            v.ClienteId, v.BancoId, FechaCobro,
            Aplicacion(v.FacturaId, 5000m, RetencionIva(500m)),
            Aplicacion(v.FacturaId, 5000m, RetencionIva(400m)));

        var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, cobro);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("ya tiene IVA retenido registrado", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearPago_DeClienteNoAgente_ExigeConfirmacionExplicita()
    {
        var v = await PrepararVentaAsync(tipoAgente: null);

        var sinConfirmar = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(540m)));
        var confirmada = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(540m, confirmaClienteNoAgente: true)));

        Assert.Equal(HttpStatusCode.Conflict, sinConfirmar.Estado);
        Assert.Contains("confirmaClienteNoAgente", sinConfirmar.Cuerpo.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, confirmada.Estado);
        Assert.Empty(Advertencias(confirmada.Cuerpo));
        Assert.Equal(1L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_ConRetencionIgualAlMontoAplicado_Responde400PorCobroNetoNoPositivo()
    {
        var v = await PrepararVentaAsync("AGENTE_PRIVADO", 112m);

        var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 12m, RetencionIva(12m)));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("cobro neto", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_SinCuentaDeIvaRetenidoPorClientes_Responde409()
    {
        var v = await PrepararVentaAsync();
        try
        {
            await _pg.Data.ConfigurarCuentasRetencionAsync(v.E.CuentaIsrRetenido, null);

            var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m)));

            Assert.Equal(HttpStatusCode.Conflict, estado);
            Assert.StartsWith("Falta configurar la cuenta de IVA retenido por clientes", cuerpo.GetProperty("error").GetString());
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Fact]
    public async Task CrearPago_SinParametroVigente_Responde409()
    {
        var v = await PrepararVentaAsync();
        try
        {
            await _pg.Data.EjecutarAsync("UPDATE parametroretencion SET activo = FALSE WHERE codigo = 'IVA_CLIENTE_SECTOR_PUBLICO'");

            var (estado, cuerpo) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m)));

            Assert.Equal(HttpStatusCode.Conflict, estado);
            Assert.Contains("No hay un parámetro de retención de IVA vigente", cuerpo.GetProperty("error").GetString());
        }
        finally
        {
            await _pg.Data.EjecutarAsync("UPDATE parametroretencion SET activo = TRUE WHERE codigo = 'IVA_CLIENTE_SECTOR_PUBLICO'");
        }
    }

    [Fact]
    public async Task CrearPago_EnPeriodoCerrado_Responde400SinRetencion()
    {
        var v = await PrepararVentaAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");

        var (estado, cuerpo) = await v.Client.EnviarAsync(
            RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m), inicio.AddDays(2).ToString("yyyy-MM-dd")));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("no esté Abierto", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Theory]
    [InlineData(" ", HttpStatusCode.BadRequest)]
    [InlineData("DUP", HttpStatusCode.Conflict)]
    public async Task CrearPago_ConConstanciaInvalidaODuplicada_RespondeSinRegistrarElCobro(string numero, HttpStatusCode esperado)
    {
        var v = await PrepararVentaAsync();
        var repetido = $"K-{TestData.Sufijo()}";
        var otra = await v.Client.RegistrarVentaAsync(VentaConIva(v.E, v.ClienteId, 33600m));
        var primero = Cobro(v.ClienteId, v.BancoId, FechaCobro, Aplicacion(otra, 33600m, RetencionIva(900m, numeroConstancia: repetido, fechaConstancia: FechaFactura)));
        Assert.Equal(HttpStatusCode.Created, (await v.Client.EnviarAsync(RutaPagosCxC, primero)).Estado);

        var (estado, _) = await v.Client.EnviarAsync(
            RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m, numeroConstancia: numero == "DUP" ? repetido : numero, fechaConstancia: FechaFactura)));

        Assert.Equal(esperado, estado);
        Assert.Equal(0L, await ContarRetencionesAsync(v.FacturaId));
    }

    [Fact]
    public async Task CrearPago_ComoVendedor_ConRetencion_Responde403()
    {
        var v = await PrepararVentaAsync();
        var vendedor = _pg.CreateApiClient(v.E.UsuarioId, Roles.Vendedor);

        var (estado, _) = await vendedor.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m)));

        Assert.Equal(HttpStatusCode.Forbidden, estado);
    }

    [Fact]
    public async Task ListarPagos_IncluyeMontoNetoYRetencionPorAplicacion()
    {
        var v = await PrepararVentaAsync();
        var (_, creado) = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 33600m, RetencionIva(900m)));

        var pagos = await v.Client.GetFromJsonAsync<JsonElement>(RutaPagosCxC);

        var pago = pagos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == creado.GetProperty("id").GetInt32());
        Assert.Equal((33600m, 32700m), (pago.GetProperty("montoTotal").GetDecimal(), pago.GetProperty("montoNeto").GetDecimal()));
        Assert.Equal(900m, pago.GetProperty("aplicaciones")[0].GetProperty("retencionIva").GetDecimal());
    }

    [Fact]
    public async Task RetencionIvaSugerida_ReflejaElIvaDeLaFacturaLasNotasDeCreditoYLaRetencionPrevia()
    {
        var v = await PrepararVentaAsync();
        var ruta = $"/api/cxc/facturas/{v.FacturaId}/retencion-iva-sugerida";

        var inicial = await v.Client.GetFromJsonAsync<JsonElement>(ruta);
        var nota = VentaConIva(v.E, v.ClienteId, 11200m).Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", v.FacturaId);
        await v.Client.RegistrarVentaAsync(nota);
        var conNota = await v.Client.GetFromJsonAsync<JsonElement>(ruta);
        var cobro = await v.Client.EnviarAsync(RutaPagosCxC, CobroDe(v, 22400m, RetencionIva(600m)));
        var despues = await v.Client.GetFromJsonAsync<JsonElement>(ruta);

        Assert.Equal((true, 900m, 3600m, 3600m, true, "SECTOR_PUBLICO", 25m, 30000m, 33600m, false), (
            inicial.GetProperty("aplica").GetBoolean(), inicial.GetProperty("sugerido").GetDecimal(), inicial.GetProperty("ivaFactura").GetDecimal(),
            inicial.GetProperty("ivaRetenible").GetDecimal(), inicial.GetProperty("clienteAgente").GetBoolean(), inicial.GetProperty("tipoAgente").GetString(),
            inicial.GetProperty("tasa").GetDecimal(), inicial.GetProperty("umbralMinimo").GetDecimal(), inicial.GetProperty("totalFactura").GetDecimal(),
            inicial.GetProperty("yaRetenida").GetBoolean()));
        Assert.Equal((1200m, 2400m, 600m), (
            conNota.GetProperty("ivaNotasCredito").GetDecimal(), conNota.GetProperty("ivaRetenible").GetDecimal(), conNota.GetProperty("sugerido").GetDecimal()));
        Assert.Equal(HttpStatusCode.Created, cobro.Estado);
        Assert.Empty(Advertencias(cobro.Cuerpo));
        Assert.Equal((true, false, 0m), (despues.GetProperty("yaRetenida").GetBoolean(), despues.GetProperty("aplica").GetBoolean(), despues.GetProperty("sugerido").GetDecimal()));
        Assert.Contains("ya tiene IVA retenido", despues.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task RetencionIvaSugerida_SinUmbralOParaNotaDeCredito_NoAplica()
    {
        var v = await PrepararVentaAsync("SECTOR_PUBLICO", 11200m);
        var nota = await v.Client.RegistrarVentaAsync(VentaConIva(v.E, v.ClienteId, 1120m).Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", v.FacturaId));

        var pequena = await v.Client.GetFromJsonAsync<JsonElement>($"/api/cxc/facturas/{v.FacturaId}/retencion-iva-sugerida");
        var deNota = await v.Client.GetFromJsonAsync<JsonElement>($"/api/cxc/facturas/{nota}/retencion-iva-sugerida");

        Assert.Equal((false, 0m), (pequena.GetProperty("aplica").GetBoolean(), pequena.GetProperty("sugerido").GetDecimal()));
        Assert.Contains("no alcanza el umbral", pequena.GetProperty("motivo").GetString());
        Assert.Contains("Solo una factura vigente", deNota.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task RetencionIvaSugerida_ConFacturaInexistenteOComoVendedor_Responde404Y403()
    {
        var v = await PrepararVentaAsync();
        var vendedor = _pg.CreateApiClient(v.E.UsuarioId, Roles.Vendedor);

        var inexistente = await v.Client.GetAsync("/api/cxc/facturas/2000000000/retencion-iva-sugerida");
        var prohibida = await vendedor.GetAsync($"/api/cxc/facturas/{v.FacturaId}/retencion-iva-sugerida");

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
    }
}
