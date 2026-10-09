using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class LibroFiscalControllerTests
{
    private static int _anioLibro = 2009;

    private readonly PostgresFixture _pg;

    public LibroFiscalControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    // Cada prueba usa un año propio (certificación DTE no puede ser futura) para aislar el libro de otras pruebas.
    private async Task<(Escenario E, HttpClient Client, int Anio)> PrepararAsync()
    {
        var anio = Interlocked.Increment(ref _anioLibro);
        var e = await _pg.Data.SembrarEscenarioAsync();
        e = e with { PeriodoId = await _pg.Data.CrearPeriodoEnRangoAsync(new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31)) };
        return (e, _pg.CreateApiClient(e.UsuarioId), anio);
    }

    private static async Task<int> EnviarAsync(HttpClient client, string ruta, Dictionary<string, object?> payload)
    {
        var response = await client.PostAsJsonAsync(ruta, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    private static Dictionary<string, object?> Venta(Escenario e, DateOnly fecha, params object[] lineas) =>
        FacturaPayloads.Cxc(e, $"F-{TestData.Sufijo()}", lineas).EnFecha(fecha);

    private static Dictionary<string, object?> Compra(Escenario e, DateOnly fecha, params object[] lineas) =>
        FacturaPayloads.Cxp(e, $"P-{TestData.Sufijo()}", lineas).ConDte().EnFecha(fecha);

    private static (decimal BaseBienes, decimal BaseServicios, decimal Exento, decimal Iva, decimal Total) Totales(JsonElement t) => (
        t.GetProperty("baseBienes").GetDecimal(), t.GetProperty("baseServicios").GetDecimal(),
        t.GetProperty("exento").GetDecimal(), t.GetProperty("iva").GetDecimal(), t.GetProperty("total").GetDecimal());

    [Fact]
    public async Task Ventas_ParticionaBaseIvaYExentoYRestaLasNotasDeCredito()
    {
        var (e, client, anio) = await PrepararAsync();
        var junio = new DateOnly(anio, 6, 10);
        await EnviarAsync(client, "/api/cxc/facturas", Venta(e, junio,
            FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, 2m, 100m),
            FacturaPayloads.Linea(e.ImpuestoExentoId, e.CuentaIngreso, 1m, 50.50m)));
        var bienes = await EnviarAsync(client, "/api/cxc/facturas", Venta(e, junio.AddDays(1),
            FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, 1m, 112m, tipoBienServicio: "BIEN")));
        await EnviarAsync(client, "/api/cxc/facturas", Venta(e, junio.AddDays(2),
            FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, 1m, 56m, tipoBienServicio: "BIEN"))
            .Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", bienes));

        var response = await client.GetAsync($"/api/libros/fiscal/ventas?anio={anio}&mes=6");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var libro = await response.LeerJsonAsync();
        Assert.Equal(("VENTAS", TestData.NitValido), (libro.GetProperty("tipo").GetString(), libro.GetProperty("contribuyente").GetProperty("nit").GetString()));
        Assert.Equal((50m, 178.57m, 50.50m, 27.43m, 306.50m), Totales(libro.GetProperty("totales")));
        var filas = libro.GetProperty("filas");
        Assert.Equal(3, filas.GetArrayLength());
        Assert.Equal(("CF", "NotaCredito", -56m), (filas[2].GetProperty("nit").GetString(), filas[2].GetProperty("tipoDocumento").GetString(), filas[2].GetProperty("total").GetDecimal()));
        Assert.Equal(JsonValueKind.String, filas[2].GetProperty("referenciaSerie").ValueKind);
        Assert.Equal(JsonValueKind.Array, libro.GetProperty("advertencias").ValueKind);
        Assert.Equal(0, libro.GetProperty("advertencias").GetArrayLength());
    }

    [Fact]
    public async Task Compras_ConCreditoSinCreditoYAnulada_AjustaLasColumnas()
    {
        var (e, client, anio) = await PrepararAsync();
        var marzo = new DateOnly(anio, 3, 5);
        await EnviarAsync(client, "/api/cxp/facturas", Compra(e, marzo, FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaGasto, 1m, 112m, tipoBienServicio: "BIEN")));
        await EnviarAsync(client, "/api/cxp/facturas", Compra(e, marzo.AddDays(1), FacturaPayloads.Linea(e.ImpuestoSinCreditoId, e.CuentaGasto, 1m, 112m)));
        var anulada = await EnviarAsync(client, "/api/cxp/facturas", Compra(e, marzo.AddDays(2), FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaGasto, 1m, 224m)));
        await _pg.Data.EjecutarAsync("UPDATE documentocxp SET estado = 'Anulado' WHERE id = $1", anulada);

        var libro = await client.GetFromJsonAsync<JsonElement>($"/api/libros/fiscal/compras?anio={anio}&mes=3");

        Assert.Equal((100m, 112m, 0m, 12m, 224m), Totales(libro.GetProperty("totales")));
        var anuladaFila = libro.GetProperty("filas")[2];
        Assert.Equal(("Anulado", 0m), (anuladaFila.GetProperty("estado").GetString(), anuladaFila.GetProperty("total").GetDecimal()));
    }

    [Fact]
    public async Task Libro_ConMesSinDocumentos_DevuelveFilasYTotalesEnCero()
    {
        var (_, client, anio) = await PrepararAsync();

        var libro = await client.GetFromJsonAsync<JsonElement>($"/api/libros/fiscal/ventas?anio={anio}&mes=12");

        Assert.Equal(0, libro.GetProperty("filas").GetArrayLength());
        Assert.Equal((0m, 0m, 0m, 0m, 0m), Totales(libro.GetProperty("totales")));
    }

    [Fact]
    public async Task Libro_ConDocumentoLegado_AgregaLaAdvertencia()
    {
        var (e, client, anio) = await PrepararAsync();
        var asiento = await _pg.Data.SembrarAsientoAsync(e.PeriodoId, e.UsuarioId, e.CuentaCxC, e.CuentaIngreso, 100m, new DateOnly(anio, 8, 1));
        await _pg.Data.VincularFacturaCxCAsync(e.ClienteId, asiento, new DateOnly(anio, 8, 1));

        var libro = await client.GetFromJsonAsync<JsonElement>($"/api/libros/fiscal/ventas?anio={anio}&mes=8");

        Assert.Equal(1, libro.GetProperty("filas").GetArrayLength());
        Assert.Contains("calculo legado", libro.GetProperty("advertencias")[0].GetString());
        Assert.True(libro.GetProperty("filas")[0].GetProperty("legado").GetBoolean());
    }

    [Theory]
    [InlineData("anio=2025&mes=13", "El mes debe estar entre 1 y 12.")]
    [InlineData("anio=2025&mes=0", "El mes debe estar entre 1 y 12.")]
    [InlineData("anio=1999&mes=5", "El año debe estar entre 2000 y 2100.")]
    [InlineData("anio=2101&mes=5", "El año debe estar entre 2000 y 2100.")]
    public async Task Libro_ConMesOAnioInvalido_Responde400(string consulta, string mensaje)
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var ventas = await client.GetAsync($"/api/libros/fiscal/ventas?{consulta}");
        var compras = await client.GetAsync($"/api/libros/fiscal/compras?{consulta}");

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (ventas.StatusCode, compras.StatusCode));
        Assert.Equal(mensaje, await ventas.LeerErrorAsync());
    }

    [Fact]
    public async Task Libro_ComoVendedorOAnonimo_Responde403Y401()
    {
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync("/api/libros/fiscal/ventas?anio=2025&mes=3")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _pg.Factory.CreateClient().GetAsync("/api/libros/fiscal/compras?anio=2025&mes=3")).StatusCode);
    }
}
