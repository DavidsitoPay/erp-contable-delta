using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CxCControllerTests
{
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public CxCControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task CrearFactura_GeneraAsientoBalanceadoQueDebitaLaCuentaDeControl()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"F-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);
        var payload = new
        {
            numero,
            tipoDocumento = "Factura",
            clienteId = e.ClienteId,
            fecha = "2025-03-15",
            fechaVencimiento = "2025-04-15",
            periodoId = e.PeriodoId,
            cuentaControlId = e.CuentaCxC,
            lineas = new object[]
            {
                new { descripcion = "Servicio", cantidad = 2m, precioUnitario = 100m, porcentajeImpuesto = 12m, centroCostoId = e.CentroCostoId, cuentaContableId = e.CuentaIngreso },
                new { descripcion = "Otro", cantidad = 1m, precioUnitario = 50.50m, porcentajeImpuesto = 0m, centroCostoId = (int?)null, cuentaContableId = e.CuentaIngreso },
            },
        };

        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var documentoId = body.GetProperty("id").GetInt32();
        var asientoId = body.GetProperty("asientoId").GetInt32();
        Assert.Equal(274.50m, body.GetProperty("montoTotal").GetDecimal());

        var totalDebito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var totalCredito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var debitoControl = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, e.CuentaCxC);
        var creditoIngreso = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, e.CuentaIngreso);
        var estadoAsiento = await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", asientoId);
        Assert.Equal(274.50m, totalDebito);
        Assert.Equal(totalDebito, totalCredito);
        Assert.Equal(274.50m, debitoControl);
        Assert.Equal(274.50m, creditoIngreso);
        Assert.Equal("Confirmado", estadoAsiento);

        Assert.Equal(274.50m, await ObtenerSaldoAsync(client, documentoId));
        var auditorias = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_factura_cxc'", e.UsuarioId);
        Assert.Equal(1, auditorias);
    }

    [Fact]
    public async Task CrearFactura_ConUnProveedorComoCliente_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"F-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxc/facturas", PayloadFactura(e, numero, e.ProveedorId, e.CuentaIngreso, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE numero = $1", numero));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE numero = $1", $"CXC-{numero}"));
    }

    [Fact]
    public async Task CrearFactura_ConCentroDeCostoInexistente_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"F-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxc/facturas", PayloadFactura(e, numero, e.ClienteId, e.CuentaIngreso, IdInexistente));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("centros de costo", error);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE numero = $1", numero));
    }

    [Fact]
    public async Task CrearPago_QueExcedeElSaldo_RespondeRN05YNoAlteraElSaldo()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var pagoValido = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 40m));
        Assert.Equal(HttpStatusCode.Created, pagoValido.StatusCode);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));

        var pagoExcedido = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 60.01m));

        Assert.Equal(HttpStatusCode.BadRequest, pagoExcedido.StatusCode);
        var error = (await pagoExcedido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("RN-05", error);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM aplicacionpagocliente WHERE documento_id = $1", documentoId));
    }

    [Fact]
    public async Task CrearPago_AFacturaDeOtroCliente_RespondeBadRequestSinRegistrarElRecibo()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otroClienteId = await _pg.Data.CrearContraparteAsync("Cliente");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(otroClienteId, documentoId, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", otroClienteId));
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
    }

    [Fact]
    public async Task ListarFacturas_ConFacturaCreada_DevuelveFacturaConClienteNombre()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.GetFromJsonAsync<JsonElement>("/api/cxc/facturas");

        var facturas = response.EnumerateArray().ToList();
        Assert.NotEmpty(facturas);
        var factura = facturas.First(f => f.GetProperty("id").GetInt32() == documentoId);
        Assert.Equal(100m, factura.GetProperty("montoTotal").GetDecimal());
        Assert.True(factura.GetProperty("clienteNombre").GetString()!.Length > 0);
    }

    [Fact]
    public async Task ObtenerFactura_ConIdExistente_Devuelve200ConLineas()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.GetAsync($"/api/cxc/facturas/{documentoId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(documentoId, body.GetProperty("id").GetInt32());
        Assert.True(body.GetProperty("lineas").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ObtenerFactura_ConIdInexistente_Devuelve404()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.GetAsync($"/api/cxc/facturas/{IdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListarPagos_ConPagoCreado_DevuelvePagoConClienteNombre()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);
        await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m));

        var response = await client.GetFromJsonAsync<JsonElement>("/api/cxc/pagos");

        var pagos = response.EnumerateArray().ToList();
        Assert.NotEmpty(pagos);
        var pago = pagos.First();
        Assert.True(pago.GetProperty("clienteNombre").GetString()!.Length > 0);
        Assert.Equal(50m, pago.GetProperty("montoTotal").GetDecimal());
    }

    [Fact]
    public async Task CrearFactura_ConPeriodoCerrado_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var periodoCerrado = await _pg.Data.CrearPeriodoAsync("Cerrado");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { PeriodoId = periodoCerrado }, numero, e.ClienteId, e.CuentaIngreso, null);
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no esté Abierto", error);
    }

    [Fact]
    public async Task CrearFactura_ConPeriodoInexistente_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { PeriodoId = IdInexistente }, numero, e.ClienteId, e.CuentaIngreso, null);
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no existe", error);
    }

    [Fact]
    public async Task CrearFactura_ConCuentaInactiva_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuentaInactiva = await _pg.Data.CrearCuentaInactivaAsync("Ingreso", "Acreedora");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";

        var payload = PayloadFactura(e, numero, e.ClienteId, cuentaInactiva, null);
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("inactivas", error);
    }

    [Fact]
    public async Task CrearFactura_ConCuentaDeMayor_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuentaMayor = await _pg.Data.CrearCuentaConSubcuentasAsync("Ingreso", "Acreedora");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";

        var payload = PayloadFactura(e, numero, e.ClienteId, cuentaMayor.PadreId, null);
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("de mayor", error);
    }

    [Fact]
    public async Task CrearFactura_ConCuentaControlIncorrecta_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuentaControlMala = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { CuentaCxC = cuentaControlMala }, numero, e.ClienteId, e.CuentaIngreso, null);
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("Activo", error);
        Assert.Contains("Deudora", error);
    }

    [Fact]
    public async Task CrearPago_RegistraAuditoriaConAccion()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m));

        var auditorias = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_pago_cxc'", e.UsuarioId);
        Assert.Equal(1, auditorias);
    }

    [Fact]
    public async Task CrearFactura_ComoVendedor_Exitosa()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"F-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);

        var response = await client.PostAsJsonAsync("/api/cxc/facturas", PayloadFactura(e, numero, e.ClienteId, e.CuentaIngreso, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE numero = $1", numero));
    }

    [Fact]
    public async Task CrearPago_ComoVendedor_Exitoso()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
    }

    private static object PayloadFactura(Escenario e, string numero, int clienteId, int cuentaLineaId, int? centroCostoId) => new
    {
        numero,
        tipoDocumento = "Factura",
        clienteId,
        fecha = "2025-03-15",
        fechaVencimiento = "2025-04-15",
        periodoId = e.PeriodoId,
        cuentaControlId = e.CuentaCxC,
        lineas = new object[]
        {
            new { descripcion = "Servicio", cantidad = 1m, precioUnitario = 100m, porcentajeImpuesto = 0m, centroCostoId, cuentaContableId = cuentaLineaId },
        },
    };

    private static object PayloadPago(int clienteId, int documentoId, decimal monto) => new
    {
        clienteId,
        fecha = "2025-03-20",
        metodoPago = "Efectivo",
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    private async Task<int> CrearFacturaAsync(HttpClient client, Escenario e, int clienteId)
    {
        var response = await client.PostAsJsonAsync("/api/cxc/facturas", PayloadFactura(e, $"F-{TestData.Sufijo()}", clienteId, e.CuentaIngreso, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<decimal> ObtenerSaldoAsync(HttpClient client, int documentoId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/cxc/facturas/{documentoId}");
        return body.GetProperty("saldoPendiente").GetDecimal();
    }
}
