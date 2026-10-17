using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CuentasBancariasMonedaTests
{
    private const string Ruta = "/api/cuentas-bancarias";
    private const string RutaMovimientos = "/api/movimientos-tesoreria";
    private const string RutaConciliaciones = "/api/conciliaciones";

    private readonly PostgresFixture _pg;

    public CuentasBancariasMonedaTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private static object PayloadCuenta(
        int cuentaContableId, string? moneda, decimal saldoApertura = 0m, DateOnly? fecha = null, int? contrapartidaId = null) => new
    {
        banco = "Banco de Prueba",
        numero = $"N{TestData.Sufijo()}",
        tipo = "Monetaria",
        cuentaContableId,
        saldoApertura,
        fechaApertura = fecha,
        cuentaContrapartidaId = contrapartidaId,
        monedaCodigo = moneda,
    };

    private static object PayloadMovimiento(int cuentaBancariaId, DateOnly fecha, string tipo, decimal monto, int contrapartidaId) => new
    {
        cuentaBancariaId,
        fecha,
        tipo,
        monto,
        descripcion = "Movimiento de prueba",
        referencia = (string?)null,
        cuentaContrapartidaId = contrapartidaId,
    };

    private static decimal Dec(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetDecimal();

    private async Task<(int CuentaId, int CuentaContableId)> CrearCuentaUsdConAperturaAsync(EscenarioMe m, HttpClient client, int capital, int cuentaContable)
    {
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "USD", 1000m, m.Miercoles, capital));
        Assert.Equal(HttpStatusCode.Created, estado);
        return (cuerpo.GetProperty("id").GetInt32(), cuerpo.GetProperty("cuentaContableId").GetInt32());
    }

    [Fact]
    public async Task Crear_UsdConApertura_ValoraALaReferenciaDeLaFecha()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "USD", 1000m, m.Miercoles, capital));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("USD", cuerpo.GetProperty("monedaCodigo").GetString());
        Assert.Equal((1000m, 1000m, 7600m), (Dec(cuerpo, "saldo"), Dec(cuerpo, "saldoApertura"), Dec(cuerpo, "saldoLibrosGtq")));

        var cuentaBancariaId = cuerpo.GetProperty("id").GetInt32();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaBancariaId);
        Assert.Equal(1000m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaBancariaId));
        Assert.Equal(7600m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_gtq FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaBancariaId));
        Assert.Equal(7.6m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaBancariaId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1 AND tipo_cambio_id IS NOT NULL", cuentaBancariaId));
        Assert.Equal(7600m, await _pg.Data.DebitoAsync(asientoId, cuentaContable));
        Assert.Equal(7600m, await _pg.Data.CreditoAsync(asientoId, capital));
        Assert.Contains("moneda USD", await _pg.Data.DetalleAuditoriaAsync(m.E.UsuarioId, "crear_cuenta_bancaria"));
    }

    [Fact]
    public async Task Crear_UsdConAperturaSinTasa_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        _pg.Banguat.Falla = true;
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "USD", 1000m, m.Miercoles, capital));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("capturar la tasa manual", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM cuentabancaria WHERE cuenta_contable_id = $1", cuentaContable));
    }

    [Fact]
    public async Task Crear_UsdSinApertura_NoRequiereTasa()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "USD"));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(0m, Dec(cuerpo, "saldoLibrosGtq"));
        Assert.Equal(0, _pg.Banguat.Llamadas);
    }

    [Fact]
    public async Task Crear_SinMoneda_EsEnQuetzales()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, null));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("GTQ", cuerpo.GetProperty("monedaCodigo").GetString());
    }

    [Theory]
    [InlineData("EUR", "Las cuentas bancarias en EUR no están disponibles en esta versión.")]
    [InlineData("XYZ", "La moneda de la cuenta bancaria debe ser GTQ o USD.")]
    public async Task Crear_MonedaNoPermitida_Responde400(string moneda, string mensaje)
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, moneda));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Equal(mensaje, cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Actualizar_CambioDeMoneda_SoloSinMovimientos()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var (_, creada) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, null));
        var cuentaId = creada.GetProperty("id").GetInt32();
        var sinMovimientos = await client.PutAsJsonAsync($"{Ruta}/{cuentaId}", PayloadCuenta(cuentaContable, "USD"));

        Assert.Equal(HttpStatusCode.OK, sinMovimientos.StatusCode);
        Assert.Equal("USD", (await sinMovimientos.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("monedaCodigo").GetString());

        var otraContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var otraCuenta = await client.EnviarAsync(Ruta, PayloadCuenta(otraContable, null));
        var otraId = otraCuenta.Cuerpo.GetProperty("id").GetInt32();
        await _pg.Data.CrearMovimientoAsync(
            new CuentasMovimiento(otraId, otraContable, await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora")),
            "Ingreso", 100m, m.Miercoles, m.E.PeriodoId, m.E.UsuarioId);
        var conMovimientos = await client.PutAsJsonAsync($"{Ruta}/{otraId}", PayloadCuenta(otraContable, "USD"));

        Assert.Equal(HttpStatusCode.Conflict, conMovimientos.StatusCode);
        Assert.Contains("tiene movimientos", (await conMovimientos.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

        var eur = await client.PutAsJsonAsync($"{Ruta}/{cuentaId}", PayloadCuenta(cuentaContable, "EUR"));
        Assert.Equal(HttpStatusCode.BadRequest, eur.StatusCode);
    }

    [Fact]
    public async Task Listar_ExponeMonedaYSaldoEnLibros()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var (cuentaId, _) = await CrearCuentaUsdConAperturaAsync(m, client, capital, cuentaContable);

        var lista = await client.GetFromJsonAsync<JsonElement>(Ruta);

        var fila = lista.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == cuentaId);
        Assert.True(fila.TryGetProperty("monedaId", out _));
        Assert.Equal("USD", fila.GetProperty("monedaCodigo").GetString());
        Assert.Equal(7600m, Dec(fila, "saldoLibrosGtq"));
    }

    [Fact]
    public async Task Movimiento_ManualEnCuentaUsd_ValoraYGuardaLaTasa()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var (cuentaId, _) = await CrearCuentaUsdPorApiAsync(client, cuentaContable);

        var (estado, cuerpo) = await client.EnviarAsync(RutaMovimientos, PayloadMovimiento(cuentaId, m.Miercoles, "Ingreso", 250m, capital));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((250m, 1900m, 7.6m), (Dec(cuerpo, "monto"), Dec(cuerpo, "montoGtq"), Dec(cuerpo, "tipoCambio")));
        var asientoId = cuerpo.GetProperty("asientoId").GetInt32();
        Assert.Equal(1900m, await _pg.Data.DebitoAsync(asientoId, cuentaContable));
        Assert.Equal(1900m, await _pg.Data.CreditoAsync(asientoId, capital));
        Assert.Equal(1L, await _pg.Data.ContarAuditoriaAsync(m.E.UsuarioId, "registrar_movimiento_tesoreria"));

        _pg.Banguat.Falla = true;
        var sinTasa = await client.EnviarAsync(RutaMovimientos, PayloadMovimiento(cuentaId, m.Jueves, "Ingreso", 250m, capital));
        Assert.Equal(HttpStatusCode.Conflict, sinTasa.Estado);
    }

    [Fact]
    public async Task Movimientos_ListarExponeMontoGtqTasaYMoneda()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var (cuentaId, _) = await CrearCuentaUsdConAperturaAsync(m, client, capital, cuentaContable);

        var movimientos = await client.GetFromJsonAsync<JsonElement>($"{RutaMovimientos}?cuentaBancariaId={cuentaId}");

        var movimiento = movimientos.EnumerateArray().Single();
        Assert.Equal((7600m, 7.6m), (Dec(movimiento, "montoGtq"), Dec(movimiento, "tipoCambio")));
        Assert.True(movimiento.TryGetProperty("monedaId", out _));
    }

    [Fact]
    public async Task Conciliacion_EnUsd_ConDiferenciaCeroSeFinaliza()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var (cuentaId, _) = await CrearCuentaUsdConAperturaAsync(m, client, capital, cuentaContable);
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Jueves, 7.70m);
        var (estadoMovimiento, movimiento) = await client.EnviarAsync(
            RutaMovimientos, PayloadMovimiento(cuentaId, m.Jueves, "Ingreso", 250m, capital));
        Assert.Equal(HttpStatusCode.Created, estadoMovimiento);

        var (estado, conciliacion) = await client.EnviarAsync(
            RutaConciliaciones, new { cuentaBancariaId = cuentaId, fecha = m.Viernes, saldoExtracto = 1250m });

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal("USD", conciliacion.GetProperty("monedaCodigo").GetString());
        Assert.Equal(250m, Dec(conciliacion, "diferencia"));
        var conciliacionId = conciliacion.GetProperty("id").GetInt32();

        var marcado = await client.PostAsJsonAsync(
            $"{RutaConciliaciones}/{conciliacionId}/movimientos", new { movimientoId = movimiento.GetProperty("id").GetInt32() });
        Assert.Equal(0m, Dec(await marcado.Content.ReadFromJsonAsync<JsonElement>(), "diferencia"));

        var finalizada = await client.PostAsync($"{RutaConciliaciones}/{conciliacionId}/finalizar", null);
        var cuerpoFinal = await finalizada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, finalizada.StatusCode);
        Assert.Equal("Conciliado", cuerpoFinal.GetProperty("estado").GetString());
        Assert.Equal(1L, await _pg.Data.ContarAuditoriaAsync(m.E.UsuarioId, "finalizar_conciliacion"));

        var listado = await client.GetFromJsonAsync<JsonElement>($"{RutaConciliaciones}?cuentaBancariaId={cuentaId}");
        var fila = listado.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == conciliacionId);
        Assert.Equal("USD", fila.GetProperty("monedaCodigo").GetString());

        var detalle = await client.GetFromJsonAsync<JsonElement>($"{RutaConciliaciones}/{conciliacionId}");
        Assert.Equal("USD", detalle.GetProperty("resumen").GetProperty("monedaCodigo").GetString());
    }

    [Fact]
    public async Task Transferencia_UsdAUsd_ValoraAmbasPiernasALaMismaReferencia()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (destinoId, destinoContableId) = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");

        var (estado, cuerpo) = await client.EnviarAsync(
            $"{RutaMovimientos}/transferencias",
            new { cuentaOrigenId = m.BancoUsdId, cuentaDestinoId = destinoId, fecha = m.Miercoles, monto = 100m, descripcion = "Transferencia de prueba" });

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((100m, 760m, 7.6m), (Dec(cuerpo.GetProperty("egreso"), "monto"), Dec(cuerpo.GetProperty("egreso"), "montoGtq"), Dec(cuerpo.GetProperty("egreso"), "tipoCambio")));
        Assert.Equal((100m, 760m), (Dec(cuerpo.GetProperty("ingreso"), "monto"), Dec(cuerpo.GetProperty("ingreso"), "montoGtq")));
        var asientoId = cuerpo.GetProperty("asientoId").GetInt32();
        Assert.Equal(760m, await _pg.Data.DebitoAsync(asientoId, destinoContableId));
        Assert.Equal(760m, await _pg.Data.CreditoAsync(asientoId, m.BancoUsdContableId));
        Assert.Equal((0m, 0m), (Dec(cuerpo.GetProperty("diferencialRealizado"), "ganancia"), Dec(cuerpo.GetProperty("diferencialRealizado"), "perdida")));
    }

    [Fact]
    public async Task Transferencia_UsdAUsdSinTasa_Responde409()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        _pg.Banguat.Falla = true;
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (destinoId, _) = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");

        var (estado, cuerpo) = await client.EnviarAsync(
            $"{RutaMovimientos}/transferencias",
            new { cuentaOrigenId = m.BancoUsdId, cuentaDestinoId = destinoId, fecha = m.Jueves, monto = 100m, descripcion = "Transferencia de prueba" });

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("capturar la tasa manual", cuerpo.GetProperty("error").GetString());
    }

    private static object Transferencia(int origenId, int destinoId, DateOnly fecha, decimal monto, decimal? montoDestino = null) => new
    {
        cuentaOrigenId = origenId,
        cuentaDestinoId = destinoId,
        fecha,
        monto,
        montoDestino,
        descripcion = "Transferencia de prueba",
    };

    private static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> TransferirAsync(HttpClient client, object payload) =>
        client.EnviarAsync($"{RutaMovimientos}/transferencias", payload);

    [Fact]
    public async Task Transferencia_Compra_GtqAUsd()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.64157m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await TransferirAsync(client, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Miercoles, 7700m, 1000m));

        Assert.Equal(HttpStatusCode.Created, estado);
        var egreso = cuerpo.GetProperty("egreso");
        var ingreso = cuerpo.GetProperty("ingreso");
        Assert.Equal((7700m, 7700m, 1m), (Dec(egreso, "monto"), Dec(egreso, "montoGtq"), Dec(egreso, "tipoCambio")));
        Assert.Equal((1000m, 7641.57m, 7.64157m), (Dec(ingreso, "monto"), Dec(ingreso, "montoGtq"), Dec(ingreso, "tipoCambio")));
        Assert.Equal((0m, 58.43m), (Dec(cuerpo.GetProperty("diferencialRealizado"), "ganancia"), Dec(cuerpo.GetProperty("diferencialRealizado"), "perdida")));
        var asientoId = cuerpo.GetProperty("asientoId").GetInt32();
        Assert.Equal(7641.57m, await _pg.Data.DebitoAsync(asientoId, m.BancoUsdContableId));
        Assert.Equal(58.43m, await _pg.Data.DebitoAsync(asientoId, m.CuentaDifPerdida));
        Assert.Equal(7700m, await _pg.Data.CreditoAsync(asientoId, m.BancoGtqContableId));
        Assert.Contains("diferencial realizado Q-58.43", await _pg.Data.DetalleAuditoriaAsync(m.E.UsuarioId, "registrar_transferencia_tesoreria"));
    }

    [Fact]
    public async Task Transferencia_Venta_UsdAGtq()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.64157m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await TransferirAsync(client, Transferencia(m.BancoUsdId, m.BancoGtqId, m.Miercoles, 1000m, 7600m));

        Assert.Equal(HttpStatusCode.Created, estado);
        var egreso = cuerpo.GetProperty("egreso");
        Assert.Equal((1000m, 7641.57m), (Dec(egreso, "monto"), Dec(egreso, "montoGtq")));
        Assert.Equal(41.57m, Dec(cuerpo.GetProperty("diferencialRealizado"), "perdida"));
    }

    [Fact]
    public async Task Transferencia_ConGanancia_UsaLaCuentaDeGananciaRealizada()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.64157m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await TransferirAsync(client, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Miercoles, 7600m, 1000m));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(41.57m, Dec(cuerpo.GetProperty("diferencialRealizado"), "ganancia"));
        var asientoId = cuerpo.GetProperty("asientoId").GetInt32();
        Assert.Equal(41.57m, await _pg.Data.CreditoAsync(asientoId, m.CuentaDifGanancia));
    }

    [Fact]
    public async Task Transferencia_SinMontoDestino_Responde400()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = await TransferirAsync(client, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Miercoles, 100m));

        Assert.Equal((HttpStatusCode.BadRequest, "El monto destino es obligatorio cuando las cuentas son de distinta moneda."),
            (estado, cuerpo.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Transferencia_MismaMonedaConMontoDestinoDistinto_Responde400()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var client = _pg.CreateApiClient(m.E.UsuarioId);
        var (destinoId, _) = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");

        var (estado, cuerpo) = await TransferirAsync(client, Transferencia(m.BancoUsdId, destinoId, m.Miercoles, 100m, 99m));

        Assert.Equal((HttpStatusCode.BadRequest, "En una transferencia entre cuentas de la misma moneda el monto destino debe ser igual al monto."),
            (estado, cuerpo.GetProperty("error").GetString()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transferencia_SinTasa_Responde409(bool usdEsOrigen)
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        _pg.Banguat.Falla = true;
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (estado, cuerpo) = usdEsOrigen
            ? await TransferirAsync(client, Transferencia(m.BancoUsdId, m.BancoGtqId, m.Jueves, 1000m, 7700m))
            : await TransferirAsync(client, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Jueves, 7700m, 1000m));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("capturar la tasa manual", cuerpo.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transferencia_SinCuentaDeDiferencial_Responde409(bool perdida)
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.64157m);
        var client = _pg.CreateApiClient(m.E.UsuarioId);

        var (cuentaGanancia, cuentaPerdida, payload) = perdida
            ? ((int?)m.CuentaDifGanancia, (int?)null, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Miercoles, 7700m, 1000m))
            : ((int?)null, (int?)m.CuentaDifPerdida, Transferencia(m.BancoGtqId, m.BancoUsdId, m.Miercoles, 7600m, 1000m));
        await _pg.Data.ConfigurarCuentasCambiariasAsync(cuentaGanancia, cuentaPerdida, m.CuentaRevalGanancia, m.CuentaRevalPerdida);

        var (estado, cuerpo) = await TransferirAsync(client, payload);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains(perdida ? "pérdida cambiaria realizada" : "ganancia cambiaria realizada", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Permisos_VendedorNoAccede()
    {
        var m = await _pg.Data.SembrarEscenarioMeAsync();
        var vendedor = _pg.CreateApiClient(m.E.UsuarioId, Roles.Vendedor);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");

        var cuenta = await vendedor.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "GTQ"));
        var movimiento = await vendedor.EnviarAsync(RutaMovimientos, PayloadMovimiento(m.BancoGtqId, m.Miercoles, "Ingreso", 10m, capital));

        Assert.Equal(HttpStatusCode.Forbidden, cuenta.Estado);
        Assert.Equal(HttpStatusCode.Forbidden, movimiento.Estado);
    }

    private static async Task<(int CuentaId, int CuentaContableId)> CrearCuentaUsdPorApiAsync(HttpClient client, int cuentaContable)
    {
        var (estado, cuerpo) = await client.EnviarAsync(Ruta, PayloadCuenta(cuentaContable, "USD"));
        Assert.Equal(HttpStatusCode.Created, estado);
        return (cuerpo.GetProperty("id").GetInt32(), cuerpo.GetProperty("cuentaContableId").GetInt32());
    }
}
