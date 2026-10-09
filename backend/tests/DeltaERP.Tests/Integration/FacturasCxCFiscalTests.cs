using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FacturasCxCFiscalTests
{
    private const string Ruta = "/api/cxc/facturas";
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public FacturasCxCFiscalTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static string Numero() => $"F-{TestData.Sufijo()}";

    private static Dictionary<string, object?> Exenta(Escenario e, decimal precio = 100m) =>
        FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoExentoId, e.CuentaIngreso, precio: precio) });

    private static Dictionary<string, object?> NotaCredito(Escenario e, int origenId, decimal monto, int? clienteId = null) =>
        FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoExentoId, e.CuentaIngreso, precio: monto) }, clienteId)
            .Con("tipoDocumento", "NotaCredito")
            .Con("documentoOrigenId", origenId);

    [Fact]
    public async Task CrearFactura_Exenta_NoGeneraLineaDeIvaYDejaElDocumentoEnFormatoNuevo()
    {
        var (e, client) = await _pg.PrepararAsync();

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, Exenta(e));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((100m, 100m, 0m, false), (cuerpo.GetProperty("montoTotal").GetDecimal(), cuerpo.GetProperty("montoBase").GetDecimal(), cuerpo.GetProperty("montoIva").GetDecimal(), cuerpo.GetProperty("calculoLegado").GetBoolean()));
        var lineas = await _pg.Data.LineasDeAsientoAsync(cuerpo.GetProperty("asientoId").GetInt32());
        Assert.Equal(new[] { (e.CuentaCxC, 100m, 0m), (e.CuentaIngreso, 0m, 100m) }, lineas);
    }

    [Fact]
    public async Task CrearFactura_IgnoraLosMontosYElTipoDeCambioQueEnviaElCliente()
    {
        var (e, client) = await _pg.PrepararAsync();
        var payload = Exenta(e).Con("tipoCambioAplicado", 7.75m).Con("montoTotal", 999m).Con("montoIva", 5m).Con("calculoLegado", true);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((100m, 0m, 1m, false), (cuerpo.GetProperty("montoTotal").GetDecimal(), cuerpo.GetProperty("montoIva").GetDecimal(), cuerpo.GetProperty("tipoCambioAplicado").GetDecimal(), cuerpo.GetProperty("calculoLegado").GetBoolean()));
    }

    [Fact]
    public async Task ObtenerYListar_ExponenLosDatosFiscalesYElDteDeLaFactura()
    {
        var (e, client) = await _pg.PrepararAsync();
        var payload = FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, precio: 112m, tipoBienServicio: "BIEN") });
        var (_, creada) = await client.EnviarAsync(Ruta, payload);
        var id = creada.GetProperty("id").GetInt32();

        var detalle = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        var lista = (await client.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray().First(f => f.GetProperty("id").GetInt32() == id);

        var linea = detalle.GetProperty("lineas")[0];
        Assert.Equal(("IVA_GENERAL", 12m, 112m, 100m, 12m, "BIEN"), (linea.GetProperty("impuestoCodigo").GetString(), linea.GetProperty("tasaAplicada").GetDecimal(), linea.GetProperty("montoLinea").GetDecimal(), linea.GetProperty("montoBase").GetDecimal(), linea.GetProperty("montoIva").GetDecimal(), linea.GetProperty("tipoBienServicio").GetString()));
        Assert.Equal((payload["dteSerie"], payload["dteNumero"]), (detalle.GetProperty("dteSerie").GetString(), detalle.GetProperty("dteNumero").GetString()));
        Assert.Equal((12m, payload["dteSerie"]), (lista.GetProperty("montoIva").GetDecimal(), lista.GetProperty("dteSerie").GetString()));
    }

    [Theory]
    [InlineData(0, "requieren los datos del DTE")]
    [InlineData(1, "no existe en el catálogo")]
    [InlineData(2, "está inactivo")]
    [InlineData(3, "no está vigente el 2025-03-15")]
    [InlineData(4, "solo aplica a compras")]
    [InlineData(5, "Cada línea debe indicar un impuesto del catálogo (impuestoId).")]
    [InlineData(6, "Cada línea debe indicar si es un BIEN o un SERVICIO.")]
    [InlineData(7, "no puede ser futura")]
    [InlineData(8, "no pertenece al periodo contable")]
    [InlineData(9, "exento de IVA")]
    [InlineData(10, "debe referenciar la factura de origen")]
    [InlineData(11, "formato canónico de 36 caracteres")]
    public async Task CrearFactura_ConDatosFiscalesInvalidos_Responde400SinCrearNada(int caso, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var payload = await PayloadInvalidoAsync(e, caso);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains(fragmento, cuerpo.GetProperty("error").GetString());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE numero = $1", payload["numero"]!));
    }

    private async Task<Dictionary<string, object?>> PayloadInvalidoAsync(Escenario e, int caso)
    {
        var inactivo = await _pg.Data.CrearImpuestoAsync(new OpcionesImpuesto(Activo: false));
        var futuro = await _pg.Data.CrearImpuestoAsync(new OpcionesImpuesto(Desde: new DateOnly(2030, 1, 1)));
        var clienteExento = await _pg.Data.CrearContraparteFiscalAsync("Cliente", "EXENTO", null);
        var conIva = new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso) };
        return caso switch
        {
            0 => Exenta(e).Sin("dteUuid"),
            1 => ConImpuesto(e, IdInexistente),
            2 => ConImpuesto(e, inactivo),
            3 => ConImpuesto(e, futuro),
            4 => ConImpuesto(e, e.ImpuestoPequenoId),
            5 => FacturaPayloads.Cxc(e, Numero(), new object[] { new { descripcion = "Heredada", cantidad = 1m, precioUnitario = 100m, porcentajeImpuesto = 12m, tipoBienServicio = "SERVICIO", cuentaContableId = e.CuentaIngreso } }),
            6 => FacturaPayloads.Cxc(e, Numero(), new object[] { new { descripcion = "Sin tipo", cantidad = 1m, precioUnitario = 100m, impuestoId = e.ImpuestoExentoId, cuentaContableId = e.CuentaIngreso } }),
            7 => Exenta(e).Con("dteFechaCertificacion", DateTimeOffset.UtcNow.AddDays(2)),
            8 => Exenta(e).EnFecha(new DateOnly(2026, 3, 15)),
            9 => FacturaPayloads.Cxc(e, Numero(), conIva, clienteExento),
            10 => Exenta(e).Con("tipoDocumento", "NotaCredito"),
            _ => Exenta(e).Con("dteUuid", "3f2504e04f8941d39a0c0305e82c3301"),
        };
    }

    private static Dictionary<string, object?> ConImpuesto(Escenario e, int impuestoId) =>
        FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(impuestoId, e.CuentaIngreso) });

    [Theory]
    [InlineData(true, "Ya existe una factura con el UUID de DTE")]
    [InlineData(false, "Ya existe una factura de venta con el DTE serie")]
    public async Task CrearFactura_ConDteRepetido_Responde409(bool repiteUuid, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var primera = Exenta(e);
        Assert.Equal(HttpStatusCode.Created, (await client.EnviarAsync(Ruta, primera)).Estado);
        var segunda = repiteUuid
            ? Exenta(e).Con("dteUuid", primera["dteUuid"])
            : Exenta(e).Con("dteSerie", primera["dteSerie"]).Con("dteNumero", primera["dteNumero"]);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, segunda);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains(fragmento, cuerpo.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrearFactura_ConIvaYCuentaDeIvaNoUtilizable_Responde409(bool cuentaInactiva)
    {
        var (e, client) = await _pg.PrepararAsync();
        var inactiva = await _pg.Data.CrearCuentaInactivaAsync("Pasivo", "Acreedora");
        var conIva = FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso) });
        try
        {
            await _pg.Data.ConfigurarCuentasFiscalesAsync(cuentaInactiva ? inactiva : null, e.CuentaIvaCredito);

            var (estado, cuerpo) = await client.EnviarAsync(Ruta, conIva);
            var exenta = await client.EnviarAsync(Ruta, Exenta(e));

            Assert.Equal(HttpStatusCode.Conflict, estado);
            var mensaje = cuerpo.GetProperty("error").GetString();
            Assert.StartsWith(cuentaInactiva ? "La cuenta de IVA débito fiscal configurada no es utilizable" : "Falta configurar la cuenta de IVA débito fiscal", mensaje);
            Assert.Equal(HttpStatusCode.Created, exenta.Estado);
            Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxc WHERE numero = $1", conIva["numero"]!));
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Fact]
    public async Task NotaDeCredito_InvierteElAsientoYReduceElSaldoDeLaFacturaOrigen()
    {
        var (e, client) = await _pg.PrepararAsync();
        var origen = Exenta(e);
        var (_, factura) = await client.EnviarAsync(Ruta, origen);
        var facturaId = factura.GetProperty("id").GetInt32();

        var (estado, nota) = await client.EnviarAsync(Ruta, NotaCredito(e, facturaId, 40m));

        Assert.Equal(HttpStatusCode.Created, estado);
        var lineas = await _pg.Data.LineasDeAsientoAsync(nota.GetProperty("asientoId").GetInt32());
        Assert.Equal(new[] { (e.CuentaCxC, 0m, 40m), (e.CuentaIngreso, 40m, 0m) }, lineas);
        var detalleOrigen = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{facturaId}");
        var detalleNota = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{nota.GetProperty("id").GetInt32()}");
        Assert.Equal(60m, detalleOrigen.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal((0m, facturaId, origen["dteSerie"]), (detalleNota.GetProperty("saldoPendiente").GetDecimal(), detalleNota.GetProperty("documentoOrigenId").GetInt32(), detalleNota.GetProperty("dteSerieOrigen").GetString()));
    }

    [Theory]
    [InlineData(0, HttpStatusCode.Conflict, "excede el saldo pendiente del documento de origen (Q90.00)")]
    [InlineData(1, HttpStatusCode.BadRequest, "pertenece a otro cliente")]
    [InlineData(2, HttpStatusCode.BadRequest, "El documento de origen no existe.")]
    [InlineData(3, HttpStatusCode.BadRequest, "debe ser una factura vigente")]
    public async Task NotaDeCredito_ConOrigenOMontoInvalido_Rechaza(int caso, HttpStatusCode esperado, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var (_, factura) = await client.EnviarAsync(Ruta, Exenta(e));
        var facturaId = factura.GetProperty("id").GetInt32();
        var (_, primeraNota) = await client.EnviarAsync(Ruta, NotaCredito(e, facturaId, 10m));
        var otroCliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var payload = caso switch
        {
            0 => NotaCredito(e, facturaId, 150m),
            1 => NotaCredito(e, facturaId, 10m, otroCliente),
            2 => NotaCredito(e, IdInexistente, 10m),
            _ => NotaCredito(e, primeraNota.GetProperty("id").GetInt32(), 5m),
        };

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(esperado, estado);
        var mensaje = cuerpo.GetProperty("error").GetString();
        Assert.Contains(fragmento, mensaje);
    }

    [Fact]
    public async Task NotaDeCredito_NoAdmitePagosYElPagoPosteriorSeLimitaAlSaldoReducido()
    {
        var (e, client) = await _pg.PrepararAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        var (_, factura) = await client.EnviarAsync(Ruta, Exenta(e));
        var facturaId = factura.GetProperty("id").GetInt32();
        var (_, nota) = await client.EnviarAsync(Ruta, NotaCredito(e, facturaId, 40m));

        var pagoANota = await client.PostAsJsonAsync("/api/cxc/pagos", PagoA(e, nota.GetProperty("id").GetInt32(), 10m, cuenta.Id));
        var pagoExcedido = await client.PostAsJsonAsync("/api/cxc/pagos", PagoA(e, facturaId, 60.01m, cuenta.Id));
        var pagoCompleto = await client.PostAsJsonAsync("/api/cxc/pagos", PagoA(e, facturaId, 60m, cuenta.Id));

        Assert.Equal(HttpStatusCode.BadRequest, pagoANota.StatusCode);
        Assert.Equal("Una nota de crédito no admite pagos ni cobros; solo reduce el saldo contable.", await pagoANota.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, pagoExcedido.StatusCode);
        Assert.Contains("RN-05", await pagoExcedido.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.Created, pagoCompleto.StatusCode);
        Assert.Equal(0m, (await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{facturaId}")).GetProperty("saldoPendiente").GetDecimal());
    }

    private static object PagoA(Escenario e, int documentoId, decimal monto, int cuentaBancariaId) => new
    {
        clienteId = e.ClienteId,
        fecha = "2025-03-20",
        metodoPago = "Efectivo",
        cuentaBancariaId,
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    [Fact]
    public async Task CrearFactura_ConIvaYVendedor_SeAsientaEnLaCuentaDeIvaDebito()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var vendedor = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);
        var payload = FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, precio: 112m) });

        var (estado, cuerpo) = await vendedor.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(12m, await _pg.Data.CreditoAsync(cuerpo.GetProperty("asientoId").GetInt32(), e.CuentaIvaDebito));
    }
}
