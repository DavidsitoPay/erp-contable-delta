using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ReporteRetencionesTests
{
    private const string Ruta = "/api/retenciones/reporte";
    private const int AnioSinMovimientos = 2001;

    private readonly PostgresFixture _pg;

    public ReporteRetencionesTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private sealed record Mes(int Anio, Escenario E, HttpClient Client, int ProveedorId, int ClienteId, int RetencionIsr, int RetencionIva);

    // Un año propio por prueba: sus totales y su conciliación no se mezclan con los de otras pruebas.
    private async Task<Mes> SembrarJunioAsync()
    {
        var anio = SiguienteAnio();
        var e = await _pg.PrepararEnAnioAsync(anio);
        var client = _pg.CreateApiClient(e.UsuarioId);
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var cliente = await _pg.Data.CrearClienteAgenteAsync("SECTOR_PUBLICO");
        var banco = await _pg.Data.CrearCuentaBancariaAsync();

        var compra = CompraExenta(e, proveedor, 10000m).EnFecha(new DateOnly(anio, 6, 10));
        var (_, retencionIsr, _) = await client.RegistrarCompraAsync(compra);
        var factura = await client.RegistrarVentaAsync(VentaConIva(e, cliente, 33600m).EnFecha(new DateOnly(anio, 6, 10)));
        var cobro = Cobro(cliente, banco.Id, $"{anio}-06-20", Aplicacion(factura, 33600m, RetencionIva(900m, numeroConstancia: $"A-{TestData.Sufijo()}", fechaConstancia: $"{anio}-06-10")));
        var (estado, cuerpoCobro) = await client.EnviarAsync(RutaPagosCxC, cobro);
        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.NotNull(retencionIsr);
        return new Mes(anio, e, client, proveedor, cliente, retencionIsr.Value, cuerpoCobro.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32());
    }

    private static async Task<JsonElement> ConsultarAsync(HttpClient client, string consulta) =>
        await client.GetFromJsonAsync<JsonElement>($"{Ruta}?{consulta}");

    [Fact]
    public async Task Reporte_DelMes_ListaIsrEIvaConTotalesYConciliacionSinDiferencia()
    {
        var m = await SembrarJunioAsync();

        var reporte = await ConsultarAsync(m.Client, $"anio={m.Anio}&mes=6");

        var filas = reporte.GetProperty("filas").EnumerateArray().ToDictionary(f => f.GetProperty("retencionId").GetInt32());
        Assert.Equal(2, filas.Count);
        var isr = filas[m.RetencionIsr];
        Assert.Equal(("EFECTUADA", "ISR_ROS", 10000m, 5m, 5m, 500m, "VENCIDA", false), (
            isr.GetProperty("sentido").GetString(), isr.GetProperty("concepto").GetString(), isr.GetProperty("base").GetDecimal(),
            isr.GetProperty("tasa").GetDecimal(), isr.GetProperty("tasaEfectiva").GetDecimal(), isr.GetProperty("monto").GetDecimal(),
            isr.GetProperty("estadoConstancia").GetString(), isr.GetProperty("periodoContableDistinto").GetBoolean()));
        var iva = filas[m.RetencionIva];
        Assert.Equal(("RECIBIDA", "IVA_CLIENTE", 3600m, 25m, 900m, "EMITIDA", false), (
            iva.GetProperty("sentido").GetString(), iva.GetProperty("concepto").GetString(), iva.GetProperty("base").GetDecimal(),
            iva.GetProperty("tasa").GetDecimal(), iva.GetProperty("monto").GetDecimal(), iva.GetProperty("estadoConstancia").GetString(),
            iva.GetProperty("periodoContableDistinto").GetBoolean()));
        Assert.NotEqual(JsonValueKind.Null, iva.GetProperty("constanciaNumero").ValueKind);

        var totales = reporte.GetProperty("totales").EnumerateArray().ToDictionary(t => t.GetProperty("concepto").GetString() ?? string.Empty);
        Assert.Equal((1, 10000m, 500m, 1), (
            totales["ISR_ROS"].GetProperty("cantidad").GetInt32(), totales["ISR_ROS"].GetProperty("base").GetDecimal(),
            totales["ISR_ROS"].GetProperty("monto").GetDecimal(), totales["ISR_ROS"].GetProperty("sinConstancia").GetInt32()));
        Assert.Equal((1, 3600m, 900m, 0), (
            totales["IVA_CLIENTE"].GetProperty("cantidad").GetInt32(), totales["IVA_CLIENTE"].GetProperty("base").GetDecimal(),
            totales["IVA_CLIENTE"].GetProperty("monto").GetDecimal(), totales["IVA_CLIENTE"].GetProperty("sinConstancia").GetInt32()));

        var conciliacion = reporte.GetProperty("conciliacion").EnumerateArray().ToDictionary(c => c.GetProperty("concepto").GetString() ?? string.Empty);
        Assert.Equal((m.E.CuentaIsrRetenido, TestData.CodigoCuentaIsrRetenido, 500m, 500m, 0m), (
            conciliacion["ISR_ROS"].GetProperty("cuentaId").GetInt32(), conciliacion["ISR_ROS"].GetProperty("cuentaCodigo").GetString(),
            conciliacion["ISR_ROS"].GetProperty("totalReporte").GetDecimal(), conciliacion["ISR_ROS"].GetProperty("movimientoContable").GetDecimal(),
            conciliacion["ISR_ROS"].GetProperty("diferencia").GetDecimal()));
        Assert.Equal((900m, 900m, 0m), (
            conciliacion["IVA_CLIENTE"].GetProperty("totalReporte").GetDecimal(), conciliacion["IVA_CLIENTE"].GetProperty("movimientoContable").GetDecimal(),
            conciliacion["IVA_CLIENTE"].GetProperty("diferencia").GetDecimal()));
    }

    [Theory]
    [InlineData("sentido=RECIBIDA", "IVA_CLIENTE")]
    [InlineData("sentido=efectuada", "ISR_ROS")]
    [InlineData("concepto=ISR_ROS", "ISR_ROS")]
    [InlineData("estadoConstancia=EMITIDA", "IVA_CLIENTE")]
    [InlineData("estadoConstancia=VENCIDA", "ISR_ROS")]
    [InlineData("nit=1234567-9", "ISR_ROS")]
    public async Task Reporte_ConFiltros_DevuelveSoloLasFilasQueCoinciden(string filtro, string conceptoEsperado)
    {
        var m = await SembrarJunioAsync();

        var reporte = await ConsultarAsync(m.Client, $"anio={m.Anio}&mes=6&{filtro}");

        var fila = Assert.Single(reporte.GetProperty("filas").EnumerateArray());
        Assert.Equal(conceptoEsperado, fila.GetProperty("concepto").GetString());
    }

    [Fact]
    public async Task Reporte_ConCobroEnOtroMes_MarcaElPeriodoContableDistintoYMuestraLaDiferencia()
    {
        var anio = SiguienteAnio();
        var e = await _pg.PrepararEnAnioAsync(anio);
        var client = _pg.CreateApiClient(e.UsuarioId);
        var cliente = await _pg.Data.CrearClienteAgenteAsync("SECTOR_PUBLICO");
        var banco = await _pg.Data.CrearCuentaBancariaAsync();
        var factura = await client.RegistrarVentaAsync(VentaConIva(e, cliente, 33600m).EnFecha(new DateOnly(anio, 5, 10)));
        var cobro = await client.EnviarAsync(RutaPagosCxC, Cobro(cliente, banco.Id, $"{anio}-06-05", Aplicacion(factura, 33600m, RetencionIva(900m))));
        Assert.Equal(HttpStatusCode.Created, cobro.Estado);

        var mayo = await ConsultarAsync(client, $"anio={anio}&mes=5");
        var junio = await ConsultarAsync(client, $"anio={anio}&mes=6");

        var fila = Assert.Single(mayo.GetProperty("filas").EnumerateArray());
        Assert.True(fila.GetProperty("periodoContableDistinto").GetBoolean());
        var conciliacion = mayo.GetProperty("conciliacion").EnumerateArray().Single(c => c.GetProperty("concepto").GetString() == "IVA_CLIENTE");
        Assert.Equal((900m, 0m, -900m), (
            conciliacion.GetProperty("totalReporte").GetDecimal(), conciliacion.GetProperty("movimientoContable").GetDecimal(),
            conciliacion.GetProperty("diferencia").GetDecimal()));
        Assert.Empty(junio.GetProperty("filas").EnumerateArray());
        Assert.Equal(900m, junio.GetProperty("conciliacion").EnumerateArray().Single(c => c.GetProperty("concepto").GetString() == "IVA_CLIENTE").GetProperty("movimientoContable").GetDecimal());
    }

    [Fact]
    public async Task Reporte_DeUnMesSinRetenciones_DevuelveListasVaciasYConciliacionEnCero()
    {
        var (_, client) = await _pg.PrepararAsync();

        var reporte = await ConsultarAsync(client, $"anio={AnioSinMovimientos}&mes=3");

        Assert.Empty(reporte.GetProperty("filas").EnumerateArray());
        Assert.Empty(reporte.GetProperty("totales").EnumerateArray());
        Assert.All(reporte.GetProperty("conciliacion").EnumerateArray(), c => Assert.Equal(0m, c.GetProperty("diferencia").GetDecimal()));
    }

    [Fact]
    public async Task Reporte_SinCuentasDeRetencionConfiguradas_OmiteLaConciliacion()
    {
        var (_, client) = await _pg.PrepararAsync();
        try
        {
            await _pg.Data.ConfigurarCuentasRetencionAsync(null, null);

            var reporte = await ConsultarAsync(client, $"anio={AnioSinMovimientos}&mes=3");

            Assert.Empty(reporte.GetProperty("conciliacion").EnumerateArray());
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Theory]
    [InlineData("anio=2025&mes=13", "El mes debe estar entre 1 y 12.")]
    [InlineData("anio=1999&mes=6", "El año debe estar entre 2000 y 2100.")]
    [InlineData("anio=2025&mes=6&sentido=OTRO", "Sentido inválido")]
    [InlineData("anio=2025&mes=6&concepto=OTRO", "Concepto inválido")]
    [InlineData("anio=2025&mes=6&estadoConstancia=OTRO", "Estado de constancia inválido")]
    public async Task Reporte_ConFiltrosInvalidos_Responde400(string consulta, string fragmento)
    {
        var (_, client) = await _pg.PrepararAsync();

        var response = await client.GetAsync($"{Ruta}?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(fragmento, await response.LeerErrorAsync());
    }

    [Theory]
    [InlineData(Roles.Administrador, HttpStatusCode.OK)]
    [InlineData(Roles.Contador, HttpStatusCode.OK)]
    [InlineData(Roles.Vendedor, HttpStatusCode.Forbidden)]
    public async Task Reporte_SegunElPerfil(string rol, HttpStatusCode esperado)
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(rol), rol);

        var response = await client.GetAsync($"{Ruta}?anio=2025&mes=6");

        Assert.Equal(esperado, response.StatusCode);
    }

    [Fact]
    public async Task Reporte_SinAutenticar_Responde401()
    {
        var response = await _pg.Factory.CreateClient().GetAsync($"{Ruta}?anio=2025&mes=6");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LibroFiscal_ConMesInvalido_ConservaElMensajeDeValidacion()
    {
        var (_, client) = await _pg.PrepararAsync();

        var response = await client.GetAsync("/api/libros/fiscal/compras?anio=2025&mes=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El mes debe estar entre 1 y 12.", await response.LeerErrorAsync());
    }
}
