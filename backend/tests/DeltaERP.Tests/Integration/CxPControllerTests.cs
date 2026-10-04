using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CxPControllerTests
{
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public CxPControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task CrearFactura_GeneraAsientoBalanceadoQueAcreditaLaCuentaDeControl()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"P-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);
        var payload = new
        {
            numero,
            tipoDocumento = "Factura",
            proveedorId = e.ProveedorId,
            fecha = "2025-03-15",
            fechaVencimiento = "2025-04-15",
            periodoId = e.PeriodoId,
            cuentaControlId = e.CuentaCxP,
            lineas = new object[]
            {
                new { descripcion = "Insumos", cantidad = 2m, precioUnitario = 100m, porcentajeImpuesto = 12m, centroCostoId = e.CentroCostoId, cuentaContableId = e.CuentaGasto },
                new { descripcion = "Otro", cantidad = 1m, precioUnitario = 50.50m, porcentajeImpuesto = 0m, centroCostoId = (int?)null, cuentaContableId = e.CuentaGasto },
            },
        };

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var documentoId = body.GetProperty("id").GetInt32();
        var asientoId = body.GetProperty("asientoId").GetInt32();
        Assert.Equal(274.50m, body.GetProperty("montoTotal").GetDecimal());

        var totalDebito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var totalCredito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var creditoControl = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, e.CuentaCxP);
        var debitoGasto = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, e.CuentaGasto);
        var estadoAsiento = await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", asientoId);
        Assert.Equal(274.50m, totalCredito);
        Assert.Equal(totalCredito, totalDebito);
        Assert.Equal(274.50m, creditoControl);
        Assert.Equal(274.50m, debitoGasto);
        Assert.Equal("Confirmado", estadoAsiento);

        Assert.Equal(274.50m, await ObtenerSaldoAsync(client, documentoId));
        var auditorias = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_factura_cxp'", e.UsuarioId);
        Assert.Equal(1, auditorias);
    }

    [Fact]
    public async Task CrearFactura_ConUnClienteComoProveedor_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"P-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", PayloadFactura(e, numero, e.ClienteId, e.CuentaGasto));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", numero));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE numero = $1", $"CXP-{numero}"));
    }

    [Fact]
    public async Task CrearFactura_ConProveedorInexistente_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"P-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", PayloadFactura(e, numero, IdInexistente, e.CuentaGasto));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no existe", error);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", numero));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE numero = $1", $"CXP-{numero}"));
    }

    [Fact]
    public async Task CrearFactura_ConCuentaDeLineaInexistente_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"P-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", PayloadFactura(e, numero, e.ProveedorId, IdInexistente));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no existen", error);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", numero));
    }

    [Fact]
    public async Task CrearPago_QueExcedeElSaldo_RespondeRN05YNoAlteraElSaldo()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var pagoValido = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 40m));
        Assert.Equal(HttpStatusCode.Created, pagoValido.StatusCode);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));

        var pagoExcedido = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 60.01m));

        Assert.Equal(HttpStatusCode.BadRequest, pagoExcedido.StatusCode);
        var error = (await pagoExcedido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("RN-05", error);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM aplicacionpagoproveedor WHERE documento_id = $1", documentoId));
    }

    [Fact]
    public async Task CrearPago_AFacturaDeOtroProveedor_RespondeBadRequestSinRegistrarElPago()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otroProveedorId = await _pg.Data.CrearContraparteAsync("Proveedor");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(otroProveedorId, documentoId, 10m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM pagoproveedorcabecera WHERE proveedor_id = $1", otroProveedorId));
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
    }

    [Fact]
    public async Task CrearFactura_ComoVendedor_RespondeForbidden()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"P-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", PayloadFactura(e, numero, e.ProveedorId, e.CuentaGasto));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", numero));
    }

    [Fact]
    public async Task CrearPago_ComoVendedor_RespondeForbidden()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var clientAdmin = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(clientAdmin, e, e.ProveedorId);

        var clientVendedor = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);
        var response = await clientVendedor.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarFacturas_ConFacturaCreada_DevuelveFacturaConProveedorNombre()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.GetFromJsonAsync<JsonElement>("/api/cxp/facturas");

        var facturas = response.EnumerateArray().ToList();
        Assert.NotEmpty(facturas);
        var factura = facturas.First(f => f.GetProperty("id").GetInt32() == documentoId);
        Assert.Equal(100m, factura.GetProperty("montoTotal").GetDecimal());
        Assert.True(factura.GetProperty("proveedorNombre").GetString()!.Length > 0);
    }

    [Fact]
    public async Task ObtenerFactura_ConIdExistente_Devuelve200ConLineas()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.GetAsync($"/api/cxp/facturas/{documentoId}");

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

        var response = await client.GetAsync($"/api/cxp/facturas/{IdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListarPagos_ConPagoCreado_DevuelvePagoConProveedorNombre()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);
        await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m));

        var response = await client.GetFromJsonAsync<JsonElement>("/api/cxp/pagos");

        var pagos = response.EnumerateArray().ToList();
        Assert.NotEmpty(pagos);
        var pago = pagos.First();
        Assert.True(pago.GetProperty("proveedorNombre").GetString()!.Length > 0);
        Assert.Equal(50m, pago.GetProperty("montoTotal").GetDecimal());
    }

    [Fact]
    public async Task CrearFactura_ConPeriodoCerrado_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var periodoCerrado = await _pg.Data.CrearPeriodoAsync("Cerrado");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"P-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { PeriodoId = periodoCerrado }, numero, e.ProveedorId, e.CuentaGasto);
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no esté Abierto", error);
    }

    [Fact]
    public async Task CrearFactura_ConPeriodoInexistente_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"P-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { PeriodoId = IdInexistente }, numero, e.ProveedorId, e.CuentaGasto);
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no existe", error);
    }

    [Fact]
    public async Task CrearFactura_ConCuentaInactiva_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuentaInactiva = await _pg.Data.CrearCuentaInactivaAsync("Gasto", "Deudora");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"P-{TestData.Sufijo()}";

        var payload = PayloadFactura(e, numero, e.ProveedorId, cuentaInactiva);
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("inactivas", error);
    }

    [Fact]
    public async Task CrearFactura_ConCuentaDeMayor_RespondeBadRequest()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuentaMayor = await _pg.Data.CrearCuentaConSubcuentasAsync("Gasto", "Deudora");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"P-{TestData.Sufijo()}";

        var payload = PayloadFactura(e, numero, e.ProveedorId, cuentaMayor.PadreId);
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

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
        var numero = $"P-{TestData.Sufijo()}";

        var payload = PayloadFactura(e with { CuentaCxP = cuentaControlMala }, numero, e.ProveedorId, e.CuentaGasto);
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("Pasivo", error);
        Assert.Contains("Acreedora", error);
    }

    [Fact]
    public async Task CrearPago_RegistraAuditoriaConAccion()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m));

        var auditorias = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_pago_cxp'", e.UsuarioId);
        Assert.Equal(1, auditorias);
    }

    private static object PayloadFactura(Escenario e, string numero, int proveedorId, int cuentaLineaId) => new
    {
        numero,
        tipoDocumento = "Factura",
        proveedorId,
        fecha = "2025-03-15",
        fechaVencimiento = "2025-04-15",
        periodoId = e.PeriodoId,
        cuentaControlId = e.CuentaCxP,
        lineas = new object[]
        {
            new { descripcion = "Insumos", cantidad = 1m, precioUnitario = 100m, porcentajeImpuesto = 0m, centroCostoId = (int?)null, cuentaContableId = cuentaLineaId },
        },
    };

    private static object PayloadPago(int proveedorId, int documentoId, decimal monto) => new
    {
        proveedorId,
        fecha = "2025-03-20",
        metodoPago = "Transferencia",
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    private static async Task<int> CrearFacturaAsync(HttpClient client, Escenario e, int proveedorId)
    {
        var response = await client.PostAsJsonAsync("/api/cxp/facturas", PayloadFactura(e, $"P-{TestData.Sufijo()}", proveedorId, e.CuentaGasto));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<decimal> ObtenerSaldoAsync(HttpClient client, int documentoId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/cxp/facturas/{documentoId}");
        return body.GetProperty("saldoPendiente").GetDecimal();
    }
}
