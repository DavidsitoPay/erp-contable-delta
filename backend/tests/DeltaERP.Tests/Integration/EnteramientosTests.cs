using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Rules;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.CompraNrExtensions;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class EnteramientosTests
{
    private readonly PostgresFixture _pg;

    public EnteramientosTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    // Los periodos son meses: otras pruebas comparten el mes, por eso se compara contra el saldo leido antes.
    private static async Task<decimal> SaldoAsync(Compra c) =>
        (await c.Client.GetFromJsonAsync<JsonElement>($"{RutaEnteramientos}/saldo?periodo={c.Periodo.Dia()}"))[0].GetProperty("saldo").GetDecimal();

    private static async Task AssertRechazoAsync(Compra c, object payload, HttpStatusCode esperado, string? fragmento)
    {
        var (estado, cuerpo) = await c.Client.EnviarAsync(RutaEnteramientos, payload);
        Assert.Equal(esperado, estado);
        if (fragmento is not null)
        {
            Assert.Contains(fragmento, cuerpo.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task Registrar_ParcialDelSaldo_CreaAsientoYMovimientoYBajaElSaldo()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var antes = await SaldoAsync(c);

        var (estado, cuerpo) = await c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(50m));

        Assert.Equal(HttpStatusCode.Created, estado);
        var asientoId = cuerpo.GetProperty("asientoId").GetInt32();
        var cuentaNr = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIsrNrRetenido);
        Assert.Equal(("VIGENTE", 50m, 50m, 50m, "Egreso"), (
            cuerpo.GetProperty("estado").GetString(), await _pg.Data.DebitoAsync(asientoId, cuentaNr),
            await _pg.Data.CreditoAsync(asientoId, c.M.BancoGtqContableId), antes - await SaldoAsync(c),
            await _pg.Data.ScalarAsync<string>("SELECT tipo FROM movimientotesoreria WHERE asiento_id = $1", asientoId)));
        Assert.True(await _pg.Data.AsientoCuadraAsync(asientoId));
        Assert.Contains("Enteramiento", await _pg.Data.DetalleAuditoriaAsync(c.M.E.UsuarioId, "registrar_enteramiento"));
    }

    [Fact]
    public async Task VistaPrevia_DevuelveLasDosLineasYLosSaldos()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var antes = await SaldoAsync(c);

        var (estado, cuerpo) = await c.Client.EnviarAsync(
            $"{RutaEnteramientos}/vista-previa", new { periodo = c.Periodo.Dia(), monto = 40m, cuentaBancariaId = c.M.BancoGtqId });

        Assert.Equal(HttpStatusCode.OK, estado);
        var lineas = cuerpo.GetProperty("lineas").EnumerateArray().ToArray();
        Assert.Equal((2, 40m, 40m, antes, antes - 40m), (
            lineas.Length, lineas.Sum(l => l.GetProperty("debe").GetDecimal()), lineas.Sum(l => l.GetProperty("haber").GetDecimal()),
            cuerpo.GetProperty("saldoAntes").GetDecimal(), cuerpo.GetProperty("saldoDespues").GetDecimal()));
    }

    [Fact]
    public async Task VistaPrevia_CuentaUsd_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();

        var (estado, cuerpo) = await c.Client.EnviarAsync(
            $"{RutaEnteramientos}/vista-previa", new { periodo = c.Periodo.Dia(), monto = 10m, cuentaBancariaId = c.M.BancoUsdId });

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("quetzales", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task VistaPrevia_MontoMayorAlSaldo_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();

        var (estado, cuerpo) = await c.Client.EnviarAsync(
            $"{RutaEnteramientos}/vista-previa", new { periodo = c.Periodo.Dia(), monto = await SaldoAsync(c) + 0.01m, cuentaBancariaId = c.M.BancoGtqId });

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("excede", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Registrar_MontoMayorAlSaldo_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();

        await AssertRechazoAsync(c, c.SolicitudEntero(await SaldoAsync(c) + 0.01m), HttpStatusCode.Conflict, "excede");
    }

    [Fact]
    public async Task Registrar_BoletaRepetida_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var primera = c.SolicitudEntero(10m);
        Assert.Equal(HttpStatusCode.Created, (await c.Client.EnviarAsync(RutaEnteramientos, primera)).Estado);

        await AssertRechazoAsync(c, c.SolicitudEntero(10m).Con("boleta", primera["boleta"]), HttpStatusCode.Conflict, "boleta");
    }

    [Fact]
    public async Task Registrar_CuentaBancariaUsdOInexistente_Rechaza()
    {
        var c = await _pg.CompraNrPagadaAsync();

        await AssertRechazoAsync(c, c.SolicitudEntero(10m).Con("cuentaBancariaId", c.M.BancoUsdId), HttpStatusCode.Conflict, "quetzales");
        await AssertRechazoAsync(c, c.SolicitudEntero(10m).Con("cuentaBancariaId", 0), HttpStatusCode.NotFound, "no existe");
    }

    [Fact]
    public async Task Registrar_PeriodoContableCerrado_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();
        await _pg.Data.CerrarPeriodoDirectoAsync(c.M.E.PeriodoId);

        await AssertRechazoAsync(c, c.SolicitudEntero(10m), HttpStatusCode.Conflict, null);
    }

    [Fact]
    public async Task Registrar_FechaPagoPosteriorAHoy_Responde400()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var manana = CambioRules.HoyGuatemala(DateTimeOffset.UtcNow).AddDays(1);

        await AssertRechazoAsync(c, c.SolicitudEntero(10m).Con("fechaPago", manana.Dia()), HttpStatusCode.BadRequest, "posterior a hoy");
    }

    [Fact]
    public async Task Registrar_SinCuentaIsrNrConfigurada_Responde409()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var cuentaNr = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIsrNrRetenido);
        await _pg.Data.ConfigurarCuentaIsrNrAsync(null);
        try
        {
            await AssertRechazoAsync(c, c.SolicitudEntero(10m), HttpStatusCode.Conflict, "Falta configurar");
        }
        finally
        {
            await _pg.Data.ConfigurarCuentaIsrNrAsync(cuentaNr);
        }
    }

    [Fact]
    public async Task SolicitudesInvalidas_Responden400()
    {
        var c = await _pg.CompraNrPagadaAsync();

        var (sinBoleta, _) = await c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(10m).Con("boleta", " "));
        var (montoCero, _) = await c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(0m));
        var (vistaSinMonto, _) = await c.Client.EnviarAsync(
            $"{RutaEnteramientos}/vista-previa", new { periodo = c.Periodo.Dia(), monto = 0m, cuentaBancariaId = c.M.BancoGtqId });
        var saldoMalPeriodo = await c.Client.GetAsync($"{RutaEnteramientos}/saldo?periodo={c.Periodo.AddDays(14).Dia()}");

        Assert.All(new[] { sinBoleta, montoCero, vistaSinMonto, saldoMalPeriodo.StatusCode }, e => Assert.Equal(HttpStatusCode.BadRequest, e));
    }

    [Fact]
    public async Task Registrar_ComoVendedor_Responde403()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var (estado, _) = await vendedor.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(10m));

        Assert.Equal(HttpStatusCode.Forbidden, estado);
    }

    [Fact]
    public async Task Registrar_DosEnterosSimultaneosPorTodoElSaldo_SoloUnoSeAcepta()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var saldo = await SaldoAsync(c);

        var respuestas = await Task.WhenAll(
            c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(saldo)), c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(saldo)));

        Assert.Equal(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            respuestas.Select(r => r.Estado).OrderBy(e => (int)e).ToArray());
        Assert.Equal(0m, await SaldoAsync(c));
    }

    [Fact]
    public async Task Anular_ConMotivo_CreaContraAsientoYReabreElSaldo()
    {
        await _pg.AsegurarPeriodoDeHoyAsync();
        var c = await _pg.CompraNrPagadaAsync();
        var saldoAntes = await SaldoAsync(c);
        var librosAntes = await _pg.Data.SaldoLibrosGtqAsync(c.M.BancoGtqContableId);
        var id = await c.EnterarAsync(50m);

        var (estado, cuerpo) = await c.Client.EnviarAsync($"{RutaEnteramientos}/{id}/anular", new { motivo = "Boleta duplicada" });

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(("ANULADO", "Boleta duplicada", saldoAntes, librosAntes), (
            cuerpo.GetProperty("estado").GetString(), cuerpo.GetProperty("motivoAnulacion").GetString(), await SaldoAsync(c),
            await _pg.Data.SaldoLibrosGtqAsync(c.M.BancoGtqContableId)));
        Assert.True(await _pg.Data.AsientoCuadraAsync(cuerpo.GetProperty("asientoReversaId").GetInt32()));
        var (otraVez, _) = await c.Client.EnviarAsync($"{RutaEnteramientos}/{id}/anular", new { motivo = "Otra vez" });
        Assert.Equal(HttpStatusCode.Conflict, otraVez);
        var anulados = await c.Client.GetFromJsonAsync<JsonElement>($"{RutaEnteramientos}?periodo={c.Periodo.Dia()}&estado=ANULADO");
        Assert.Contains(id, anulados.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Anular_SinMotivoOInexistente_Rechaza()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var id = await c.EnterarAsync(10m);

        var (sinMotivo, _) = await c.Client.EnviarAsync($"{RutaEnteramientos}/{id}/anular", new { motivo = " " });
        var (inexistente, _) = await c.Client.EnviarAsync($"{RutaEnteramientos}/0/anular", new { motivo = "x" });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.NotFound), (sinMotivo, inexistente));
    }

    [Fact]
    public async Task Anular_SinPeriodoAbiertoHoy_Responde409()
    {
        var periodoHoy = await _pg.AsegurarPeriodoDeHoyAsync();
        var c = await _pg.CompraNrPagadaAsync();
        var id = await c.EnterarAsync(10m);
        await _pg.Data.CerrarPeriodoDirectoAsync(periodoHoy);
        try
        {
            var (estado, _) = await c.Client.EnviarAsync($"{RutaEnteramientos}/{id}/anular", new { motivo = "Boleta duplicada" });

            Assert.Equal(HttpStatusCode.Conflict, estado);
        }
        finally
        {
            await _pg.Data.ReabrirPeriodoDirectoAsync(periodoHoy);
        }
    }

    [Fact]
    public async Task Anular_ComoVendedor_Responde403()
    {
        var c = await _pg.CompraNrPagadaAsync();
        var id = await c.EnterarAsync(10m);
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        var (estado, _) = await vendedor.EnviarAsync($"{RutaEnteramientos}/{id}/anular", new { motivo = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, estado);
    }

    [Fact]
    public async Task Saldo_ReportaVencimientoDelDecimoDiaHabilYCalendarioIncompleto()
    {
        // Supone saldo > 0 y ningún feriado cargado: el vencimiento se calcula sin calendario.
        var c = await _pg.CompraNrPagadaAsync();

        var fila = (await c.Client.GetFromJsonAsync<JsonElement>($"{RutaEnteramientos}/saldo?periodo={c.Periodo.Dia()}"))[0];

        var vence = DiaHabilRules.DecimoDiaHabil(c.Periodo, new HashSet<DateOnly>());
        Assert.Equal((vence.Dia(), "VENCIDO", true), (
            fila.GetProperty("fechaVencimiento").GetString(), fila.GetProperty("estadoVencimiento").GetString(),
            fila.GetProperty("calendarioIncompleto").GetBoolean()));
        Assert.NotEqual(JsonValueKind.Null, fila.GetProperty("avisoMulta").ValueKind);
    }
}
