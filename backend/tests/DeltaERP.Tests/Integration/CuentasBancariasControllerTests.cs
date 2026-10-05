using System.Net;
using System.Net.Http.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class CuentasBancariasControllerTests
{
    private const int IdInexistente = 2_000_000_000;
    private const string Ruta = "/api/cuentas-bancarias";

    private readonly PostgresFixture _pg;

    public CuentasBancariasControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private sealed record Contexto(HttpClient Client, int UsuarioId, int CuentaContableId);

    private async Task<Contexto> PrepararAsync()
    {
        var usuarioId = await _pg.Data.CrearUsuarioAsync();
        var cuentaContableId = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        return new Contexto(_pg.CreateApiClient(usuarioId), usuarioId, cuentaContableId);
    }

    private async Task<(DateOnly Fecha, int CapitalId)> PrepararAperturaAsync()
    {
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin);
        return (inicio.AddDays(5), await _pg.Data.CrearCuentaAsync("Capital", "Acreedora"));
    }

    private static object Payload(
        int cuentaContableId, string? numero = null, string banco = "Banco de Prueba", string tipo = "Monetaria",
        decimal saldoApertura = 0m, DateOnly? fechaApertura = null, int? contrapartidaId = null) => new
    {
        banco,
        numero = numero ?? $"N{TestData.Sufijo()}",
        tipo,
        cuentaContableId,
        saldoApertura,
        fechaApertura,
        cuentaContrapartidaId = contrapartidaId,
    };

    private static object PayloadMovimiento(int cuentaBancariaId, DateOnly fecha, string tipo, decimal monto, int contrapartidaId) => new
    {
        cuentaBancariaId,
        fecha,
        tipo,
        monto,
        descripcion = "Movimiento de prueba",
        referencia = (string?)null,
        cuentaContrapartidaId = contrapartidaId,
    };

    private static async Task<int> CrearCuentaApiAsync(Contexto ctx)
    {
        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201ConSaldoCero()
    {
        var ctx = await PrepararAsync();

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.LeerJsonAsync();
        Assert.True(body.GetProperty("activa").GetBoolean());
        Assert.Equal(0m, body.GetProperty("saldo").GetDecimal());
        Assert.Equal(0m, body.GetProperty("saldoApertura").GetDecimal());
        Assert.Equal(ctx.CuentaContableId, body.GetProperty("cuentaContableId").GetInt32());
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(ctx.UsuarioId, "crear_cuenta_bancaria"));
    }

    [Fact]
    public async Task Crear_ConSaldoDeApertura_CreaAsientoYMovimientoDeAperturaYElSaldoLoIncluyeUnaVez()
    {
        var ctx = await PrepararAsync();
        var (fecha, capitalId) = await PrepararAperturaAsync();

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 1000.50m, fechaApertura: fecha, contrapartidaId: capitalId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.LeerJsonAsync();
        var cuentaId = body.GetProperty("id").GetInt32();
        Assert.Equal(1000.50m, body.GetProperty("saldo").GetDecimal());
        Assert.Equal(1000.50m, body.GetProperty("saldoApertura").GetDecimal());
        Assert.Equal(1, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaId));
        Assert.Equal("Apertura", await _pg.Data.ScalarAsync<string>("SELECT origen FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaId));
        Assert.Equal("Ingreso", await _pg.Data.ScalarAsync<string>("SELECT tipo FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaId));
        var asientoId = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaId);
        Assert.Equal(1000.50m, await _pg.Data.DebitoAsync(asientoId, ctx.CuentaContableId));
        Assert.Equal(1000.50m, await _pg.Data.CreditoAsync(asientoId, capitalId));
        Assert.Equal(1000.50m, await _pg.Data.SaldoBancarioAsync(cuentaId));
    }

    [Fact]
    public async Task Crear_ConSaldoDeAperturaSinFechaOSinContrapartida_Responde400()
    {
        var ctx = await PrepararAsync();
        var (fecha, capitalId) = await PrepararAperturaAsync();

        var sinFecha = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 10m, contrapartidaId: capitalId));
        var sinContrapartida = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 10m, fechaApertura: fecha));

        Assert.Equal(HttpStatusCode.BadRequest, sinFecha.StatusCode);
        Assert.Equal("La fecha de apertura es obligatoria cuando hay saldo de apertura.", await sinFecha.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinContrapartida.StatusCode);
        Assert.Equal("La cuenta de contrapartida de la apertura es obligatoria cuando hay saldo de apertura.", await sinContrapartida.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConAperturaNegativaODecimalesExcesivos_Responde400()
    {
        var ctx = await PrepararAsync();

        var negativa = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: -1m));
        var decimales = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 10.005m));
        var excesiva = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 1000000000000m));

        Assert.Equal("El saldo de apertura no puede ser negativo.", await negativa.LeerErrorAsync());
        Assert.Equal("El saldo de apertura no puede tener más de 2 decimales.", await decimales.LeerErrorAsync());
        Assert.Equal("El saldo de apertura excede el máximo permitido.", await excesiva.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, negativa.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, decimales.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, excesiva.StatusCode);
    }

    [Fact]
    public async Task Crear_ConContrapartidaQueNoEsCapital_Responde400()
    {
        var ctx = await PrepararAsync();
        var (fecha, _) = await PrepararAperturaAsync();
        var ingresoId = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 10m, fechaApertura: fecha, contrapartidaId: ingresoId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La cuenta de contrapartida de la apertura debe ser de tipo Capital.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConAperturaEnPeriodoCerradoOSinPeriodo_Responde400SinDejarFilas()
    {
        var ctx = await PrepararAsync();
        var capitalId = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin, "Cerrado");
        var numeroCerrado = $"N{TestData.Sufijo()}";
        var numeroSinPeriodo = $"N{TestData.Sufijo()}";

        var cerrado = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, numeroCerrado, saldoApertura: 10m, fechaApertura: inicio.AddDays(1), contrapartidaId: capitalId));
        var sinPeriodo = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, numeroSinPeriodo, saldoApertura: 10m, fechaApertura: TestData.FechaSinPeriodo, contrapartidaId: capitalId));

        Assert.Equal(HttpStatusCode.BadRequest, cerrado.StatusCode);
        Assert.Contains("no esté Abierto", await cerrado.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinPeriodo.StatusCode);
        Assert.Contains("No existe un periodo contable", await sinPeriodo.LeerErrorAsync());
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM cuentabancaria WHERE numero = $1 OR numero = $2", numeroCerrado, numeroSinPeriodo));
    }

    [Fact]
    public async Task Crear_ConAperturaCero_IgnoraFechaYContrapartida()
    {
        var ctx = await PrepararAsync();

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, saldoApertura: 0m, fechaApertura: new DateOnly(2025, 1, 1), contrapartidaId: IdInexistente));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cuentaId = (await response.LeerJsonAsync()).GetProperty("id").GetInt32();
        Assert.True(await _pg.Data.ScalarAsync<bool>("SELECT fecha_apertura IS NULL FROM cuentabancaria WHERE id = $1", cuentaId));
        Assert.Equal(0, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM movimientotesoreria WHERE cuenta_bancaria_id = $1", cuentaId));
    }

    [Fact]
    public async Task Actualizar_NoCambiaLaAperturaNiLaCuentaContable()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);
        var otraCuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var cambios = new
        {
            banco = "Banco Nuevo",
            numero = $"N{TestData.Sufijo()}",
            tipo = "Ahorro",
            cuentaContableId = otraCuentaContable,
            saldoApertura = 500m,
            fechaApertura = new DateOnly(2025, 1, 1),
            cuentaContrapartidaId = (int?)null,
        };

        var response = await ctx.Client.PutAsJsonAsync($"{Ruta}/{cuentaId}", cambios);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Banco Nuevo", body.GetProperty("banco").GetString());
        Assert.Equal("Ahorro", body.GetProperty("tipo").GetString());
        Assert.Equal(ctx.CuentaContableId, body.GetProperty("cuentaContableId").GetInt32());
        Assert.Equal(0m, body.GetProperty("saldoApertura").GetDecimal());
        Assert.Equal(ctx.CuentaContableId, await _pg.Data.ScalarAsync<int>("SELECT cuenta_contable_id FROM cuentabancaria WHERE id = $1", cuentaId));
    }

    [Fact]
    public async Task TC11_IngresoAumentaElSaldoDeLaVistaYDelEndpoint()
    {
        var ctx = await PrepararAsync();
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        await _pg.Data.CrearPeriodoEnRangoAsync(inicio, fin);
        var contrapartidaId = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var cuentaId = await CrearCuentaApiAsync(ctx);
        var fecha = inicio.AddDays(10);

        var ingreso = await ctx.Client.PostAsJsonAsync("/api/movimientos-tesoreria", PayloadMovimiento(cuentaId, fecha, "Ingreso", 500m, contrapartidaId));
        Assert.Equal(HttpStatusCode.Created, ingreso.StatusCode);

        var trasIngreso = await ctx.Client.GetAsync($"{Ruta}/{cuentaId}");
        Assert.Equal(500m, (await trasIngreso.LeerJsonAsync()).GetProperty("saldo").GetDecimal());
        Assert.Equal(500m, await _pg.Data.SaldoBancarioAsync(cuentaId));

        var egreso = await ctx.Client.PostAsJsonAsync("/api/movimientos-tesoreria", PayloadMovimiento(cuentaId, fecha, "Egreso", 200m, contrapartidaId));
        Assert.Equal(HttpStatusCode.Created, egreso.StatusCode);

        var trasEgreso = await ctx.Client.GetAsync($"{Ruta}/{cuentaId}");
        Assert.Equal(300m, (await trasEgreso.LeerJsonAsync()).GetProperty("saldo").GetDecimal());
        Assert.Equal(300m, await _pg.Data.SaldoBancarioAsync(cuentaId));
    }

    [Fact]
    public async Task Crear_ConBancoYNumeroDuplicados_Responde409()
    {
        var ctx = await PrepararAsync();
        var otraCuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var numero = $"N{TestData.Sufijo()}";
        var primera = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, numero));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var segunda = await ctx.Client.PostAsJsonAsync(Ruta, Payload(otraCuentaContable, numero));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal($"Ya existe la cuenta bancaria Banco de Prueba {numero}.", await segunda.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConCuentaContableYaAsociada_Responde409()
    {
        var ctx = await PrepararAsync();
        await CrearCuentaApiAsync(ctx);

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("La cuenta contable ya está asociada a otra cuenta bancaria.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConCuentaContableDePasivo_Responde400()
    {
        var ctx = await PrepararAsync();
        var pasivoId = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");

        var response = await ctx.Client.PostAsJsonAsync(Ruta, Payload(pasivoId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La cuenta contable del banco debe ser de tipo Activo.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConCuentaContableInactivaDeMayorOInexistente_Responde400()
    {
        var ctx = await PrepararAsync();
        var inactivaId = await _pg.Data.CrearCuentaInactivaAsync("Activo", "Deudora");
        var (padreId, _) = await _pg.Data.CrearCuentaConSubcuentasAsync("Activo", "Deudora");

        var inactiva = await ctx.Client.PostAsJsonAsync(Ruta, Payload(inactivaId));
        var deMayor = await ctx.Client.PostAsJsonAsync(Ruta, Payload(padreId));
        var inexistente = await ctx.Client.PostAsJsonAsync(Ruta, Payload(IdInexistente));

        Assert.Contains("inactivas", await inactiva.LeerErrorAsync());
        Assert.Contains("de mayor", await deMayor.LeerErrorAsync());
        Assert.Contains("no existen", await inexistente.LeerErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, inactiva.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deMayor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
    }

    [Fact]
    public async Task Crear_ConTipoInvalido_Responde400()
    {
        var ctx = await PrepararAsync();

        var tipo = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, tipo: "Corriente"));

        Assert.Equal(HttpStatusCode.BadRequest, tipo.StatusCode);
        Assert.Equal("El tipo debe ser Monetaria o Ahorro.", await tipo.LeerErrorAsync());
    }

    [Fact]
    public async Task Crear_ConBancoVacio_Responde400()
    {
        var ctx = await PrepararAsync();

        var banco = await ctx.Client.PostAsJsonAsync(Ruta, Payload(ctx.CuentaContableId, banco: ""));

        Assert.Equal(HttpStatusCode.BadRequest, banco.StatusCode);
        var body = await banco.LeerJsonAsync();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Actualizar_ConDatosValidos_Responde200()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);
        var numero = $"N{TestData.Sufijo()}";

        var response = await ctx.Client.PutAsJsonAsync($"{Ruta}/{cuentaId}", Payload(ctx.CuentaContableId, numero, banco: "Banco Editado", tipo: "Ahorro"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal("Banco Editado", body.GetProperty("banco").GetString());
        Assert.Equal(numero, body.GetProperty("numero").GetString());
        Assert.Equal("Ahorro", body.GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task Actualizar_ConDatosInvalidos_Responde400()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);

        var response = await ctx.Client.PutAsJsonAsync($"{Ruta}/{cuentaId}", Payload(ctx.CuentaContableId, tipo: "Corriente"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El tipo debe ser Monetaria o Ahorro.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_Inexistente_Responde404()
    {
        var ctx = await PrepararAsync();

        var response = await ctx.Client.PutAsJsonAsync($"{Ruta}/{IdInexistente}", Payload(ctx.CuentaContableId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Actualizar_ConDuplicado_Responde409()
    {
        var ctx = await PrepararAsync();
        var otraCuentaContable = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var primeraId = await CrearCuentaApiAsync(ctx);
        var segundaResponse = await ctx.Client.PostAsJsonAsync(Ruta, Payload(otraCuentaContable));
        var segundaId = (await segundaResponse.LeerJsonAsync()).GetProperty("id").GetInt32();
        var numeroPrimera = await _pg.Data.ScalarAsync<string>("SELECT numero FROM cuentabancaria WHERE id = $1", primeraId);

        var response = await ctx.Client.PutAsJsonAsync($"{Ruta}/{segundaId}", Payload(otraCuentaContable, numeroPrimera));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"Ya existe la cuenta bancaria Banco de Prueba {numeroPrimera}.", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Desactivar_Responde204YOcultaDelListadoPorDefecto()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);

        var response = await ctx.Client.DeleteAsync($"{Ruta}/{cuentaId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var porDefecto = await (await ctx.Client.GetAsync(Ruta)).LeerJsonAsync();
        Assert.DoesNotContain(cuentaId, porDefecto.Ids());
        var conInactivas = await (await ctx.Client.GetAsync($"{Ruta}?incluirInactivas=true")).LeerJsonAsync();
        Assert.Contains(cuentaId, conInactivas.Ids());
    }

    [Fact]
    public async Task Desactivar_Inexistente_Responde404()
    {
        var ctx = await PrepararAsync();

        var response = await ctx.Client.DeleteAsync($"{Ruta}/{IdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Desactivar_YaInactiva_Responde204()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);
        await ctx.Client.DeleteAsync($"{Ruta}/{cuentaId}");

        var response = await ctx.Client.DeleteAsync($"{Ruta}/{cuentaId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Obtener_ConIdExistente_Responde200ConCuentaContable()
    {
        var ctx = await PrepararAsync();
        var cuentaId = await CrearCuentaApiAsync(ctx);

        var response = await ctx.Client.GetAsync($"{Ruta}/{cuentaId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(cuentaId, body.GetProperty("id").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("cuentaContableCodigo").GetString()));
        var listado = await (await ctx.Client.GetAsync(Ruta)).LeerJsonAsync();
        Assert.Contains(cuentaId, listado.Ids());
    }

    [Fact]
    public async Task Obtener_Inexistente_Responde404()
    {
        var ctx = await PrepararAsync();

        var response = await ctx.Client.GetAsync($"{Ruta}/{IdInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_SegunElRol_RechazanVendedorYTecnicoYAnonimo()
    {
        var usuarioId = await _pg.Data.CrearUsuarioAsync();

        var vendedor = await _pg.CreateApiClient(usuarioId, Roles.Vendedor).GetAsync(Ruta);
        var tecnico = await _pg.CreateApiClient(usuarioId, Roles.Tecnico).GetAsync(Ruta);
        var anonimo = await _pg.Factory.CreateClient().GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Forbidden, vendedor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tecnico.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
    }
}
