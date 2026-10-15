using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FacturasMonedaExtranjeraTests
{
    private const string Ruta = "/api/cxc/facturas";
    private const string RutaCxp = "/api/cxp/facturas";

    private readonly PostgresFixture _pg;

    public FacturasMonedaExtranjeraTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private static int IdDe(JsonElement cuerpo) => cuerpo.GetProperty("id").GetInt32();

    private static decimal Monto(JsonElement cuerpo, string propiedad) => cuerpo.GetProperty(propiedad).GetDecimal();

    private static string MensajeDe(JsonElement cuerpo) => cuerpo.GetProperty("error").GetString() ?? string.Empty;

    private async Task AssertAsientoAsync(int asientoId, params (int Cuenta, decimal Debito, decimal Credito)[] esperadas)
    {
        var lineas = await _pg.Data.LineasDeAsientoAsync(asientoId);
        Assert.Equal(esperadas.Length, lineas.Count);
        foreach (var esperada in esperadas)
        {
            Assert.Contains(esperada, lineas);
        }
    }

    private static Dictionary<string, object?> NotaDeCredito(EscenarioMe m, int facturaId, DateOnly fecha, decimal precio) =>
        FacturaPayloads.Cxc(m.E, $"NC-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(m.E.ImpuestoIvaId, m.E.CuentaIngreso, precio: precio) })
            .EnFecha(fecha)
            .Con("tipoDocumento", "NotaCredito")
            .Con("documentoOrigenId", facturaId);

    [Fact]
    public async Task CxC_UsdConIva_RegistraAsientoYGuardaMonedaYTasa()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var tasa = await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(8512m, Monto(cuerpo, "montoTotal"));
        Assert.Equal(7600m, Monto(cuerpo, "montoBase"));
        Assert.Equal(912m, Monto(cuerpo, "montoIva"));
        Assert.Equal(1120m, Monto(cuerpo, "montoTotalOriginal"));
        Assert.Equal("USD", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(7.6m, Monto(cuerpo, "tipoCambioAplicado"));
        Assert.Equal("Banguat", cuerpo.GetProperty("tipoCambioFuente").GetString());
        Assert.False(cuerpo.GetProperty("tipoCambioArrastrado").GetBoolean());
        Assert.Equal(1120m, Monto(cuerpo, "saldoPendiente"));
        Assert.Equal(8512m, Monto(cuerpo, "saldoPendienteGtq"));
        var linea = cuerpo.GetProperty("lineas")[0];
        Assert.Equal((1120m, 8512m), (linea.GetProperty("montoLineaOriginal").GetDecimal(), linea.GetProperty("montoLinea").GetDecimal()));

        await AssertAsientoAsync(
            cuerpo.GetProperty("asientoId").GetInt32(),
            (m.E.CuentaCxC, 8512m, 0m), (m.E.CuentaIngreso, 0m, 7600m), (m.E.CuentaIvaDebito, 0m, 912m));

        var id = IdDe(cuerpo);
        Assert.Equal(
            await _pg.Data.ScalarAsync<int>("SELECT id FROM moneda WHERE codigo = 'USD'"),
            await _pg.Data.ScalarAsync<int>("SELECT moneda_id FROM documentocxc WHERE id = $1", id));
        Assert.Equal(tasa, await _pg.Data.ScalarAsync<int>("SELECT tipo_cambio_id FROM documentocxc WHERE id = $1", id));
        Assert.Contains(
            ", moneda USD, original 1,120.00, tipo de cambio 7.600000 (Banguat)",
            await _pg.Data.DetalleAuditoriaAsync(m.E.UsuarioId, "registrar_factura_cxc"));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(m.E.UsuarioId, "registrar_factura_cxc"));
    }

    [Fact]
    public async Task CxP_Eur_RegistraConLaTasaDerivada()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("EUR", m.Miercoles, 8.556266m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(RutaCxp, FacturaPayloads.CxpEn(m, "EUR", m.Miercoles, 100m));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(855.63m, Monto(cuerpo, "montoTotal"));
        Assert.Equal(100m, Monto(cuerpo, "montoTotalOriginal"));
        Assert.Equal("EUR", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(8.556266m, Monto(cuerpo, "tipoCambioAplicado"));
        Assert.Contains(", moneda EUR, original 100.00", await _pg.Data.DetalleAuditoriaAsync(m.E.UsuarioId, "registrar_factura_cxp"));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(m.E.UsuarioId, "registrar_factura_cxp"));
    }

    [Fact]
    public async Task RedondeoPorLinea_SumaLosGtqRedondeadosDeCadaLinea()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.64157m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var lineas = new[]
        {
            FacturaPayloads.Linea(m.E.ImpuestoExentoId, m.E.CuentaIngreso, precio: 33.33m),
            FacturaPayloads.Linea(m.E.ImpuestoExentoId, m.E.CuentaIngreso, precio: 33.33m),
        };
        var payload = FacturaPayloads.Cxc(m.E, $"F-{TestData.Sufijo()}", lineas).EnFecha(m.Miercoles).EnMoneda("USD");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(509.38m, Monto(cuerpo, "montoTotal"));
        Assert.Equal(66.66m, Monto(cuerpo, "montoTotalOriginal"));
    }

    [Fact]
    public async Task NotaDeCredito_HeredaMonedaYTasaYSeLimitaEnOriginal()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var factura = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, NotaDeCredito(m, factura, m.Jueves, 112m).Con("tipoCambioAplicado", 9.99m));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("USD", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(7.6m, Monto(cuerpo, "tipoCambioAplicado"));
        Assert.Equal(851.20m, Monto(cuerpo, "montoTotal"));
        Assert.Equal(91.20m, Monto(cuerpo, "montoIva"));
        Assert.Equal("Banguat", cuerpo.GetProperty("tipoCambioFuente").GetString());

        var facturaCuerpo = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{factura}");
        Assert.Equal((1008m, 7660.80m), (Monto(facturaCuerpo, "saldoPendiente"), Monto(facturaCuerpo, "saldoPendienteGtq")));
    }

    [Fact]
    public async Task NotaDeCredito_QueExcedeElSaldoOriginal_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var factura = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, NotaDeCredito(m, factura, m.Jueves, 1120.01m));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("(USD 1120.01)", MensajeDe(cuerpo));
        Assert.Contains("(USD 1120.00)", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task NotaDeCredito_ConMonedaDistinta_Responde400()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var factura = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, NotaDeCredito(m, factura, m.Jueves, 112m).EnMoneda("EUR"));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Equal("La nota de crédito usa la moneda de su factura de origen (USD).", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task SinTasaYBanguatCaido_Responde409ConAvisoDeCapturaManual()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        _pg.Banguat.Falla = true;
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("No hay tipo de cambio de USD para el", MensajeDe(cuerpo));
        Assert.Contains("capturar la tasa manual", MensajeDe(cuerpo));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE cliente_id = $1", m.E.ClienteId));
    }

    [Fact]
    public async Task ElClienteNoControlaLaTasa()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var payload = FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId)
            .Con("tipoCambioAplicado", 9.99m)
            .Con("tipoCambioId", 1)
            .Con("monedaId", 999)
            .Con("montoTotalOriginal", 5m)
            .Con("tipoCambioFuente", "Manual")
            .Con("tipoCambioArrastrado", true);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(7.6m, Monto(cuerpo, "tipoCambioAplicado"));
        Assert.Equal(1120m, Monto(cuerpo, "montoTotalOriginal"));
        Assert.Equal("Banguat", cuerpo.GetProperty("tipoCambioFuente").GetString());
        Assert.False(cuerpo.GetProperty("tipoCambioArrastrado").GetBoolean());
    }

    [Fact]
    public async Task DocumentoEnQuetzales_QuedaIntacto()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "GTQ", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("GTQ", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(1m, Monto(cuerpo, "tipoCambioAplicado"));
        Assert.Equal(Monto(cuerpo, "montoTotal"), Monto(cuerpo, "montoTotalOriginal"));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE id = $1 AND tipo_cambio_id IS NULL", IdDe(cuerpo)));
        Assert.Equal(0, _pg.Banguat.Llamadas);
    }

    [Fact]
    public async Task MonedaInexistente_Responde400()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "XXX", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("no existe o está inactiva", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task TasaArrastrada_QuedaMarcadaEnElDocumento()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m, arrastrado: true);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.True(cuerpo.GetProperty("tipoCambioArrastrado").GetBoolean());
    }

    [Fact]
    public async Task CxP_UsdConRetencionIsr_GuardaElOriginalDeLaRetencion()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await client.EnviarAsync(RutaCxp, FacturaPayloads.CxpEn(m, "USD", m.Miercoles, 1000m, proveedor));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(7600m, Monto(cuerpo, "montoTotal"));
        Assert.Equal(380m, Monto(cuerpo, "montoRetencionIsr"));
        Assert.Equal(380m, Monto(cuerpo.GetProperty("retencionIsr"), "monto"));
        Assert.Equal(950m, Monto(cuerpo, "saldoPendiente"));
        Assert.Equal(7220m, Monto(cuerpo, "saldoPendienteGtq"));
        await AssertAsientoAsync(
            cuerpo.GetProperty("asientoId").GetInt32(),
            (m.E.CuentaCxP, 0m, 7220m), (m.E.CuentaGasto, 7600m, 0m), (m.E.CuentaIsrRetenido, 0m, 380m));
        var id = IdDe(cuerpo);
        Assert.Equal(50m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido_original FROM retencion WHERE documento_cxp_id = $1", id));
        Assert.Equal(380m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE documento_cxp_id = $1", id));
    }

    [Fact]
    public async Task ListarYObtener_ExponenLosCamposDeMoneda()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var factura = await client.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));
        string[] campos = ["moneda", "montoTotalOriginal", "tipoCambioAplicado", "tipoCambioFuente", "tipoCambioArrastrado", "saldoPendiente", "saldoPendienteGtq"];

        var lista = await client.GetFromJsonAsync<JsonElement>(Ruta);
        var fila = lista.EnumerateArray().Single(f => f.GetProperty("id").GetInt32() == factura);
        var detalle = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{factura}");

        foreach (var campo in campos)
        {
            Assert.True(fila.TryGetProperty(campo, out _), campo);
            Assert.True(detalle.TryGetProperty(campo, out _), campo);
        }
    }

    [Fact]
    public async Task Permisos_VendedorRegistraCxCPeroNoCxP()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var vendedor = _pg.CreateApiClient(m.E.UsuarioId, Roles.Vendedor);

        var (estadoCxC, _) = await vendedor.EnviarAsync(Ruta, FacturaPayloads.CxcEn(m, "USD", m.Miercoles, 1120m, m.E.ImpuestoIvaId));
        var (estadoCxP, _) = await vendedor.EnviarAsync(RutaCxp, FacturaPayloads.CxpEn(m, "USD", m.Miercoles, 1000m));

        Assert.Equal(HttpStatusCode.Created, estadoCxC);
        Assert.Equal(HttpStatusCode.Forbidden, estadoCxP);
    }

    [Fact]
    public async Task NotaDeCredito_DeFacturaEnQuetzales_SigueEnQuetzales()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var factura = await client.RegistrarVentaAsync(FacturaPayloads.Cxc(m.E, $"F-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(m.E.ImpuestoIvaId, m.E.CuentaIngreso, precio: 1120m) }).EnFecha(m.Miercoles));

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, NotaDeCredito(m, factura, m.Jueves, 112m));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("GTQ", cuerpo.GetProperty("moneda").GetString());
        Assert.Equal(1m, Monto(cuerpo, "tipoCambioAplicado"));
    }
}
