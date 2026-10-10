using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FacturasCxPFiscalTests
{
    private const string Ruta = "/api/cxp/facturas";

    private readonly PostgresFixture _pg;

    public FacturasCxPFiscalTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static string Numero() => $"P-{TestData.Sufijo()}";

    private static Dictionary<string, object?> Compra(Escenario e, int impuestoId, decimal precio = 100m, int? proveedorId = null, decimal cantidad = 1m) =>
        FacturaPayloads.Cxp(e, Numero(), new[] { FacturaPayloads.Linea(impuestoId, e.CuentaGasto, cantidad, precio) }, proveedorId);

    [Fact]
    public async Task CrearFactura_IvaSinCredito_LlevaElIvaAlGastoSinLineaDeIva()
    {
        var (e, client) = await _pg.PrepararAsync();

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, Compra(e, e.ImpuestoSinCreditoId, 100m, cantidad: 2m));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((200m, 21.43m), (cuerpo.GetProperty("montoTotal").GetDecimal(), cuerpo.GetProperty("montoIva").GetDecimal()));
        var lineas = await _pg.Data.LineasDeAsientoAsync(cuerpo.GetProperty("asientoId").GetInt32());
        Assert.Equal(new[] { (e.CuentaCxP, 0m, 200m), (e.CuentaGasto, 200m, 0m) }, lineas);
    }

    [Fact]
    public async Task CrearFactura_DePequenoContribuyente_RegistraTodoElValorComoGasto()
    {
        var (e, client) = await _pg.PrepararAsync();
        var pequeno = await _pg.Data.CrearContraparteFiscalAsync("Proveedor", "PEQUENO_CONTRIBUYENTE", TestData.NitValido);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, Compra(e, e.ImpuestoPequenoId, proveedorId: pequeno));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal((100m, 0m), (cuerpo.GetProperty("montoTotal").GetDecimal(), cuerpo.GetProperty("montoIva").GetDecimal()));
        var lineas = await _pg.Data.LineasDeAsientoAsync(cuerpo.GetProperty("asientoId").GetInt32());
        Assert.Equal(new[] { (e.CuentaCxP, 0m, 100m), (e.CuentaGasto, 100m, 0m) }, lineas);
    }

    [Theory]
    [InlineData(0, "Para registrar crédito fiscal se requieren el UUID, la serie y el número")]
    [InlineData(1, "deben indicar UUID, serie y número juntos")]
    [InlineData(2, "no tiene NIT registrado")]
    [InlineData(3, "es pequeño contribuyente")]
    [InlineData(4, "solo puede usarse con proveedores inscritos")]
    [InlineData(5, "exento de IVA: solo puede usar impuestos exentos o no afectos")]
    public async Task CrearFactura_ConDatosFiscalesInvalidos_Responde400(int caso, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var sinNit = await _pg.Data.CrearContraparteFiscalAsync("Proveedor", "GENERAL", null);
        var pequeno = await _pg.Data.CrearContraparteFiscalAsync("Proveedor", "PEQUENO_CONTRIBUYENTE", TestData.NitValido);
        var exento = await _pg.Data.CrearContraparteFiscalAsync("Proveedor", "EXENTO", TestData.NitValido);
        var payload = caso switch
        {
            0 => Compra(e, e.ImpuestoIvaId),
            1 => Compra(e, e.ImpuestoExentoId).Con("dteSerie", "SOLO-SERIE"),
            2 => Compra(e, e.ImpuestoIvaId, proveedorId: sinNit).ConDte(),
            3 => Compra(e, e.ImpuestoIvaId, proveedorId: pequeno).ConDte(),
            5 => Compra(e, e.ImpuestoIvaId, proveedorId: exento).ConDte(),
            _ => Compra(e, e.ImpuestoPequenoId),
        };

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains(fragmento, cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearFactura_ConSerieYNumeroRepetidos_SoloSeRechazaParaElMismoProveedor()
    {
        var (e, client) = await _pg.PrepararAsync();
        var otroProveedor = await _pg.Data.CrearContraparteAsync("Proveedor");
        var primera = Compra(e, e.ImpuestoExentoId).ConDte();
        Assert.Equal(HttpStatusCode.Created, (await client.EnviarAsync(Ruta, primera)).Estado);
        var repetida = Compra(e, e.ImpuestoExentoId).ConDte().Con("dteSerie", primera["dteSerie"]).Con("dteNumero", primera["dteNumero"]);

        var (estadoMismo, cuerpoMismo) = await client.EnviarAsync(Ruta, repetida);
        var (estadoOtro, _) = await client.EnviarAsync(Ruta, new Dictionary<string, object?>(repetida) { ["numero"] = Numero(), ["proveedorId"] = otroProveedor, ["dteUuid"] = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Conflict, estadoMismo);
        Assert.Contains("El proveedor ya tiene registrado el DTE serie", cuerpoMismo.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, estadoOtro);
    }

    [Fact]
    public async Task CrearFactura_ConUuidRepetido_Responde409()
    {
        var (e, client) = await _pg.PrepararAsync();
        var primera = Compra(e, e.ImpuestoExentoId).ConDte();
        Assert.Equal(HttpStatusCode.Created, (await client.EnviarAsync(Ruta, primera)).Estado);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, Compra(e, e.ImpuestoExentoId).ConDte().Con("dteUuid", primera["dteUuid"]));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("Ya existe una factura con el UUID de DTE", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearFactura_ConCreditoYSinCuentaDeIvaCredito_Responde409()
    {
        var (e, client) = await _pg.PrepararAsync();
        var payload = Compra(e, e.ImpuestoIvaId).ConDte();
        try
        {
            await _pg.Data.ConfigurarCuentasFiscalesAsync(e.CuentaIvaDebito, null);

            var (estado, cuerpo) = await client.EnviarAsync(Ruta, payload);

            Assert.Equal(HttpStatusCode.Conflict, estado);
            Assert.StartsWith("Falta configurar la cuenta de IVA crédito fiscal", cuerpo.GetProperty("error").GetString());
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Fact]
    public async Task NotaDeCredito_ConIvaCredito_InvierteElAsientoYReduceElSaldo()
    {
        var (e, client) = await _pg.PrepararAsync();
        var (_, factura) = await client.EnviarAsync(Ruta, Compra(e, e.ImpuestoIvaId, 112m).ConDte());
        var facturaId = factura.GetProperty("id").GetInt32();
        var nota = Compra(e, e.ImpuestoIvaId, 56m).ConDte().Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", facturaId);

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, nota);

        Assert.Equal(HttpStatusCode.Created, estado);
        var lineas = await _pg.Data.LineasDeAsientoAsync(cuerpo.GetProperty("asientoId").GetInt32());
        Assert.Equal(new[] { (e.CuentaCxP, 56m, 0m), (e.CuentaGasto, 0m, 50m), (e.CuentaIvaCredito, 0m, 6m) }, lineas);
        var origen = await client.GetFromJsonAsync<JsonElement>($"{Ruta}/{facturaId}");
        Assert.Equal(56m, origen.GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task NotaDeCredito_MayorAlSaldoDeLaFacturaOrigen_Responde409()
    {
        var (e, client) = await _pg.PrepararAsync();
        var (_, factura) = await client.EnviarAsync(Ruta, Compra(e, e.ImpuestoExentoId));
        var nota = Compra(e, e.ImpuestoExentoId, 100.01m).Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", factura.GetProperty("id").GetInt32());

        var (estado, cuerpo) = await client.EnviarAsync(Ruta, nota);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("(Q100.01) excede el saldo pendiente del documento de origen (Q100.00)", cuerpo.GetProperty("error").GetString());
    }
}
