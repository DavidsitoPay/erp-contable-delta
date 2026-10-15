using System.Net;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ReporteSaldosMonedaTests
{
    private const string Ruta = "/api/reportes/saldos-antiguedad";
    private const string RutaRevaluacion = "/api/revaluaciones";

    private readonly PostgresFixture _pg;

    public ReporteSaldosMonedaTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private sealed record Cartera(EscenarioRevaluacion R, HttpClient Cliente, int A, int B, int C);

    private static decimal Dec(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetDecimal();

    private static string Texto(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetString() ?? string.Empty;

    private static JsonElement FilaDe(JsonElement cuerpo, int documentoId) =>
        cuerpo.GetProperty("filas").EnumerateArray().Single(f => f.GetProperty("documentoId").GetInt32() == documentoId);

    private static async Task<(HttpStatusCode Estado, JsonElement Cuerpo)> ConsultarAsync(HttpClient cliente, string consulta)
    {
        var response = await cliente.GetAsync($"{Ruta}?{consulta}");
        return (response.StatusCode, await response.LeerJsonAsync());
    }

    private async Task<Cartera> SembrarCarteraAsync()
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync();
        var m = r.M;
        var anio = r.Anio;
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(anio, 8, 1), 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(anio, 9, 1), 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(anio, 10, 20), 7.60m);
        var cliente = _pg.CreateApiClient(m.E.UsuarioId);
        var a = await cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", new DateOnly(anio, 9, 1), 1000m).Con("periodoId", r.PeriodoPrevio));
        var b = await cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", new DateOnly(anio, 8, 1), 500m).Con("periodoId", r.PeriodoPrevio));
        var c = await cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", new DateOnly(anio, 10, 20), 1000m));
        return new Cartera(r, cliente, a, b, c);
    }

    private async Task<int> SembrarVentaEurAsync(Cartera cartera)
    {
        var m = cartera.R.M;
        await _pg.Data.SembrarTipoCambioAsync("EUR", new DateOnly(cartera.R.Anio, 10, 20), 8.556266m);
        return await cartera.Cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "EUR", new DateOnly(cartera.R.Anio, 10, 20), 100m));
    }

    [Fact]
    public async Task Reporte_ClasificaPorTramoYMuestraLasTasasDeLibros()
    {
        var c = await SembrarCarteraAsync();

        var (estado, cuerpo) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        var filas = cuerpo.GetProperty("filas").EnumerateArray().ToList();
        Assert.Equal(3, filas.Count);
        Assert.Equal((c.B, "61-90", 61, 500m), (filas[0].GetProperty("documentoId").GetInt32(), Texto(filas[0], "tramo"), filas[0].GetProperty("diasVencido").GetInt32(), Dec(filas[0], "saldoOriginal")));
        Assert.Equal((c.A, "1-30", 30), (filas[1].GetProperty("documentoId").GetInt32(), Texto(filas[1], "tramo"), filas[1].GetProperty("diasVencido").GetInt32()));
        Assert.Equal((c.C, "NO_VENCIDO", 0), (filas[2].GetProperty("documentoId").GetInt32(), Texto(filas[2], "tramo"), filas[2].GetProperty("diasVencido").GetInt32()));
        Assert.All(filas, f => Assert.Equal(7.6m, Dec(f, "tipoCambioLibros")));
        Assert.Equal(3800m, Dec(filas[0], "saldoGtq"));
    }

    [Fact]
    public async Task Reporte_CortaLosPagosPosterioresYLosIncluyeDespues()
    {
        var c = await SembrarCarteraAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(c.R.Anio, 11, 5), 7.70m);
        var cobro = await c.Cliente.EnviarAsync(
            RutaPagosCxC, Cobro(c.R.M.E.ClienteId, c.R.M.BancoUsdId, new DateOnly(c.R.Anio, 11, 5).Dia(), AplicacionMe(c.A, 400m)));
        Assert.Equal(HttpStatusCode.Created, cobro.Estado);

        var (_, alCorte) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-10-31");
        var (_, trasCobro) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-11-30");

        Assert.Equal(1000m, Dec(FilaDe(alCorte, c.A), "saldoOriginal"));
        Assert.Equal(600m, Dec(FilaDe(trasCobro, c.A), "saldoOriginal"));
    }

    [Fact]
    public async Task Reporte_TotalesPorMonedaNuncaMezclaImportesOriginales()
    {
        var c = await SembrarCarteraAsync();
        await SembrarVentaEurAsync(c);

        var (estado, cuerpo) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        var totales = cuerpo.GetProperty("totales").EnumerateArray().ToList();
        Assert.Equal(2, totales.Count);
        Assert.Equal(("EUR", 100m), (Texto(totales[0], "moneda"), Dec(totales[0], "saldoOriginal")));
        Assert.Equal(("USD", 2500m), (Texto(totales[1], "moneda"), Dec(totales[1], "saldoOriginal")));
        Assert.Equal(totales.Sum(t => Dec(t, "saldoGtq")), Dec(cuerpo, "totalGtq"));
        Assert.All(totales, t => Assert.Equal(5, t.GetProperty("porTramo").EnumerateObject().Count()));
    }

    [Fact]
    public async Task Reporte_ConFiltroDeMoneda_NoCalculaConciliacion()
    {
        var c = await SembrarCarteraAsync();
        await SembrarVentaEurAsync(c);

        var (estado, cuerpo) = await ConsultarAsync(c.Cliente, $"tipo=CxC&moneda=USD&corte={c.R.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.All(cuerpo.GetProperty("filas").EnumerateArray(), f => Assert.Equal("USD", Texto(f, "moneda")));
        Assert.Empty(cuerpo.GetProperty("conciliacion").EnumerateArray());
    }

    [Fact]
    public async Task Reporte_SinFiltroDeMoneda_ConciliaConElMayorYLaRevaluacionVigente()
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync();
        var anio = r.Anio;
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(anio, 10, 5), 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(anio, 10, 31), 7.65m);
        var cliente = _pg.CreateApiClient(r.M.E.UsuarioId);
        await cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(r.M, "USD", new DateOnly(anio, 10, 5), 1000m));
        var (estadoRevaluacion, _) = await cliente.EnviarAsync(RutaRevaluacion, new { anio, mes = 10, monedaCodigo = "USD" });
        Assert.Equal(HttpStatusCode.Created, estadoRevaluacion);

        var (estado, cuerpo) = await ConsultarAsync(cliente, $"tipo=CxC&corte={anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        var conciliacion = Assert.Single(cuerpo.GetProperty("conciliacion").EnumerateArray());
        Assert.Equal((7600m, 7650m, 50m, 0m), (Dec(conciliacion, "totalReporte"), Dec(conciliacion, "saldoMayor"), Dec(conciliacion, "revaluacionVigente"), Dec(conciliacion, "diferencia")));
    }

    [Fact]
    public async Task Reporte_CxP_UsaElMayorInvertido()
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync();
        var m = r.M;
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 6), 7.66m);
        var cliente = _pg.CreateApiClient(m.E.UsuarioId);
        await cliente.RegistrarCompraAsync(FacturaPayloads.CxpEn(m, "USD", new DateOnly(r.Anio, 10, 6), 500m, proveedor));

        var (estado, cuerpo) = await ConsultarAsync(cliente, $"tipo=CxP&corte={r.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        var conciliacion = Assert.Single(cuerpo.GetProperty("conciliacion").EnumerateArray());
        Assert.Equal((3830m, 0m), (Dec(conciliacion, "totalReporte"), Dec(conciliacion, "diferencia")));
    }

    [Fact]
    public async Task Reporte_DocumentoCerradoEnCero_NoAparece()
    {
        var c = await SembrarCarteraAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(c.R.Anio, 10, 15), 7.60m);
        var cobro = await c.Cliente.EnviarAsync(
            RutaPagosCxC, Cobro(c.R.M.E.ClienteId, c.R.M.BancoUsdId, new DateOnly(c.R.Anio, 10, 15).Dia(), AplicacionMe(c.B, 500m)));
        Assert.Equal(HttpStatusCode.Created, cobro.Estado);

        var (estado, cuerpo) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.DoesNotContain(cuerpo.GetProperty("filas").EnumerateArray(), f => f.GetProperty("documentoId").GetInt32() == c.B);
    }

    [Fact]
    public async Task Reporte_NotaDeCredito_ReduceElSaldoOriginal()
    {
        var c = await SembrarCarteraAsync();
        var m = c.R.M;
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(c.R.Anio, 10, 10), 7.60m);
        var notaCredito = FacturaPayloads.Cxc(m.E, $"NC-{TestData.Sufijo()}", new[] { FacturaPayloads.Linea(m.E.ImpuestoExentoId, m.E.CuentaIngreso, precio: 200m) })
            .EnFecha(new DateOnly(c.R.Anio, 10, 10))
            .EnMoneda("USD")
            .Con("tipoDocumento", "NotaCredito")
            .Con("documentoOrigenId", c.A);
        await c.Cliente.RegistrarVentaAsync(notaCredito);

        var (estado, cuerpo) = await ConsultarAsync(c.Cliente, $"tipo=CxC&corte={c.R.Anio}-10-31");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(800m, Dec(FilaDe(cuerpo, c.A), "saldoOriginal"));
    }

    [Fact]
    public async Task Reporte_ParametrosInvalidos_Responde400()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var cliente = _pg.CreateApiClient(usuario);

        Assert.Equal(HttpStatusCode.BadRequest, (await ConsultarAsync(cliente, "tipo=XX&corte=2010-10-31")).Estado);
        Assert.Equal(HttpStatusCode.BadRequest, (await ConsultarAsync(cliente, "tipo=CxC")).Estado);
        Assert.Equal(HttpStatusCode.BadRequest, (await ConsultarAsync(cliente, "tipo=CxC&moneda=XXX&corte=2010-10-31")).Estado);
    }

    [Fact]
    public async Task Reporte_ComoVendedor_Responde403()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var vendedor = _pg.CreateApiClient(usuario, Roles.Vendedor);

        var response = await vendedor.GetAsync($"{Ruta}?tipo=CxC&corte=2010-10-31");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
