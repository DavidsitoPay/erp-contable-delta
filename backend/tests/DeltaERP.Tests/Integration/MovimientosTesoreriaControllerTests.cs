using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class MovimientosTesoreriaControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private const string Ruta = "/api/movimientos-tesoreria";

    private static readonly (decimal Monto, string Mensaje)[] MontosInvalidos =
    {
        (0m, "El monto debe ser mayor a cero."),
        (-5m, "El monto debe ser mayor a cero."),
        (1.005m, "El monto no puede tener más de 2 decimales."),
    };

    private readonly PostgresFixture _pg;

    public MovimientosTesoreriaControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object PayloadMovimiento(
        EscenarioTesoreria e, string tipo, decimal monto, int? contrapartidaId = null, DateOnly? fecha = null,
        string descripcion = "Movimiento de prueba", string? referencia = "REF-1") => new
    {
        cuentaBancariaId = e.CuentaBancaria,
        fecha = fecha ?? e.Inicio.AddDays(10),
        tipo,
        monto,
        descripcion,
        referencia,
        cuentaContrapartidaId = contrapartidaId ?? e.Contrapartida,
    };

    private static object PayloadTransferencia(int origenId, int destinoId, DateOnly fecha, decimal monto = 250m) => new
    {
        cuentaOrigenId = origenId,
        cuentaDestinoId = destinoId,
        fecha,
        monto,
        descripcion = "Traslado",
        referencia = (string?)null,
    };

    private static bool EstaConciliado(JsonElement lista, int movimientoId) =>
        lista.EnumerateArray().Single(m => m.GetProperty("id").GetInt32() == movimientoId).GetProperty("conciliado").GetBoolean();

    private async Task<(EscenarioTesoreria E, HttpClient Client)> PrepararAsync()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        return (e, _pg.CreateApiClient(e.Usuario));
    }

    [Fact]
    public async Task Registrar_Ingreso_CreaAsientoConfirmadoConBancoAlDebe()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 100.50m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        var asientoId = body.GetProperty("asientoId").GetInt32();
        Assert.Equal("Manual", body.GetProperty("origen").GetString());
        Assert.Equal(100.50m, body.GetProperty("monto").GetDecimal());
        Assert.Equal("Confirmado", await _pg.Data.ScalarAsync<string>("SELECT estado FROM asientocontable WHERE id = $1", asientoId));
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(100.50m, await _pg.Data.DebitoAsync(asientoId, e.CuentaContableBanco));
        Assert.Equal(100.50m, await _pg.Data.CreditoAsync(asientoId, e.Contrapartida));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.Usuario, "registrar_movimiento_tesoreria"));
    }

    [Fact]
    public async Task Registrar_Egreso_CreaAsientoConBancoAlHaber()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 75m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asientoId = (await response.LeerJsonAsync()).GetProperty("asientoId").GetInt32();
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM lineaasiento WHERE asiento_id = $1", asientoId));
        Assert.Equal(75m, await _pg.Data.CreditoAsync(asientoId, e.CuentaContableBanco));
        Assert.Equal(75m, await _pg.Data.DebitoAsync(asientoId, e.Contrapartida));
        Assert.Equal(-75m, await _pg.Data.SaldoBancarioAsync(e.CuentaBancaria));
    }

    [Fact]
    public async Task Registrar_ConMontoCeroNegativoODemasiadosDecimales_Responde400()
    {
        var (e, client) = await PrepararAsync();

        foreach (var (monto, mensaje) in MontosInvalidos)
        {
            var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", monto));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(mensaje, await response.LeerErrorAsync());
        }
    }

    [Fact]
    public async Task Registrar_ConTipoInvalido_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var tipo = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Otro", 10m));

        Assert.Equal(HttpStatusCode.BadRequest, tipo.StatusCode);
        Assert.Equal("El tipo debe ser Ingreso o Egreso.", await tipo.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConDescripcionInvalida_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var vacia = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, descripcion: ""));
        var larga = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, descripcion: new string('x', 256)));

        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, larga.StatusCode);
        var vaciaBody = await vacia.LeerJsonAsync();
        var largaBody = await larga.LeerJsonAsync();
        Assert.True(vaciaBody.TryGetProperty("errors", out _));
        Assert.True(largaBody.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Registrar_ConCuentaInexistenteOInactiva_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var (inactivaId, _) = await _pg.Data.CrearCuentaBancariaAsync();
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET activa = false WHERE id = $1", inactivaId);

        var inexistente = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e with { CuentaBancaria = IdInexistente }, "Ingreso", 10m));
        var inactiva = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e with { CuentaBancaria = inactivaId }, "Ingreso", 10m));

        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Equal("La cuenta bancaria indicada no existe.", await inexistente.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, inactiva.StatusCode);
        Assert.Contains("está inactiva", await inactiva.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_SinPeriodoParaLaFecha_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, fecha: TestData.FechaSinPeriodo));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("No existe un periodo contable que contenga la fecha 1990-06-15.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_EnPeriodoCerrado_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, fecha: inicio.AddDays(3)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no esté Abierto", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConContrapartidaInexistenteInactivaDeMayorOIgualAlBanco_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var inactivaId = await _pg.Data.CrearCuentaInactivaAsync("Gasto", "Deudora");
        var (padreId, _) = await _pg.Data.CrearCuentaConSubcuentasAsync("Gasto", "Deudora");

        var inexistente = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 10m, IdInexistente));
        var inactiva = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 10m, inactivaId));
        var deMayor = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 10m, padreId));
        var igualAlBanco = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 10m, e.CuentaContableBanco));

        Assert.Contains("no existen", await inexistente.LeerErrorAsync());
        Assert.Contains("inactivas", await inactiva.LeerErrorAsync());
        Assert.Contains("de mayor", await deMayor.LeerErrorAsync());
        Assert.Equal("La contrapartida no puede ser la cuenta contable del banco.", await igualAlBanco.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, igualAlBanco.StatusCode);
    }

    [Fact]
    public async Task RegistrarManual_ConContrapartidaDeOtraCuentaBancaria_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var (_, ctaBanco2) = await _pg.Data.CrearCuentaBancariaAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, ctaBanco2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La contrapartida no puede ser la cuenta contable de una cuenta bancaria; usa una transferencia.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Registrar_ConContrapartidaDeGastoODeIngreso_Responde201()
    {
        var (e, client) = await PrepararAsync();
        var gastoId = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");

        var comision = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Egreso", 12m, gastoId));
        var interes = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 8m, e.Contrapartida));

        Assert.Equal(HttpStatusCode.Created, comision.StatusCode);
        Assert.Equal(HttpStatusCode.Created, interes.StatusCode);
        Assert.Equal(-4m, await _pg.Data.SaldoBancarioAsync(e.CuentaBancaria));
    }

    [Fact]
    public async Task Listar_IndicaConciliadoSoloSiLaConciliacionEstaFinalizada()
    {
        var (e, client) = await PrepararAsync();
        var movimientoId = await _pg.Data.CrearMovimientoAsync(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida, "Ingreso", 100m, e.Inicio.AddDays(2), e.Periodo, e.Usuario);
        var conciliacionId = await _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(30), 0m);
        var ruta = $"{Ruta}?cuentaBancariaId={e.CuentaBancaria}";

        Assert.False(EstaConciliado(await (await client.GetAsync(ruta)).LeerJsonAsync(), movimientoId));

        await _pg.Data.MarcarMovimientoAsync(conciliacionId, movimientoId);
        Assert.False(EstaConciliado(await (await client.GetAsync(ruta)).LeerJsonAsync(), movimientoId));

        await _pg.Data.ForzarEstadoConciliacionAsync(conciliacionId, "Conciliado");
        Assert.True(EstaConciliado(await (await client.GetAsync(ruta)).LeerJsonAsync(), movimientoId));
    }

    [Fact]
    public async Task Registrar_ConReferenciaDemasiadoLarga_Responde400SinCrearNada()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m, referencia: new string('r', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM asientocontable WHERE usuario_id = $1 AND numero LIKE 'TES-MAN-%'", e.Usuario));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", e.CuentaBancaria));
        Assert.Equal(0, await _pg.Data.ContarAuditoriaAsync(e.Usuario, "registrar_movimiento_tesoreria"));
    }

    [Fact]
    public async Task Listar_FiltraPorCuentaFechasYOrigen()
    {
        var (e, client) = await PrepararAsync();
        var primero = await _pg.Data.CrearMovimientoAsync(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida, "Ingreso", 10m, e.Inicio.AddDays(1), e.Periodo, e.Usuario);
        var segundo = await _pg.Data.CrearMovimientoAsync(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida, "Egreso", 5m, e.Inicio.AddDays(20), e.Periodo, e.Usuario);
        var (otraCuentaId, otraContableId) = await _pg.Data.CrearCuentaBancariaAsync();
        var ajeno = await _pg.Data.CrearMovimientoAsync(otraCuentaId, otraContableId, e.Contrapartida, "Ingreso", 1m, e.Inicio.AddDays(1), e.Periodo, e.Usuario);
        var corte = e.Inicio.AddDays(10).ToString("yyyy-MM-dd");
        var baseRuta = $"{Ruta}?cuentaBancariaId={e.CuentaBancaria}";

        var todos = (await (await client.GetAsync(baseRuta)).LeerJsonAsync()).Ids();
        var desde = (await (await client.GetAsync($"{baseRuta}&desde={corte}")).LeerJsonAsync()).Ids();
        var hasta = (await (await client.GetAsync($"{baseRuta}&hasta={corte}")).LeerJsonAsync()).Ids();
        var manual = (await (await client.GetAsync($"{baseRuta}&origen=Manual")).LeerJsonAsync()).Ids();
        var cxc = (await (await client.GetAsync($"{baseRuta}&origen=CxC")).LeerJsonAsync()).Ids();
        var sinFiltro = (await (await client.GetAsync(Ruta)).LeerJsonAsync()).Ids();

        Assert.Equal(new HashSet<int> { primero, segundo }, todos);
        Assert.Equal(new HashSet<int> { segundo }, desde);
        Assert.Equal(new HashSet<int> { primero }, hasta);
        Assert.Equal(new HashSet<int> { primero, segundo }, manual);
        Assert.Empty(cxc);
        Assert.Contains(ajeno, sinFiltro);
    }

    [Fact]
    public async Task Listar_ConDesdePosteriorAHasta_Responde400()
    {
        var (_, client) = await PrepararAsync();

        var response = await client.GetAsync($"{Ruta}?desde=2025-03-10&hasta=2025-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La fecha inicial no puede ser posterior a la final.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Transferir_ConDatosValidos_CreaUnAsientoYDosMovimientos()
    {
        var (e, client) = await PrepararAsync();
        var (destinoId, destinoContableId) = await _pg.Data.CrearCuentaBancariaAsync();

        var response = await client.PostAsJsonAsync($"{Ruta}/transferencias", PayloadTransferencia(e.CuentaBancaria, destinoId, e.Inicio.AddDays(4)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        var transferenciaId = body.GetProperty("transferenciaId").GetGuid();
        var asientoId = body.GetProperty("asientoId").GetInt32();
        Assert.Equal("Egreso", body.GetProperty("egreso").GetProperty("tipo").GetString());
        Assert.Equal("Ingreso", body.GetProperty("ingreso").GetProperty("tipo").GetString());
        Assert.Equal(2, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE transferencia_id = $1", transferenciaId));
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(DISTINCT asiento_id) FROM movimientotesoreria WHERE transferencia_id = $1", transferenciaId));
        Assert.Equal(250m, await _pg.Data.DebitoAsync(asientoId, destinoContableId));
        Assert.Equal(250m, await _pg.Data.CreditoAsync(asientoId, e.CuentaContableBanco));
        Assert.Equal(-250m, await _pg.Data.SaldoBancarioAsync(e.CuentaBancaria));
        Assert.Equal(250m, await _pg.Data.SaldoBancarioAsync(destinoId));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.Usuario, "registrar_transferencia_tesoreria"));
    }

    [Fact]
    public async Task Transferir_AMismaCuenta_Responde400()
    {
        var (e, client) = await PrepararAsync();

        var response = await client.PostAsJsonAsync($"{Ruta}/transferencias", PayloadTransferencia(e.CuentaBancaria, e.CuentaBancaria, e.Inicio.AddDays(4)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La cuenta de origen y la de destino deben ser distintas.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Transferir_ConCuentaInactiva_Responde400()
    {
        var (e, client) = await PrepararAsync();
        var (otraId, _) = await _pg.Data.CrearCuentaBancariaAsync();
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET activa = false WHERE id = $1", otraId);

        var comoDestino = await client.PostAsJsonAsync($"{Ruta}/transferencias", PayloadTransferencia(e.CuentaBancaria, otraId, e.Inicio.AddDays(4)));
        var comoOrigen = await client.PostAsJsonAsync($"{Ruta}/transferencias", PayloadTransferencia(otraId, e.CuentaBancaria, e.Inicio.AddDays(4)));

        Assert.Equal(HttpStatusCode.BadRequest, comoDestino.StatusCode);
        Assert.Contains("está inactiva", await comoDestino.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, comoOrigen.StatusCode);
        Assert.Contains("está inactiva", await comoOrigen.LeerErrorAsync());
    }

    [Fact]
    public async Task Transferir_EnPeriodoCerrado_Responde400SinFilasResiduales()
    {
        var (e, client) = await PrepararAsync();
        var (destinoId, _) = await _pg.Data.CrearCuentaBancariaAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");

        var response = await client.PostAsJsonAsync($"{Ruta}/transferencias", PayloadTransferencia(e.CuentaBancaria, destinoId, inicio.AddDays(2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no esté Abierto", await response.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1 OR cuenta_bancaria_id = $2", e.CuentaBancaria, destinoId));
    }

    [Fact]
    public async Task Endpoints_SegunElRol_RechazanVendedorTecnicoYAnonimo()
    {
        var (e, _) = await PrepararAsync();

        var vendedor = await _pg.CreateApiClient(e.Usuario, Roles.Vendedor).GetAsync(Ruta);
        var tecnico = await _pg.CreateApiClient(e.Usuario, Roles.Tecnico).PostAsJsonAsync(Ruta, PayloadMovimiento(e, "Ingreso", 10m));
        var anonimo = await _pg.Factory.CreateClient().GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Forbidden, vendedor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tecnico.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
    }
}
