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
        var payload = FacturaPayloads.Cxp(e, numero, new[]
        {
            FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaGasto, 2m, 100m, e.CentroCostoId),
            FacturaPayloads.Linea(e.ImpuestoExentoId, e.CuentaGasto, 1m, 50.50m),
        }).ConDte();

        var response = await client.PostAsJsonAsync("/api/cxp/facturas", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var documentoId = body.GetProperty("id").GetInt32();
        var asientoId = body.GetProperty("asientoId").GetInt32();
        Assert.Equal((250.50m, 229.07m, 21.43m), (body.GetProperty("montoTotal").GetDecimal(), body.GetProperty("montoBase").GetDecimal(), body.GetProperty("montoIva").GetDecimal()));

        var totalDebito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var totalCredito = await _pg.Data.ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1", asientoId);
        var estadoAsiento = await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", asientoId);
        Assert.Equal(250.50m, totalCredito);
        Assert.Equal(totalCredito, totalDebito);
        Assert.Equal(250.50m, await _pg.Data.CreditoAsync(asientoId, e.CuentaCxP));
        Assert.Equal(229.07m, await _pg.Data.DebitoAsync(asientoId, e.CuentaGasto));
        Assert.Equal(21.43m, await _pg.Data.DebitoAsync(asientoId, e.CuentaIvaCredito));
        Assert.Equal("Confirmado", estadoAsiento);

        Assert.Equal(250.50m, await ObtenerSaldoAsync(client, documentoId));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "registrar_factura_cxp"));
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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var pagoValido = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 40m, cuenta.Id));
        Assert.Equal(HttpStatusCode.Created, pagoValido.StatusCode);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));

        var pagoExcedido = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 60.01m, cuenta.Id));

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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var otroProveedorId = await _pg.Data.CrearContraparteAsync("Proveedor");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(otroProveedorId, documentoId, 10m, cuenta.Id));

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
        var response = await clientVendedor.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, null));

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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);
        await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id));

        var response = await client.GetFromJsonAsync<JsonElement>("/api/cxp/pagos");

        var pagos = response.EnumerateArray().ToList();
        Assert.NotEmpty(pagos);
        var pago = pagos[0];
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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id));

        var auditorias = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = 'registrar_pago_cxp'", e.UsuarioId);
        Assert.Equal(1, auditorias);
    }

    [Fact]
    public async Task CrearPago_SinCuentaBancaria_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La cuenta bancaria es obligatoria para registrar un cobro o un pago.", await response.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM pagoproveedorcabecera WHERE proveedor_id = $1", e.ProveedorId));
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
    }

    [Fact]
    public async Task CrearPago_CreaMovimientoYAsientoConControlDerivado()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 40m, cuenta.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pagoId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        Assert.Equal("Egreso", await _pg.Data.ScalarAsync<string>("SELECT tipo FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId));
        Assert.Equal("CxP", await _pg.Data.ScalarAsync<string>("SELECT origen FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId));
        Assert.Equal(40m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId));
        Assert.Equal(cuenta.Id, await _pg.Data.ScalarAsync<int>("SELECT cuenta_bancaria_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId));
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId);
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(40m, await _pg.Data.DebitoAsync(asientoId, e.CuentaCxP));
        Assert.Equal(40m, await _pg.Data.CreditoAsync(asientoId, cuenta.CuentaContableId));
        Assert.Equal(-40m, await _pg.Data.SaldoBancarioAsync(cuenta.Id));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "registrar_pago_cxp"));
    }

    [Fact]
    public async Task CrearPago_ConFacturasDeControlesDistintos_GeneraUnaLineaPorControl()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otraCuentaCxP = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var primeraId = await CrearFacturaAsync(client, e, e.ProveedorId);
        var segundaId = await CrearFacturaAsync(client, e with { CuentaCxP = otraCuentaCxP }, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPagoMultiple(e.ProveedorId, cuenta.Id, (primeraId, 30m), (segundaId, 20m)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pagoId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId);
        Assert.Equal(3, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(50m, await _pg.Data.CreditoAsync(asientoId, cuenta.CuentaContableId));
        Assert.Equal(30m, await _pg.Data.DebitoAsync(asientoId, e.CuentaCxP));
        Assert.Equal(20m, await _pg.Data.DebitoAsync(asientoId, otraCuentaCxP));
        Assert.Equal(50m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE pago_proveedor_id = $1", pagoId));
    }

    [Fact]
    public async Task CrearPago_ConFacturaSinAsiento_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"P-{TestData.Sufijo()}";
        var documentoId = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado) VALUES ($1, 'Factura', $2, $3, $4, 100, 100, TRUE, 'Vigente') RETURNING id",
            numero, e.ProveedorId, new DateOnly(2025, 3, 15), new DateOnly(2025, 4, 15));

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal($"No se pudo determinar la cuenta de control de la factura {numero}.", await response.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM pagoproveedorcabecera WHERE proveedor_id = $1", e.ProveedorId));
    }

    [Fact]
    public async Task CrearPago_ConSaldoRN05Excedido_NoCreaMovimiento()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 40m, cuenta.Id))).StatusCode);

        var excedido = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 60.01m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, excedido.StatusCode);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuenta.Id));
        Assert.Equal(-40m, await _pg.Data.SaldoBancarioAsync(cuenta.Id));
    }

    [Fact]
    public async Task CrearPago_ConCuentaBancariaInactivaOInexistente_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET activa = false WHERE id = $1", cuenta.Id);

        var inexistente = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, IdInexistente));
        var inactiva = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Equal("La cuenta bancaria indicada no existe.", await inexistente.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, inactiva.StatusCode);
        Assert.Contains("está inactiva", await inactiva.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM pagoproveedorcabecera WHERE proveedor_id = $1", e.ProveedorId));
    }

    [Fact]
    public async Task CrearPago_EnPeriodoCerradoOSinPeriodo_Responde400YNoDejaPago()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");

        var cerrado = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id, inicio.AddDays(2).ToString("yyyy-MM-dd")));
        var sinPeriodo = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 50m, cuenta.Id, TestData.FechaSinPeriodo.ToString("yyyy-MM-dd")));

        Assert.Equal(HttpStatusCode.BadRequest, cerrado.StatusCode);
        Assert.Contains("no esté Abierto", await cerrado.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinPeriodo.StatusCode);
        Assert.Contains("No existe un periodo contable", await sinPeriodo.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM pagoproveedorcabecera WHERE proveedor_id = $1", e.ProveedorId));
    }

    [Fact]
    public async Task CrearPago_ConMontoDeTresDecimales_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ProveedorId);

        var response = await client.PostAsJsonAsync("/api/cxp/pagos", PayloadPago(e.ProveedorId, documentoId, 10.005m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Los montos del pago no pueden tener más de 2 decimales.", await response.LeerErrorAsync());
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
    }

    private static Dictionary<string, object?> PayloadFactura(Escenario e, string numero, int proveedorId, int cuentaLineaId) =>
        FacturaPayloads.Cxp(e, numero, new[] { FacturaPayloads.LineaExenta(e, cuentaLineaId) }, proveedorId);

    private static object PayloadPago(int proveedorId, int documentoId, decimal monto, int? cuentaBancariaId, string fecha = "2025-03-20") => new
    {
        proveedorId,
        fecha,
        metodoPago = "Transferencia",
        cuentaBancariaId,
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    private static object PayloadPagoMultiple(int proveedorId, int cuentaBancariaId, params (int DocumentoId, decimal Monto)[] aplicaciones) => new
    {
        proveedorId,
        fecha = "2025-03-20",
        metodoPago = "Transferencia",
        cuentaBancariaId,
        aplicaciones = aplicaciones.Select(a => new { documentoId = a.DocumentoId, montoAplicado = a.Monto }).ToArray(),
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
