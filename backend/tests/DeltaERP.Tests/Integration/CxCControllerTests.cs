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
    public async Task CrearFactura_ConClienteInexistente_RespondeBadRequestSinCrearNada()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var numero = $"F-{TestData.Sufijo()}";
        var client = _pg.CreateApiClient(e.UsuarioId);

        var response = await client.PostAsJsonAsync("/api/cxc/facturas", PayloadFactura(e, numero, IdInexistente, e.CuentaIngreso, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("no existe", error);
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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var pagoValido = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 40m, cuenta.Id));
        Assert.Equal(HttpStatusCode.Created, pagoValido.StatusCode);
        Assert.Equal(60m, await ObtenerSaldoAsync(client, documentoId));

        var pagoExcedido = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 60.01m, cuenta.Id));

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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var otroClienteId = await _pg.Data.CrearContraparteAsync("Cliente");
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(otroClienteId, documentoId, 10m, cuenta.Id));

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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);
        await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id));

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
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id));

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
    public async Task CrearPago_ComoVendedor_Responde403()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
    }

    [Fact]
    public async Task CrearPago_SinCuentaBancaria_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La cuenta bancaria es obligatoria para registrar un cobro o un pago.", await response.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
    }

    [Fact]
    public async Task CrearPago_CreaMovimientoYAsientoConControlDerivado()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 40m, cuenta.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reciboId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        Assert.Equal("Ingreso", await _pg.Data.ScalarAsync<string>("SELECT tipo FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal("CxC", await _pg.Data.ScalarAsync<string>("SELECT origen FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(40m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        Assert.Equal(cuenta.Id, await _pg.Data.ScalarAsync<int>("SELECT cuenta_bancaria_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId);
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(40m, await _pg.Data.DebitoAsync(asientoId, cuenta.CuentaContableId));
        Assert.Equal(40m, await _pg.Data.CreditoAsync(asientoId, e.CuentaCxC));
        Assert.Equal(40m, await _pg.Data.SaldoBancarioAsync(cuenta.Id));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "registrar_pago_cxc"));
    }

    [Fact]
    public async Task CrearPago_ConFacturasDeControlesDistintos_GeneraUnaLineaPorControl()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var otraCuentaCxC = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var primeraId = await CrearFacturaAsync(client, e, e.ClienteId);
        var segundaId = await CrearFacturaAsync(client, e with { CuentaCxC = otraCuentaCxC }, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPagoMultiple(e.ClienteId, cuenta.Id, (primeraId, 30m), (segundaId, 20m)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reciboId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId);
        Assert.Equal(3, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(50m, await _pg.Data.DebitoAsync(asientoId, cuenta.CuentaContableId));
        Assert.Equal(30m, await _pg.Data.CreditoAsync(asientoId, e.CuentaCxC));
        Assert.Equal(20m, await _pg.Data.CreditoAsync(asientoId, otraCuentaCxC));
        Assert.Equal(50m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE recibo_pago_id = $1", reciboId));
    }

    [Fact]
    public async Task CrearPago_ConFacturaSinAsiento_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var numero = $"F-{TestData.Sufijo()}";
        var documentoId = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, estado) VALUES ($1, 'Factura', $2, $3, $4, 100, 'Vigente') RETURNING id",
            numero, e.ClienteId, new DateOnly(2025, 3, 15), new DateOnly(2025, 4, 15));

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal($"No se pudo determinar la cuenta de control de la factura {numero}.", await response.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
    }

    [Fact]
    public async Task CrearPago_ConSaldoRN05Excedido_NoCreaMovimiento()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 40m, cuenta.Id))).StatusCode);

        var excedido = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 60.01m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, excedido.StatusCode);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuenta.Id));
        Assert.Equal(40m, await _pg.Data.SaldoBancarioAsync(cuenta.Id));
    }

    [Fact]
    public async Task CrearPago_ConCuentaBancariaInactivaOInexistente_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET activa = false WHERE id = $1", cuenta.Id);

        var inexistente = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, IdInexistente));
        var inactiva = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Equal("La cuenta bancaria indicada no existe.", await inexistente.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, inactiva.StatusCode);
        Assert.Contains("está inactiva", await inactiva.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
    }

    [Fact]
    public async Task CrearPago_EnPeriodoCerradoOSinPeriodo_Responde400YNoDejaRecibo()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");

        var cerrado = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id, inicio.AddDays(2).ToString("yyyy-MM-dd")));
        var sinPeriodo = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 50m, cuenta.Id, TestData.FechaSinPeriodo.ToString("yyyy-MM-dd")));

        Assert.Equal(HttpStatusCode.BadRequest, cerrado.StatusCode);
        Assert.Contains("no esté Abierto", await cerrado.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinPeriodo.StatusCode);
        Assert.Contains("No existe un periodo contable", await sinPeriodo.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM recibopagocliente WHERE cliente_id = $1", e.ClienteId));
    }

    [Fact]
    public async Task CrearPago_ConMontoDeTresDecimales_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var client = _pg.CreateApiClient(e.UsuarioId);
        var documentoId = await CrearFacturaAsync(client, e, e.ClienteId);

        var response = await client.PostAsJsonAsync("/api/cxc/pagos", PayloadPago(e.ClienteId, documentoId, 10.005m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Los montos del pago no pueden tener más de 2 decimales.", await response.LeerErrorAsync());
        Assert.Equal(100m, await ObtenerSaldoAsync(client, documentoId));
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

    private static object PayloadPago(int clienteId, int documentoId, decimal monto, int? cuentaBancariaId, string fecha = "2025-03-20") => new
    {
        clienteId,
        fecha,
        metodoPago = "Efectivo",
        cuentaBancariaId,
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    private static object PayloadPagoMultiple(int clienteId, int cuentaBancariaId, params (int DocumentoId, decimal Monto)[] aplicaciones) => new
    {
        clienteId,
        fecha = "2025-03-20",
        metodoPago = "Efectivo",
        cuentaBancariaId,
        aplicaciones = aplicaciones.Select(a => new { documentoId = a.DocumentoId, montoAplicado = a.Monto }).ToArray(),
    };

    private static async Task<int> CrearFacturaAsync(HttpClient client, Escenario e, int clienteId)
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
