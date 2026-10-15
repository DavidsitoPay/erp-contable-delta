using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Domain.Rules;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class RevaluacionesControllerTests
{
    private const string Ruta = "/api/revaluaciones";
    private const string RutaCxp = "/api/cxp/facturas";
    private const string RutaCuentas = "/api/cuentas-bancarias";

    private readonly PostgresFixture _pg;

    public RevaluacionesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private sealed record Caso(EscenarioRevaluacion R, HttpClient Cliente, int CxcId, int BancoId, int BancoContableId);

    private static decimal Dec(JsonElement elemento, string propiedad) => elemento.GetProperty(propiedad).GetDecimal();

    private static string MensajeDe(JsonElement cuerpo) => cuerpo.GetProperty("error").GetString() ?? string.Empty;

    private static object Solicitud(int anio, int mes, string? moneda = "USD") => new { anio, mes, monedaCodigo = moneda };

    private static object PayloadBanco(int cuentaContableId, int capitalId, DateOnly fecha) => new
    {
        banco = "Banco Revaluacion",
        numero = $"N{TestData.Sufijo()}",
        tipo = "Monetaria",
        cuentaContableId,
        saldoApertura = 2000m,
        fechaApertura = fecha,
        cuentaContrapartidaId = capitalId,
        monedaCodigo = "USD",
    };

    private static async Task<(HttpStatusCode Estado, JsonElement Cuerpo)> LeerAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> EjecutarRevaluacionAsync(HttpClient cliente, EscenarioRevaluacion r) =>
        cliente.EnviarAsync(Ruta, Solicitud(r.Anio, 10));

    private async Task<Caso> CasoEjemploAsync(bool conPeriodoNoviembre = true)
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync(conPeriodoNoviembre);
        var m = r.M;
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 2), 7.70m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 5), 7.60m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 6), 7.66m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 31), 7.65m);
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 11, 5), 7.70m);

        var cliente = _pg.CreateApiClient(m.E.UsuarioId);
        var cuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var (estadoBanco, banco) = await cliente.EnviarAsync(RutaCuentas, PayloadBanco(cuentaContable, capital, new DateOnly(r.Anio, 10, 2)));
        Assert.Equal(HttpStatusCode.Created, estadoBanco);

        var cxc = await cliente.RegistrarVentaAsync(FacturaPayloads.CxcEn(m, "USD", new DateOnly(r.Anio, 10, 5), 1000m));
        var (estadoCxp, _) = await cliente.EnviarAsync(RutaCxp, FacturaPayloads.CxpEn(m, "USD", new DateOnly(r.Anio, 10, 6), 500m));
        Assert.Equal(HttpStatusCode.Created, estadoCxp);

        return new Caso(r, cliente, cxc, banco.GetProperty("id").GetInt32(), banco.GetProperty("cuentaContableId").GetInt32());
    }

    [Fact]
    public async Task VistaPrevia_DevuelveLasPartidasSinCrearNada()
    {
        var a = await CasoEjemploAsync();

        var (estado, cuerpo) = await a.Cliente.EnviarAsync($"{Ruta}/vista-previa", Solicitud(a.R.Anio, 10));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(55m, Dec(cuerpo, "totalGanancia"));
        Assert.Equal(100m, Dec(cuerpo, "totalPerdida"));
        Assert.Equal(7.65m, Dec(cuerpo.GetProperty("tipoCambio"), "tasa"));
        Assert.Equal(RevaluacionRules.Leyenda, cuerpo.GetProperty("leyenda").GetString());
        var partidas = cuerpo.GetProperty("partidas").EnumerateArray()
            .Select(p => (Tipo: p.GetProperty("tipo").GetString(), Ajuste: Dec(p, "ajusteGtq"), Efecto: p.GetProperty("efecto").GetString()))
            .ToList();
        Assert.Equal(
            new List<(string?, decimal, string?)> { ("CxC", 50m, "Ganancia"), ("CxP", -5m, "Ganancia"), ("Banco", -100m, "Perdida") },
            partidas);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM revaluacion WHERE anio = $1", a.R.Anio));
    }

    [Fact]
    public async Task Ejecutar_GeneraElAsientoDeCierreYLaReversaDelDia1()
    {
        var a = await CasoEjemploAsync();
        var m = a.R.M;

        var (estado, cuerpo) = await EjecutarRevaluacionAsync(a.Cliente, a.R);

        Assert.Equal(HttpStatusCode.Created, estado);
        var cierreId = cuerpo.GetProperty("asientoId").GetInt32();
        var reversaId = cuerpo.GetProperty("asientoReversaId").GetInt32();
        Assert.Equal(50m, await _pg.Data.DebitoAsync(cierreId, m.E.CuentaCxC));
        Assert.Equal(5m, await _pg.Data.DebitoAsync(cierreId, m.E.CuentaCxP));
        Assert.Equal(100m, await _pg.Data.CreditoAsync(cierreId, a.BancoContableId));
        Assert.Equal(55m, await _pg.Data.CreditoAsync(cierreId, m.CuentaRevalGanancia));
        Assert.Equal(100m, await _pg.Data.DebitoAsync(cierreId, m.CuentaRevalPerdida));
        Assert.Equal(new DateOnly(a.R.Anio, 10, 31), await _pg.Data.ScalarAsync<DateOnly>("SELECT fecha FROM asientocontable WHERE id = $1", cierreId));
        Assert.Equal(new DateOnly(a.R.Anio, 11, 1), await _pg.Data.ScalarAsync<DateOnly>("SELECT fecha FROM asientocontable WHERE id = $1", reversaId));
        Assert.True(await _pg.Data.AsientoCuadraAsync(cierreId));
        Assert.True(await _pg.Data.AsientoCuadraAsync(reversaId));

        var lineasCierre = await _pg.Data.LineasDeAsientoAsync(cierreId);
        var lineasReversa = await _pg.Data.LineasDeAsientoAsync(reversaId);
        Assert.Equal(
            lineasCierre.Select(l => (l.Cuenta, Debito: l.Credito, Credito: l.Debito)).OrderBy(l => l.Cuenta).ThenBy(l => l.Debito).ToList(),
            lineasReversa.OrderBy(l => l.Cuenta).ThenBy(l => l.Debito).ToList());
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM asientocontable WHERE id = ANY($1) AND reversa_de_id IS NULL", new[] { cierreId, reversaId }));

        Assert.Equal("Vigente", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(3, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM revaluacionpartida WHERE revaluacion_id = $1", cuerpo.GetProperty("id").GetInt32()));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(m.E.UsuarioId, "ejecutar_revaluacion"));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM movimientotesoreria WHERE asiento_id = $1 OR asiento_id = $2", cierreId, reversaId));
        var movimientosBanco = await a.Cliente.GetFromJsonAsync<JsonElement>($"/api/movimientos-tesoreria?cuentaBancariaId={a.BancoId}");
        Assert.Single(movimientosBanco.EnumerateArray());
    }

    [Fact]
    public async Task Ejecutar_Repetida_Responde409()
    {
        var a = await CasoEjemploAsync();
        await EjecutarRevaluacionAsync(a.Cliente, a.R);

        var (estado, cuerpo) = await EjecutarRevaluacionAsync(a.Cliente, a.R);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains($"Ya existe una revaluación vigente de USD para 10/{a.R.Anio}", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task Ejecutar_MesNoTerminado_Responde409()
    {
        var futuro = DateTime.UtcNow.AddDays(35);
        var cliente = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var (estado, cuerpo) = await cliente.EnviarAsync(Ruta, Solicitud(futuro.Year, futuro.Month));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("aún no termina", MensajeDe(cuerpo));
    }

    [Theory]
    [InlineData(2012, 13, "USD")]
    [InlineData(0, 10, "USD")]
    [InlineData(2012, 10, "GTQ")]
    [InlineData(2012, 10, null)]
    [InlineData(2012, 10, "XXX")]
    public async Task Ejecutar_RangoYMonedaInvalidos_Responde400(int anio, int mes, string? moneda)
    {
        var cliente = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());

        var (estado, _) = await cliente.EnviarAsync(Ruta, Solicitud(anio, mes, moneda));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task Ejecutar_SinTasaDeCierre_Responde409()
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync();
        _pg.Banguat.Falla = true;

        var (estado, cuerpo) = await EjecutarRevaluacionAsync(_pg.CreateApiClient(r.M.E.UsuarioId), r);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("capturar la tasa manual", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task Ejecutar_PeriodoDeCierreOReversaCerradoOInexistente_Responde409()
    {
        var a = await CasoEjemploAsync();
        await _pg.Data.CerrarPeriodoDirectoAsync(a.R.PeriodoOctubre);
        var (estadoCierre, cuerpoCierre) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        Assert.Equal(HttpStatusCode.Conflict, estadoCierre);
        Assert.Contains("está en estado 'Cerrado'", MensajeDe(cuerpoCierre));

        var b = await CasoEjemploAsync();
        await _pg.Data.CerrarPeriodoDirectoAsync(b.R.PeriodoNoviembre);
        var (estadoReversa, cuerpoReversa) = await EjecutarRevaluacionAsync(b.Cliente, b.R);
        Assert.Equal(HttpStatusCode.Conflict, estadoReversa);
        Assert.Contains("está en estado 'Cerrado'", MensajeDe(cuerpoReversa));

        var c = await CasoEjemploAsync(conPeriodoNoviembre: false);
        var (estadoInexistente, cuerpoInexistente) = await EjecutarRevaluacionAsync(c.Cliente, c.R);
        Assert.Equal(HttpStatusCode.Conflict, estadoInexistente);
        Assert.Contains("No existe un periodo contable que contenga la fecha", MensajeDe(cuerpoInexistente));
    }

    [Fact]
    public async Task Ejecutar_SinPartidasConDiferencia_Responde409()
    {
        var r = await _pg.Data.SembrarEscenarioRevaluacionAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", new DateOnly(r.Anio, 10, 31), 7.65m);

        var (estado, cuerpo) = await EjecutarRevaluacionAsync(_pg.CreateApiClient(r.M.E.UsuarioId), r);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Equal($"No hay saldos en USD con diferencia al {r.Anio}-10-31.", MensajeDe(cuerpo));
    }

    [Fact]
    public async Task Ejecutar_SinCuentasDeRevaluacion_Responde409()
    {
        var a = await CasoEjemploAsync();
        await _pg.Data.ConfigurarCuentasCambiariasAsync(a.R.M.CuentaDifGanancia, a.R.M.CuentaDifPerdida, null, null);
        var (estadoGanancia, cuerpoGanancia) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        Assert.Equal(HttpStatusCode.Conflict, estadoGanancia);
        Assert.Contains("ganancia por revaluación", MensajeDe(cuerpoGanancia));

        var b = await CasoEjemploAsync();
        await _pg.Data.ConfigurarCuentasCambiariasAsync(b.R.M.CuentaDifGanancia, b.R.M.CuentaDifPerdida, b.R.M.CuentaRevalGanancia, null);
        var (estadoPerdida, cuerpoPerdida) = await EjecutarRevaluacionAsync(b.Cliente, b.R);
        Assert.Equal(HttpStatusCode.Conflict, estadoPerdida);
        Assert.Contains("pérdida por revaluación", MensajeDe(cuerpoPerdida));
    }

    [Fact]
    public async Task Anular_NetaLosCuatroAsientosYPermiteRepetir()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var id = ejecutada.GetProperty("id").GetInt32();
        var cierreId = ejecutada.GetProperty("asientoId").GetInt32();
        var reversaId = ejecutada.GetProperty("asientoReversaId").GetInt32();

        var (estadoAnula, anulada) = await a.Cliente.EnviarAsync($"{Ruta}/{id}/anular", new { motivo = "Corrección" });

        Assert.Equal(HttpStatusCode.OK, estadoAnula);
        Assert.Equal("Anulada", anulada.GetProperty("estado").GetString());
        Assert.Equal("Anulado", await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", cierreId));
        Assert.Equal("Anulado", await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", reversaId));

        var anulacionCierre = await _pg.Data.ScalarAsync<int>("SELECT asiento_anulacion_id FROM revaluacion WHERE id = $1", id);
        var anulacionReversa = await _pg.Data.ScalarAsync<int>("SELECT asiento_anulacion_reversa_id FROM revaluacion WHERE id = $1", id);
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM asientocontable WHERE id = $1 AND reversa_de_id = $2", anulacionCierre, cierreId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM asientocontable WHERE id = $1 AND reversa_de_id = $2", anulacionReversa, reversaId));

        var todas = new List<(int Cuenta, decimal Debito, decimal Credito)>();
        foreach (var asientoId in new[] { cierreId, reversaId, anulacionCierre, anulacionReversa })
        {
            todas.AddRange(await _pg.Data.LineasDeAsientoAsync(asientoId));
        }
        Assert.All(todas.GroupBy(l => l.Cuenta), g => Assert.Equal(0m, g.Sum(l => l.Debito - l.Credito)));

        var (estadoRepetida, _) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        Assert.Equal(HttpStatusCode.Created, estadoRepetida);

        var (estadoYaAnulada, cuerpoYaAnulada) = await a.Cliente.EnviarAsync($"{Ruta}/{id}/anular", new { motivo = "Otra vez" });
        Assert.Equal(HttpStatusCode.Conflict, estadoYaAnulada);
        Assert.Contains("ya está anulada", MensajeDe(cuerpoYaAnulada));

        var (estadoMotivo, _) = await a.Cliente.EnviarAsync($"{Ruta}/{id}/anular", new { motivo = " " });
        Assert.Equal(HttpStatusCode.BadRequest, estadoMotivo);

        var (estadoInexistente, _) = await a.Cliente.EnviarAsync($"{Ruta}/2000000000/anular", new { motivo = "Corrección" });
        Assert.Equal(HttpStatusCode.NotFound, estadoInexistente);

        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(a.R.M.E.UsuarioId, "anular_revaluacion"));
    }

    [Fact]
    public async Task ReversaManual_DeUnAsientoDeRevaluacion_Responde409()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var admin = _pg.CreateApiClient(a.R.M.E.UsuarioId, Roles.Administrador);

        foreach (var asientoId in new[] { ejecutada.GetProperty("asientoId").GetInt32(), ejecutada.GetProperty("asientoReversaId").GetInt32() })
        {
            var (estado, cuerpo) = await admin.EnviarAsync($"/api/asientos/{asientoId}/reversar", new { motivo = "Prueba" });

            Assert.Equal(HttpStatusCode.Conflict, estado);
            Assert.Contains("Pertenece a una revaluación", MensajeDe(cuerpo));
        }
    }

    [Fact]
    public async Task LibroDiario_MarcaLosAsientosDeRevaluacionComoNoReversibles()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var cierreId = ejecutada.GetProperty("asientoId").GetInt32();

        var (estado, diario) = await LeerAsync(a.Cliente, $"/api/libros/diario?periodoId={a.R.PeriodoOctubre}");

        Assert.Equal(HttpStatusCode.OK, estado);
        var cierre = diario.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == cierreId);
        Assert.True(cierre.GetProperty("esRevaluacion").GetBoolean());
        Assert.False(cierre.GetProperty("reversible").GetBoolean());
        Assert.Contains(diario.EnumerateArray(), x => !x.GetProperty("esRevaluacion").GetBoolean());
    }

    [Fact]
    public async Task CobroPosterior_NoAlteraLaRevaluacion()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var cierreId = ejecutada.GetProperty("asientoId").GetInt32();
        var reversaId = ejecutada.GetProperty("asientoReversaId").GetInt32();
        var antes = (await _pg.Data.LineasDeAsientoAsync(cierreId)).Concat(await _pg.Data.LineasDeAsientoAsync(reversaId)).ToList();

        var (estado, cobro) = await a.Cliente.EnviarAsync(
            RutaPagosCxC, Cobro(a.R.M.E.ClienteId, a.BancoId, new DateOnly(a.R.Anio, 11, 5).Dia(), AplicacionMe(a.CxcId, 1000m)));

        Assert.Equal(HttpStatusCode.Created, estado);
        Assert.Equal(100m, Dec(cobro.GetProperty("diferencialRealizado"), "ganancia"));
        var despues = (await _pg.Data.LineasDeAsientoAsync(cierreId)).Concat(await _pg.Data.LineasDeAsientoAsync(reversaId)).ToList();
        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task Listar_Obtener_FiltranYDevuelven404()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var id = ejecutada.GetProperty("id").GetInt32();

        var (estadoLista, lista) = await LeerAsync(a.Cliente, $"{Ruta}?anio={a.R.Anio}&mes=10&monedaCodigo=USD&estado=Vigente");
        Assert.Equal(HttpStatusCode.OK, estadoLista);
        Assert.Contains(lista.EnumerateArray(), f => f.GetProperty("id").GetInt32() == id);

        var (estadoOtra, _) = await LeerAsync(a.Cliente, $"{Ruta}?estado=Otra");
        Assert.Equal(HttpStatusCode.BadRequest, estadoOtra);
        var (estadoMes, _) = await LeerAsync(a.Cliente, $"{Ruta}?mes=13");
        Assert.Equal(HttpStatusCode.BadRequest, estadoMes);

        var (estadoDetalle, detalle) = await LeerAsync(a.Cliente, $"{Ruta}/{id}");
        Assert.Equal(HttpStatusCode.OK, estadoDetalle);
        var referencias = detalle.GetProperty("partidas").EnumerateArray().Select(p => p.GetProperty("referencia").GetString() ?? string.Empty).ToList();
        Assert.Contains(referencias, r => r.StartsWith("F-"));
        Assert.Contains(referencias, r => r.StartsWith("P-"));
        Assert.Contains(referencias, r => r.StartsWith("Banco Revaluacion"));

        var respuesta404 = await a.Cliente.GetAsync($"{Ruta}/2000000000");
        Assert.Equal(HttpStatusCode.NotFound, respuesta404.StatusCode);
    }

    [Fact]
    public async Task Permisos_VendedorRecibe403EnTodosLosEndpoints()
    {
        var a = await CasoEjemploAsync();
        var vendedor = _pg.CreateApiClient(a.R.M.E.UsuarioId, Roles.Vendedor);

        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsJsonAsync($"{Ruta}/vista-previa", Solicitud(a.R.Anio, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsJsonAsync(Ruta, Solicitud(a.R.Anio, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync($"{Ruta}/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.PostAsJsonAsync($"{Ruta}/1/anular", new { motivo = "Corrección" })).StatusCode);
    }

    [Fact]
    public async Task Anular_ConPeriodoCerrado_Responde409()
    {
        var a = await CasoEjemploAsync();
        var (_, ejecutada) = await EjecutarRevaluacionAsync(a.Cliente, a.R);
        var id = ejecutada.GetProperty("id").GetInt32();
        await _pg.Data.CerrarPeriodoDirectoAsync(a.R.PeriodoOctubre);

        var (estado, cuerpo) = await a.Cliente.EnviarAsync($"{Ruta}/{id}/anular", new { motivo = "Corrección" });

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("está en estado 'Cerrado'", MensajeDe(cuerpo));
        var (_, vigente) = await LeerAsync(a.Cliente, $"{Ruta}/{id}");
        Assert.Equal("Vigente", vigente.GetProperty("estado").GetString());
    }
}
