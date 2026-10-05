using System.Net;
using System.Net.Http.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ConciliacionesControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private const string Ruta = "/api/conciliaciones";
    private const string MensajeNoPendiente = "La conciliación no está pendiente; no admite cambios.";

    private static readonly (string Perfil, string Rol)[] PerfilesAutorizados =
    {
        ("Contador", Roles.Contador),
        ("Administrador del sistema", Roles.Administrador),
    };

    private readonly PostgresFixture _pg;

    public ConciliacionesControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private sealed record Apertura(HttpClient Client, int Usuario, int Periodo, DateOnly Inicio, int CuentaId, int MovimientoAperturaId, int ContableBanco, int Contrapartida);

    private static object PayloadConciliacion(EscenarioTesoreria e, decimal saldoExtracto = 0m, DateOnly? fecha = null) => new
    {
        cuentaBancariaId = e.CuentaBancaria,
        fecha = fecha ?? e.Inicio.AddDays(30),
        saldoExtracto,
    };

    private async Task<(EscenarioTesoreria E, HttpClient Client)> PrepararAsync()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        return (e, _pg.CreateApiClient(e.Usuario));
    }

    private Task<int> NuevaConciliacionAsync(EscenarioTesoreria e, decimal extracto = 0m, string estado = "Pendiente") =>
        _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(30), extracto, estado);

    private Task<int> NuevoMovimientoAsync(EscenarioTesoreria e, string tipo, decimal monto, int dia = 5) =>
        _pg.Data.CrearMovimientoAsync(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida, tipo, monto, e.Inicio.AddDays(dia), e.Periodo, e.Usuario);

    private static Task<HttpResponseMessage> MarcarAsync(HttpClient client, int conciliacionId, int movimientoId) =>
        client.PostAsJsonAsync($"{Ruta}/{conciliacionId}/movimientos", new { movimientoId });

    private Task<string> EstadoAsync(int conciliacionId) =>
        _pg.Data.ScalarAsync<string>("SELECT estado FROM conciliacionbancaria WHERE id = $1", conciliacionId);

    private async Task<Apertura> CrearConAperturaAsync(decimal saldo = 1000m)
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        var periodo = await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin);
        var contableBanco = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var capital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var contrapartida = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var client = _pg.CreateApiClient(usuario);
        var response = await client.PostAsJsonAsync("/api/cuentas-bancarias", new
        {
            banco = "Banco de Prueba",
            numero = $"N{TestData.Sufijo()}",
            tipo = "Monetaria",
            cuentaContableId = contableBanco,
            saldoApertura = saldo,
            fechaApertura = inicio,
            cuentaContrapartidaId = capital,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuentaId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        var aperturaId = await _pg.Data.ScalarAsync<int>("SELECT id FROM movimientotesoreria WHERE cuenta_bancaria_id = $1 AND origen = 'Apertura'", cuentaId);
        return new Apertura(client, usuario, periodo, inicio, cuentaId, aperturaId, contableBanco, contrapartida);
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201PendienteConSaldoInicialCero()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e, 100.25m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Pendiente", body.GetProperty("estado").GetString());
        Assert.Equal(e.Periodo, body.GetProperty("periodoId").GetInt32());
        Assert.Equal(0m, body.GetProperty("saldoInicial").GetDecimal());
        Assert.Equal(100.25m, body.GetProperty("saldoExtracto").GetDecimal());
        Assert.Equal(0, body.GetProperty("cantidadMovimientos").GetInt32());
    }

    [Fact]
    public async Task Crear_SinCuentaBancariaId_Responde400ConErrores()
    {
        var (_, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, new { fecha = new DateOnly(2025, 4, 30), saldoExtracto = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Crear_ConCuentaInactiva_Responde400()
    {
        var (e, client) = await PrepararAsync();
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET activa = false WHERE id = $1", e.CuentaBancaria);

        var response = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("está inactiva", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_SinPeriodoParaLaFecha_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e, fecha: TestData.FechaSinPeriodo));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("No existe un periodo contable que contenga la fecha 1990-06-15.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConExtractoConTresDecimalesOExcesivo_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var decimales = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e, 1.005m));
        var excesivo = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e, 1000000000000m));

        Assert.Equal("El saldo del extracto no puede tener más de 2 decimales.", await decimales.LeerErrorAsync());
        Assert.Equal("El saldo del extracto excede el máximo permitido.", await excesivo.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, decimales.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, excesivo.StatusCode);
    }

    [Fact]
    public async Task Crear_ConOtraPendienteEnLaCuenta_Responde409()
    {
        var (e, client) = await PrepararAsync();
        await NuevaConciliacionAsync(e);

        var response = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Ya existe una conciliación pendiente para esta cuenta bancaria.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_TrasCancelarLaPendiente_Responde201()
    {
        var (e, client) = await PrepararAsync();
        var primera = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e));
        var primeraId = (await primera.LeerJsonAsync()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Ruta}/{primeraId}/cancelar", null)).StatusCode);

        var segunda = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e));

        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
    }

    [Fact]
    public async Task Crear_PrimeraConciliacion_TomaComoSaldoInicialElSaldoDeApertura()
    {
        var a = await CrearConAperturaAsync(1000m);

        var response = await a.Client.PostAsJsonAsync(Ruta, new { cuentaBancariaId = a.CuentaId, fecha = a.Inicio.AddDays(30), saldoExtracto = 1000m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(1000m, body.GetProperty("saldoInicial").GetDecimal());
        Assert.Equal(0m, body.GetProperty("totalMarcado").GetDecimal());
        Assert.Equal(0m, body.GetProperty("diferencia").GetDecimal());
    }

    [Fact]
    public async Task Obtener_NoOfreceElMovimientoDeAperturaComoDisponible()
    {
        var a = await CrearConAperturaAsync();
        var manualId = await _pg.Data.CrearMovimientoAsync(a.CuentaId, a.ContableBanco, a.Contrapartida, "Ingreso", 50m, a.Inicio.AddDays(5), a.Periodo, a.Usuario);
        var conciliacionId = await _pg.Data.CrearConciliacionAsync(a.CuentaId, a.Periodo, a.Inicio.AddDays(30), 0m);

        var body = await (await a.Client.GetAsync($"{Ruta}/{conciliacionId}")).LeerJsonAsync();

        var disponibles = body.GetProperty("disponibles").Ids();
        Assert.Contains(manualId, disponibles);
        Assert.DoesNotContain(a.MovimientoAperturaId, disponibles);
    }

    [Fact]
    public async Task Marcar_ElMovimientoDeApertura_Responde400()
    {
        var a = await CrearConAperturaAsync();
        var conciliacionId = await _pg.Data.CrearConciliacionAsync(a.CuentaId, a.Periodo, a.Inicio.AddDays(30), 0m);

        var response = await MarcarAsync(a.Client, conciliacionId, a.MovimientoAperturaId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El movimiento de apertura no es conciliable: ya forma parte del saldo inicial.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Editar_FechaYExtracto_Responde200ActualizaPeriodoYAudita()
    {
        var (e, client) = await PrepararAsync();
        var (inicio2, fin2) = TestData.RangoPeriodoUnico();
        var periodo2 = await _pg.Data.CrearPeriodoEnRangoAsync(inicio2, fin2);
        var conciliacionId = await NuevaConciliacionAsync(e, 10m);
        var nuevaFecha = inicio2.AddDays(3);

        var response = await client.PutAsJsonAsync($"{Ruta}/{conciliacionId}", new { fecha = nuevaFecha, saldoExtracto = 777.25m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(periodo2, body.GetProperty("periodoId").GetInt32());
        Assert.Equal(nuevaFecha.ToString("yyyy-MM-dd"), body.GetProperty("fecha").GetString());
        Assert.Equal(777.25m, body.GetProperty("saldoExtracto").GetDecimal());
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.Usuario, "editar_conciliacion"));
    }

    [Fact]
    public async Task Editar_ConMovimientoMarcadoPosteriorALaNuevaFecha_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m, dia: 20);
        await _pg.Data.MarcarMovimientoAsync(conciliacionId, movimientoId);

        var response = await client.PutAsJsonAsync($"{Ruta}/{conciliacionId}", new { fecha = e.Inicio.AddDays(10), saldoExtracto = 0m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Hay movimientos marcados posteriores a la nueva fecha de corte; desmárcalos primero.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Editar_SinPeriodoParaLaFecha_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);

        var response = await client.PutAsJsonAsync($"{Ruta}/{conciliacionId}", new { fecha = TestData.FechaSinPeriodo, saldoExtracto = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("No existe un periodo contable", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Editar_ConExtractoInvalido_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);

        var response = await client.PutAsJsonAsync($"{Ruta}/{conciliacionId}", new { fecha = e.Inicio.AddDays(30), saldoExtracto = 1.005m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El saldo del extracto no puede tener más de 2 decimales.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Editar_Inexistente_Responde404()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PutAsJsonAsync($"{Ruta}/{IdInexistente}", new { fecha = e.Inicio.AddDays(30), saldoExtracto = 0m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Editar_ConciliacionFinalizadaOCancelada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var finalizadaId = await NuevaConciliacionAsync(e, estado: "Conciliado");
        var canceladaId = await NuevaConciliacionAsync(e, estado: "Cancelada");
        var cuerpo = new { fecha = e.Inicio.AddDays(30), saldoExtracto = 0m };

        var finalizada = await client.PutAsJsonAsync($"{Ruta}/{finalizadaId}", cuerpo);
        var cancelada = await client.PutAsJsonAsync($"{Ruta}/{canceladaId}", cuerpo);

        Assert.Equal(HttpStatusCode.Conflict, finalizada.StatusCode);
        Assert.Equal(MensajeNoPendiente, await finalizada.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.Conflict, cancelada.StatusCode);
    }

    [Fact]
    public async Task Cancelar_LiberaLosMovimientosMarcadosYConservaLaFilaCancelada()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 40m);
        await _pg.Data.MarcarMovimientoAsync(conciliacionId, movimientoId);

        var response = await client.PostAsync($"{Ruta}/{conciliacionId}/cancelar", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Cancelada", body.GetProperty("estado").GetString());
        Assert.Equal(0, body.GetProperty("cantidadMovimientos").GetInt32());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM detalleconciliacion WHERE conciliacion_id = $1", conciliacionId));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.Usuario, "cancelar_conciliacion"));

        var nuevaId = await NuevaConciliacionAsync(e);
        var detalle = await (await client.GetAsync($"{Ruta}/{nuevaId}")).LeerJsonAsync();
        Assert.Contains(movimientoId, detalle.GetProperty("disponibles").Ids());
    }

    [Fact]
    public async Task Cancelar_Inexistente_Responde404()
    {
        var (_, client) = await PrepararAsync();

        var response = await client.PostAsync($"{Ruta}/{IdInexistente}/cancelar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelar_FinalizadaOYaCancelada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var finalizadaId = await NuevaConciliacionAsync(e, estado: "Conciliado");
        var canceladaId = await NuevaConciliacionAsync(e, estado: "Cancelada");

        var finalizada = await client.PostAsync($"{Ruta}/{finalizadaId}/cancelar", null);
        var cancelada = await client.PostAsync($"{Ruta}/{canceladaId}/cancelar", null);

        Assert.Equal(HttpStatusCode.Conflict, finalizada.StatusCode);
        Assert.Equal("Solo se puede cancelar una conciliación pendiente.", await finalizada.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.Conflict, cancelada.StatusCode);
    }

    [Fact]
    public async Task Marcar_Ingreso_SumaYEgreso_Resta_ActualizaDiferencia()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, 300m);
        var ingresoId = await NuevoMovimientoAsync(e, "Ingreso", 500m);
        var egresoId = await NuevoMovimientoAsync(e, "Egreso", 200m);

        var trasIngreso = await (await MarcarAsync(client, conciliacionId, ingresoId)).LeerJsonAsync();
        Assert.Equal(500m, trasIngreso.GetProperty("totalMarcado").GetDecimal());
        Assert.Equal(-200m, trasIngreso.GetProperty("diferencia").GetDecimal());

        var trasEgreso = await (await MarcarAsync(client, conciliacionId, egresoId)).LeerJsonAsync();
        Assert.Equal(300m, trasEgreso.GetProperty("totalMarcado").GetDecimal());
        Assert.Equal(300m, trasEgreso.GetProperty("saldoConciliado").GetDecimal());
        Assert.Equal(0m, trasEgreso.GetProperty("diferencia").GetDecimal());
        Assert.Equal(2, trasEgreso.GetProperty("cantidadMovimientos").GetInt32());
    }

    [Fact]
    public async Task Marcar_DeOtraCuenta_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var (otraCuentaId, otraContableId) = await _pg.Data.CrearCuentaBancariaAsync();
        var ajenoId = await _pg.Data.CrearMovimientoAsync(otraCuentaId, otraContableId, e.Contrapartida, "Ingreso", 10m, e.Inicio.AddDays(5), e.Periodo, e.Usuario);

        var response = await MarcarAsync(client, conciliacionId, ajenoId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El movimiento no pertenece a la cuenta bancaria de la conciliación.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Marcar_PosteriorAlCorte_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m, dia: 40);

        var response = await MarcarAsync(client, conciliacionId, movimientoId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El movimiento es posterior a la fecha de corte de la conciliación.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Marcar_MovimientoInexistente_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);

        var response = await MarcarAsync(client, conciliacionId, IdInexistente);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El movimiento indicado no existe.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Marcar_YaIncluidoEnOtraConciliacion_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var primeraId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m);
        await _pg.Data.MarcarMovimientoAsync(primeraId, movimientoId);
        await _pg.Data.ForzarEstadoConciliacionAsync(primeraId, "Conciliado");
        var segundaId = await NuevaConciliacionAsync(e);

        var response = await MarcarAsync(client, segundaId, movimientoId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("El movimiento ya está incluido en una conciliación.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Marcar_ConciliacionInexistente_Responde404()
    {
        var (e, client) = await PrepararAsync();
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m);

        var response = await MarcarAsync(client, IdInexistente, movimientoId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Marcar_ConciliacionFinalizada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, estado: "Conciliado");
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m);

        var response = await MarcarAsync(client, conciliacionId, movimientoId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MensajeNoPendiente, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Desmarcar_Responde200YActualizaElResumen()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m);
        await _pg.Data.MarcarMovimientoAsync(conciliacionId, movimientoId);

        var response = await client.DeleteAsync($"{Ruta}/{conciliacionId}/movimientos/{movimientoId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(0, body.GetProperty("cantidadMovimientos").GetInt32());
        Assert.Equal(0m, body.GetProperty("totalMarcado").GetDecimal());
    }

    [Fact]
    public async Task Desmarcar_MarcaInexistente_Responde404()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 10m);

        var response = await client.DeleteAsync($"{Ruta}/{conciliacionId}/movimientos/{movimientoId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Desmarcar_ConciliacionFinalizada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, estado: "Conciliado");

        var response = await client.DeleteAsync($"{Ruta}/{conciliacionId}/movimientos/{IdInexistente}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MensajeNoPendiente, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Obtener_DevuelveMarcadosYDisponibles()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var marcadoId = await NuevoMovimientoAsync(e, "Ingreso", 10m, dia: 3);
        var disponibleId = await NuevoMovimientoAsync(e, "Egreso", 4m, dia: 6);
        var posteriorId = await NuevoMovimientoAsync(e, "Ingreso", 1m, dia: 40);
        await _pg.Data.MarcarMovimientoAsync(conciliacionId, marcadoId);

        var response = await client.GetAsync($"{Ruta}/{conciliacionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(new HashSet<int> { marcadoId }, body.GetProperty("marcados").Ids());
        var disponibles = body.GetProperty("disponibles").Ids();
        Assert.Contains(disponibleId, disponibles);
        Assert.DoesNotContain(marcadoId, disponibles);
        Assert.DoesNotContain(posteriorId, disponibles);
        Assert.Equal(conciliacionId, body.GetProperty("resumen").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Obtener_Finalizada_NoTraeDisponibles()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, estado: "Conciliado");
        await NuevoMovimientoAsync(e, "Ingreso", 10m);

        var body = await (await client.GetAsync($"{Ruta}/{conciliacionId}")).LeerJsonAsync();

        Assert.Equal(0, body.GetProperty("disponibles").GetArrayLength());
    }

    [Fact]
    public async Task Obtener_Inexistente_Responde404()
    {
        var (_, client) = await PrepararAsync();

        var response = await client.GetAsync($"{Ruta}/{IdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TC12_Finalizar_ConPerfilNoAutorizado_Responde403()
    {
        var (e, _) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        var vendedorEnBd = await _pg.Data.CrearUsuarioAsync("Vendedor");

        var porBd = await _pg.CreateApiClient(vendedorEnBd, Roles.Contador).PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);
        var porRol = await _pg.CreateApiClient(vendedorEnBd, Roles.Vendedor).PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);

        Assert.Equal(HttpStatusCode.Forbidden, porBd.StatusCode);
        Assert.Contains("perfil autorizado", await porBd.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.Forbidden, porRol.StatusCode);
        Assert.Equal("Pendiente", await EstadoAsync(conciliacionId));
    }

    [Fact]
    public async Task TC12_Finalizar_ConPerfilAutorizado_Responde200Conciliado()
    {
        foreach (var (perfil, rol) in PerfilesAutorizados)
        {
            var e = await _pg.Data.SembrarTesoreriaAsync();
            var usuarioId = await _pg.Data.CrearUsuarioAsync(perfil);
            var client = _pg.CreateApiClient(usuarioId, rol);
            var conciliacionId = await NuevaConciliacionAsync(e);

            var response = await client.PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.LeerJsonAsync();
            Assert.Equal("Conciliado", body.GetProperty("estado").GetString());
            Assert.Equal(0m, body.GetProperty("diferencia").GetDecimal());
            Assert.Equal("Conciliado", await EstadoAsync(conciliacionId));
            Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(usuarioId, "finalizar_conciliacion"));
        }
    }

    [Fact]
    public async Task Finalizar_ConDiferenciaDistintaDeCero_Responde409YSigueePendiente()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, 100m);

        var response = await client.PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("diferencia", await response.LeerErrorAsync());
        Assert.Equal("Pendiente", await EstadoAsync(conciliacionId));
    }

    [Fact]
    public async Task Finalizar_YaConciliada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Ruta}/{conciliacionId}/finalizar", null)).StatusCode);

        var response = await client.PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ya está finalizada", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Finalizar_Cancelada_Responde409()
    {
        var (e, client) = await PrepararAsync();
        var conciliacionId = await NuevaConciliacionAsync(e, estado: "Cancelada");

        var response = await client.PostAsync($"{Ruta}/{conciliacionId}/finalizar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("está cancelada", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Finalizar_Inexistente_Responde404()
    {
        var (_, client) = await PrepararAsync();

        var response = await client.PostAsync($"{Ruta}/{IdInexistente}/finalizar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Crear_SegundaConciliacion_TomaComoSaldoInicialElExtractoDeLaPrimera()
    {
        var (e, client) = await PrepararAsync();
        var primeraId = await NuevaConciliacionAsync(e, 500m);
        var movimientoId = await NuevoMovimientoAsync(e, "Ingreso", 500m);
        await _pg.Data.MarcarMovimientoAsync(primeraId, movimientoId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Ruta}/{primeraId}/finalizar", null)).StatusCode);

        var response = await client.PostAsJsonAsync(Ruta, PayloadConciliacion(e, 500m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(500m, body.GetProperty("saldoInicial").GetDecimal());
        Assert.Equal(0m, body.GetProperty("diferencia").GetDecimal());
    }

    [Fact]
    public async Task Listar_FiltraPorCuentaYEstado()
    {
        var (e, client) = await PrepararAsync();
        var canceladaId = await NuevaConciliacionAsync(e, estado: "Cancelada");
        var pendienteId = await NuevaConciliacionAsync(e);
        var (otraCuentaId, _) = await _pg.Data.CrearCuentaBancariaAsync();
        var ajenaId = await _pg.Data.CrearConciliacionAsync(otraCuentaId, e.Periodo, e.Inicio.AddDays(30), 0m);

        var porCuenta = (await (await client.GetAsync($"{Ruta}?cuentaBancariaId={e.CuentaBancaria}")).LeerJsonAsync()).Ids();
        var porEstado = (await (await client.GetAsync($"{Ruta}?cuentaBancariaId={e.CuentaBancaria}&estado=Cancelada")).LeerJsonAsync()).Ids();
        var sinFiltro = (await (await client.GetAsync(Ruta)).LeerJsonAsync()).Ids();

        Assert.Equal(new HashSet<int> { canceladaId, pendienteId }, porCuenta);
        Assert.Equal(new HashSet<int> { canceladaId }, porEstado);
        Assert.Contains(ajenaId, sinFiltro);
    }
}
