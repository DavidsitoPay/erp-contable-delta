using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class PagosMonedaExtranjeraTests
{
    private const string RutaFacturasCxC = "/api/cxc/facturas";

    private readonly PostgresFixture _pg;

    public PagosMonedaExtranjeraTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private sealed record Venta(EscenarioMe M, HttpClient Client, int FacturaId);

    private static decimal Dec(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetDecimal();

    private static string MensajeDe(JsonElement cuerpo) => cuerpo.GetProperty("error").GetString() ?? string.Empty;

    private async Task<Venta> VentaUsdAsync(bool exenta = false, decimal precio = 1120m, decimal tasa = 7.60m)
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, tasa);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var impuestoId = exenta ? m.E.ImpuestoExentoId : m.E.ImpuestoIvaId;
        var facturaId = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, precio, impuestoId));
        return new Venta(m, client, facturaId);
    }

    private static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> Cobrar(
        Venta v, int cuentaId, DateOnly fecha, decimal monto, decimal? valorEfectivo = null) =>
        v.Client.EnviarAsync(RutaPagosCxC, Cobro(v.M.E.ClienteId, cuentaId, fecha.Dia(), AplicacionMe(v.FacturaId, monto, valorEfectivo)));

    private static void AssertDiferencial(JsonElement cuerpo, decimal ganancia, decimal perdida)
    {
        var diferencial = cuerpo.GetProperty("diferencialRealizado");
        Assert.Equal((ganancia, perdida), (Dec(diferencial, "ganancia"), Dec(diferencial, "perdida")));
    }

    [Fact]
    public async Task Cobro_ParcialACuentaUsd_GeneraGananciaRealizada()
    {
        var v = await VentaUsdAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", v.M.Jueves, 7.70m);

        var (estado, cuerpo) = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 400m);

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("USD", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal((400m, 3080m, 400m), (Dec(cuerpo, "montoTotal"), Dec(cuerpo, "montoTotalGtq"), Dec(cuerpo, "montoCuenta")));
        AssertDiferencial(cuerpo, 40m, 0m);
        var aplicacion = cuerpo.GetProperty("aplicaciones")[0];
        Assert.Equal(
            (3040m, 3080m, 40m, 7.7m),
            (Dec(aplicacion, "montoAplicadoGtq"), Dec(aplicacion, "valorGtq"), Dec(aplicacion, "diferencialGtq"), Dec(aplicacion, "tipoCambioPago")));

        var reciboId = cuerpo.GetProperty("id").GetInt32();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId);
        Assert.Equal(3080m, await _pg.Data.DebitoAsync(asientoId, v.M.BancoUsdContableId));
        Assert.Equal(3040m, await _pg.Data.CreditoAsync(asientoId, v.M.E.CuentaCxC));
        Assert.Equal(40m, await _pg.Data.CreditoAsync(asientoId, v.M.CuentaDifGanancia));
        Assert.True(await _pg.Data.AsientoCuadraAsync(asientoId));
        Assert.Equal(400m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(3080m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_gtq FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(7.7m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM movimientotesoreria WHERE recibo_pago_id = $1 AND tipo_cambio_id IS NOT NULL", reciboId));

        var factura = await v.Client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxC}/{v.FacturaId}");
        Assert.Equal((720m, 5472m), (Dec(factura, "saldoPendiente"), Dec(factura, "saldoPendienteGtq")));
        Assert.Contains("diferencial realizado Q+40.00", await _pg.Data.DetalleAuditoriaAsync(v.M.E.UsuarioId, "registrar_pago_cxc"));
        Assert.Equal(400m, await _pg.Data.SaldoBancarioAsync(v.M.BancoUsdId));
    }

    [Fact]
    public async Task Cobro_FinalACuentaGtq_GeneraPerdidaYCierraElSaldoEnGtq()
    {
        var v = await VentaUsdAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", v.M.Jueves, 7.70m);
        Assert.Equal(HttpStatusCode.Created, (await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 400m)).Estado);

        var (estado, cuerpo) = await Cobrar(v, v.M.BancoGtqId, v.M.Viernes, 720m, 5400m);

        Assert.Equal(HttpStatusCode.Created, estado);
        AssertDiferencial(cuerpo, 0m, 72m);
        var aplicacion = cuerpo.GetProperty("aplicaciones")[0];
        Assert.Equal(
            (5472m, 5400m, -72m),
            (Dec(aplicacion, "montoAplicadoGtq"), Dec(aplicacion, "valorGtq"), Dec(aplicacion, "diferencialGtq")));
        Assert.Equal(JsonValueKind.Null, aplicacion.GetProperty("tipoCambioPago").ValueKind);
        Assert.Equal(5400m, Dec(cuerpo, "montoCuenta"));

        var reciboId = cuerpo.GetProperty("id").GetInt32();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId);
        Assert.Equal(5400m, await _pg.Data.DebitoAsync(asientoId, v.M.BancoGtqContableId));
        Assert.Equal(72m, await _pg.Data.DebitoAsync(asientoId, v.M.CuentaDifPerdida));
        Assert.Equal(5472m, await _pg.Data.CreditoAsync(asientoId, v.M.E.CuentaCxC));
        Assert.Equal(5400m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(5400m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_gtq FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(1m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM movimientotesoreria WHERE recibo_pago_id = $1 AND tipo_cambio_id IS NULL", reciboId));

        var factura = await v.Client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxC}/{v.FacturaId}");
        Assert.Equal((0m, 0m), (Dec(factura, "saldoPendiente"), Dec(factura, "saldoPendienteGtq")));
    }

    [Fact]
    public async Task TresCobrosParciales_ElUltimoAbsorbeElRedondeo()
    {
        var v = await VentaUsdAsync(exenta: true, precio: 100m, tasa: 7.64157m);
        await _pg.Data.SembrarTipoCambioAsync("USD", v.M.Jueves, 7.64157m);

        var primero = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 33.33m);
        var segundo = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 33.33m);
        var tercero = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 33.34m);

        Assert.Equal(HttpStatusCode.Created, primero.Estado);
        Assert.Equal(HttpStatusCode.Created, segundo.Estado);
        Assert.Equal(HttpStatusCode.Created, tercero.Estado);
        Assert.Equal(254.69m, Dec(primero.Cuerpo.GetProperty("aplicaciones")[0], "montoAplicadoGtq"));
        Assert.Equal(254.69m, Dec(segundo.Cuerpo.GetProperty("aplicaciones")[0], "montoAplicadoGtq"));
        Assert.Equal(254.78m, Dec(tercero.Cuerpo.GetProperty("aplicaciones")[0], "montoAplicadoGtq"));
        Assert.Equal(0m, Dec(primero.Cuerpo.GetProperty("aplicaciones")[0], "diferencialGtq"));
        Assert.Equal(0m, Dec(segundo.Cuerpo.GetProperty("aplicaciones")[0], "diferencialGtq"));
        Assert.Equal(-0.01m, Dec(tercero.Cuerpo.GetProperty("aplicaciones")[0], "diferencialGtq"));

        var factura = await v.Client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxC}/{v.FacturaId}");
        Assert.Equal((0m, 0m), (Dec(factura, "saldoPendiente"), Dec(factura, "saldoPendienteGtq")));
        Assert.Equal(764.16m, await _pg.Data.ScalarAsync<decimal>(
            "SELECT SUM(monto_aplicado_gtq) FROM aplicacionpagocliente WHERE documento_id = $1", v.FacturaId));
    }

    [Fact]
    public async Task Pago_CxpConRetencionIsr_PerdidaSobreElNeto()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Jueves, 7.70m);
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (facturaId, _, _) = await client.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, "USD", m.Miercoles, 1000m, proveedor));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxP, PagoProveedor(proveedor, m.BancoUsdId, m.Jueves.Dia(), AplicacionMe(facturaId, 950m)));

        Assert.Equal(HttpStatusCode.Created, estado);
        AssertDiferencial(cuerpo, 0m, 95m);
        Assert.Equal((950m, 7315m), (Dec(cuerpo, "montoTotal"), Dec(cuerpo, "montoTotalGtq")));
        var asientoId = await _pg.Data.ScalarAsync<int>(
            "SELECT asiento_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", cuerpo.GetProperty("id").GetInt32());
        Assert.Equal(7220m, await _pg.Data.DebitoAsync(asientoId, m.E.CuentaCxP));
        Assert.Equal(95m, await _pg.Data.DebitoAsync(asientoId, m.CuentaDifPerdida));
        Assert.Equal(7315m, await _pg.Data.CreditoAsync(asientoId, m.BancoUsdContableId));
        Assert.Equal(3, (await _pg.Data.LineasDeAsientoAsync(asientoId)).Count);
        Assert.Contains("diferencial realizado Q-95.00", await _pg.Data.DetalleAuditoriaAsync(m.E.UsuarioId, "registrar_pago_cxp"));

        var factura = await client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxP}/{facturaId}");
        Assert.Equal((0m, 0m), (Dec(factura, "saldoPendiente"), Dec(factura, "saldoPendienteGtq")));
    }

    [Fact]
    public async Task Pago_ParcialDeUnaFacturaConRetencion_NoCierra()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Jueves, 7.70m);
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (facturaId, _, _) = await client.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, "USD", m.Miercoles, 1000m, proveedor));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxP, PagoProveedor(proveedor, m.BancoUsdId, m.Jueves.Dia(), AplicacionMe(facturaId, 475m)));

        Assert.Equal(HttpStatusCode.Created, estado);
        var aplicacion = cuerpo.GetProperty("aplicaciones")[0];
        Assert.Equal((3610m, -47.50m), (Dec(aplicacion, "montoAplicadoGtq"), Dec(aplicacion, "diferencialGtq")));
        var factura = await client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxP}/{facturaId}");
        Assert.Equal((475m, 3610m), (Dec(factura, "saldoPendiente"), Dec(factura, "saldoPendienteGtq")));
    }

    [Fact]
    public async Task Rn27_FacturaEnQuetzalesConCuentaUsd_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var facturaId = await client.RegistrarVentaAsync(
            FacturaPayloads.Cxc(m.E, $"F-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(m.E.ImpuestoIvaId, m.E.CuentaIngreso, precio: 1120m) })
                .EnFecha(m.Miercoles));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxC, Cobro(m.E.ClienteId, m.BancoUsdId, m.Miercoles.Dia(), AplicacionMe(facturaId, 1120m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("RN-27", MensajeDe(cuerpo));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", m.E.ClienteId));
    }

    [Fact]
    public async Task Rn27_FacturaEurConCuentaUsd_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("EUR", m.Miercoles, 8.556266m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var facturaId = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "EUR", m.Miercoles, 100m, m.E.ImpuestoIvaId));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxC, Cobro(m.E.ClienteId, m.BancoUsdId, m.Miercoles.Dia(), AplicacionMe(facturaId, 100m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("no coincide con la de la cuenta bancaria (USD)", MensajeDe(cuerpo));
    }

    [Theory]
    [InlineData("GTQ", "RN-27")]
    [InlineData("EUR", "no coincide con la de la cuenta bancaria (USD)")]
    public async Task Rn27_CompraEnQuetzalesOEuroConCuentaUsd_Responde409(string moneda, string mensaje)
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        if (moneda == "EUR")
        {
            await _pg.Data.SembrarTipoCambioAsync("EUR", m.Miercoles, 8.556266m);
        }
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (facturaId, _, _) = await client.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, moneda, m.Miercoles, 100m));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxP, PagoProveedor(m.E.ProveedorId, m.BancoUsdId, m.Miercoles.Dia(), AplicacionMe(facturaId, 100m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains(mensaje, MensajeDe(cuerpo));
    }

    [Fact]
    public async Task MonedasMezcladas_Responde400()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("EUR", m.Miercoles, 8.556266m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var usd = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));
        var eur = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "EUR", m.Miercoles, 100m, m.E.ImpuestoIvaId));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxC,
            Cobro(m.E.ClienteId, m.BancoUsdId, m.Miercoles.Dia(), AplicacionMe(usd, 100m, 760m), AplicacionMe(eur, 10m, 85.56m)));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("una sola moneda", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task ValorEfectivo_FaltanteSobranteOInvalido_Responde400()
    {
        var v = await VentaUsdAsync();
        var gtq = await v.Client.RegistrarVentaAsync(FacturaPayloads.Cxc(
            v.M.E, $"F-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(v.M.E.ImpuestoIvaId, v.M.E.CuentaIngreso, precio: 1120m) })
            .EnFecha(v.M.Miercoles));

        var faltante = await Cobrar(v, v.M.BancoGtqId, v.M.Miercoles, 100m);
        var sobranteEnCuentaUsd = await Cobrar(v, v.M.BancoUsdId, v.M.Miercoles, 100m, 760m);
        var sobranteEnFacturaGtq = await v.Client.EnviarAsync(
            RutaPagosCxC, Cobro(v.M.E.ClienteId, v.M.BancoGtqId, v.M.Miercoles.Dia(), AplicacionMe(gtq, 1120m, 8512m)));
        var decimalesDeMas = await Cobrar(v, v.M.BancoGtqId, v.M.Miercoles, 100m, 760.123m);

        Assert.Equal(HttpStatusCode.BadRequest, faltante.Estado);
        Assert.Contains("Indique el efectivo en quetzales", MensajeDe(faltante.Cuerpo));
        Assert.Equal(HttpStatusCode.BadRequest, sobranteEnCuentaUsd.Estado);
        Assert.Contains("valorEfectivoGtq solo aplica", MensajeDe(sobranteEnCuentaUsd.Cuerpo));
        Assert.Equal(HttpStatusCode.BadRequest, sobranteEnFacturaGtq.Estado);
        Assert.Contains("valorEfectivoGtq solo aplica", MensajeDe(sobranteEnFacturaGtq.Cuerpo));
        Assert.Equal(HttpStatusCode.BadRequest, decimalesDeMas.Estado);
    }

    [Fact]
    public async Task FacturaEnMonedaExtranjeraAplicadaDosVeces_Responde400()
    {
        var v = await VentaUsdAsync();

        var (estado, cuerpo) = await v.Client.EnviarAsync(
            RutaPagosCxC,
            Cobro(v.M.E.ClienteId, v.M.BancoUsdId, v.M.Miercoles.Dia(), AplicacionMe(v.FacturaId, 100m), AplicacionMe(v.FacturaId, 100m)));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("admite una sola aplicación", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task SinCuentaDeDiferencial_Responde409()
    {
        var perdida = await VentaUsdAsync(precio: 100m);
        await _pg.Data.ConfigurarCuentasCambiariasAsync(perdida.M.CuentaDifGanancia, null, perdida.M.CuentaRevalGanancia, perdida.M.CuentaRevalPerdida);

        var (estadoPerdida, cuerpoPerdida) = await Cobrar(perdida, perdida.M.BancoGtqId, perdida.M.Miercoles, 100m, 700m);

        Assert.Equal(HttpStatusCode.Conflict, estadoPerdida);
        Assert.Contains("pérdida cambiaria realizada", MensajeDe(cuerpoPerdida));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", perdida.M.E.ClienteId));

        var ganancia = await VentaUsdAsync(precio: 100m);
        await _pg.Data.ConfigurarCuentasCambiariasAsync(null, ganancia.M.CuentaDifPerdida, ganancia.M.CuentaRevalGanancia, ganancia.M.CuentaRevalPerdida);

        var (estadoGanancia, cuerpoGanancia) = await Cobrar(ganancia, ganancia.M.BancoGtqId, ganancia.M.Miercoles, 100m, 800m);

        Assert.Equal(HttpStatusCode.Conflict, estadoGanancia);
        Assert.Contains("ganancia cambiaria realizada", MensajeDe(cuerpoGanancia));
    }

    [Fact]
    public async Task IvaRetenidoSobreFacturaEnMonedaExtranjera_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var cliente = await _pg.Data.CrearClienteAgenteAsync("SECTOR_PUBLICO");
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var facturaId = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId, cliente));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxC,
            Cobro(cliente, m.BancoGtqId, m.Miercoles.Dia(), new { documentoId = facturaId, montoAplicado = 1120m, valorEfectivoGtq = 8512m, retencionIva = RetencionIva(100m) }));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Equal(
            "El IVA retenido por clientes sobre facturas en moneda extranjera no está disponible en esta versión.",
            MensajeDe(cuerpo));
    }

    [Fact]
    public async Task ExcesoDeSaldoEnMonedaOriginal_Responde400()
    {
        var v = await VentaUsdAsync();

        var (estado, cuerpo) = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 1120.01m);

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("RN-05", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task SinTasaDeLaFechaDelPago_Responde409()
    {
        var v = await VentaUsdAsync();
        _pg.Banguat.Falla = true;

        var (estado, cuerpo) = await Cobrar(v, v.M.BancoUsdId, v.M.Viernes, 400m);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("capturar la tasa manual", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task Cobro_ComoVendedor_Responde403()
    {
        var v = await VentaUsdAsync();
        var vendedor = _pg.CreateApiClient(v.M.E.UsuarioId, Roles.Vendedor);

        var (estado, _) = await vendedor.EnviarAsync(
            RutaPagosCxC, Cobro(v.M.E.ClienteId, v.M.BancoUsdId, v.M.Miercoles.Dia(), AplicacionMe(v.FacturaId, 100m)));

        Assert.Equal(HttpStatusCode.Forbidden, estado);
    }

    [Fact]
    public async Task Listados_CxcExponeMonedaYDiferencial()
    {
        var v = await VentaUsdAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", v.M.Jueves, 7.70m);
        var (_, cobro) = await Cobrar(v, v.M.BancoUsdId, v.M.Jueves, 400m);
        var reciboId = cobro.GetProperty("id").GetInt32();

        var pagos = await v.Client.GetFromJsonAsync<JsonElement>(RutaPagosCxC);

        var recibo = pagos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == reciboId);
        Assert.Equal("USD", recibo.GetProperty("moneda").GetString());
        Assert.Equal(3080m, Dec(recibo, "montoTotalGtq"));
        var aplicacion = recibo.GetProperty("aplicaciones")[0];
        Assert.Equal(40m, Dec(aplicacion, "diferencialGtq"));
        Assert.Equal(7.7m, Dec(aplicacion, "tipoCambioPago"));
    }

    [Fact]
    public async Task Listados_CxpExponeMonedaYDiferencial()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Jueves, 7.70m);
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (facturaId, _, _) = await client.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, "USD", m.Miercoles, 1000m, proveedor));
        var (_, pago) = await client.EnviarAsync(
            RutaPagosCxP, PagoProveedor(proveedor, m.BancoUsdId, m.Jueves.Dia(), AplicacionMe(facturaId, 950m)));
        var pagoId = pago.GetProperty("id").GetInt32();

        var pagos = await client.GetFromJsonAsync<JsonElement>(RutaPagosCxP);

        var fila = pagos.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == pagoId);
        Assert.Equal("USD", fila.GetProperty("moneda").GetString());
        Assert.Equal(7315m, Dec(fila, "montoTotalGtq"));
        var aplicacion = fila.GetProperty("aplicaciones")[0];
        Assert.Equal(-95m, Dec(aplicacion, "diferencialGtq"));
        Assert.Equal(7.7m, Dec(aplicacion, "tipoCambioPago"));
    }

    [Fact]
    public async Task Pago_CxpEnQuetzales_SigueFuncionando()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (facturaId, _, _) = await client.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, "GTQ", m.Miercoles, 1000m));

        var (estado, cuerpo) = await client.EnviarAsync(
            RutaPagosCxP, PagoProveedor(m.E.ProveedorId, m.BancoGtqId, m.Miercoles.Dia(), AplicacionMe(facturaId, 1000m)));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("GTQ", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(Dec(cuerpo, "montoTotal"), Dec(cuerpo, "montoTotalGtq"));
        AssertDiferencial(cuerpo, 0m, 0m);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("aplicaciones")[0].GetProperty("tipoCambioPago").ValueKind);
    }
}
