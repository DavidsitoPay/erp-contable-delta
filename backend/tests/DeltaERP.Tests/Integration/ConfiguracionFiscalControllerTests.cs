using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ConfiguracionFiscalControllerTests
{
    private const string Ruta = "/api/configuracion-fiscal";
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public ConfiguracionFiscalControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static object Cuerpo(int debitoId, int creditoId, string regimen = "UTILIDADES", string nit = "7654321-8") => new
    {
        nitEmpresa = nit,
        nombreLegal = "Delta Actualizada, S.A.",
        regimenIsr = regimen,
        agenteRetencionIva = false,
        tipoAgenteIva = (string?)null,
        cuentaIvaDebitoId = debitoId,
        cuentaIvaCreditoId = creditoId,
    };

    [Theory]
    [InlineData(Roles.Administrador)]
    [InlineData(Roles.Contador)]
    public async Task Obtener_ComoAdministradorOContador_DevuelveLaConfiguracionBase(string rol)
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), rol);

        var response = await client.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.LeerJsonAsync();
        Assert.Equal(("GTQ", TestData.NitValido, "UTILIDADES"), (body.GetProperty("monedaFuncionalCodigo").GetString(), body.GetProperty("nitEmpresa").GetString(), body.GetProperty("regimenIsr").GetString()));
        Assert.Equal(await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIvaDebito), body.GetProperty("cuentaIvaDebitoId").GetInt32());
    }

    [Fact]
    public async Task Obtener_ComoVendedorOAnonimo_Responde403Y401()
    {
        var vendedor = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Vendedor);

        Assert.Equal(HttpStatusCode.Forbidden, (await vendedor.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _pg.Factory.CreateClient().GetAsync(Ruta)).StatusCode);
    }

    [Fact]
    public async Task Actualizar_ComoAdministrador_PersisteNormalizaYAudita()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var client = _pg.CreateApiClient(usuario, Roles.Administrador);
        var debito = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIvaDebito);
        var credito = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIvaCredito);
        try
        {
            var response = await client.PutAsJsonAsync(Ruta, new
            {
                nitEmpresa = " 76543218 ",
                nombreLegal = "Delta Actualizada, S.A.",
                regimenIsr = "SIMPLIFICADO",
                agenteRetencionIva = true,
                tipoAgenteIva = "SECTOR_PUBLICO",
                cuentaIvaDebitoId = debito,
                cuentaIvaCreditoId = credito,
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.LeerJsonAsync();
            Assert.Equal(("7654321-8", "SIMPLIFICADO", "SECTOR_PUBLICO"), (body.GetProperty("nitEmpresa").GetString(), body.GetProperty("regimenIsr").GetString(), body.GetProperty("tipoAgenteIva").GetString()));
            Assert.StartsWith("Usuario ", body.GetProperty("actualizadoPorNombre").GetString());
            Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(usuario, "actualizar_configuracion_fiscal"));

            var sinAgente = await client.PutAsJsonAsync(Ruta, new
            {
                nitEmpresa = "7654321-8",
                nombreLegal = "Delta Actualizada, S.A.",
                regimenIsr = "UTILIDADES",
                agenteRetencionIva = false,
                tipoAgenteIva = "SECTOR_PUBLICO",
                cuentaIvaDebitoId = debito,
                cuentaIvaCreditoId = credito,
            });
            Assert.Equal(HttpStatusCode.OK, sinAgente.StatusCode);
            Assert.Equal(JsonValueKind.Null, (await sinAgente.LeerJsonAsync()).GetProperty("tipoAgenteIva").ValueKind);
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Theory]
    [InlineData(Roles.Contador)]
    [InlineData(Roles.Vendedor)]
    public async Task Actualizar_SinSerAdministrador_Responde403(string rol)
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), rol);

        var response = await client.PutAsJsonAsync(Ruta, Cuerpo(1, 2));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "Régimen de ISR inválido")]
    [InlineData(1, "La cuenta de IVA débito fiscal debe ser de tipo Pasivo y naturaleza Acreedora.")]
    [InlineData(2, "La cuenta de IVA crédito fiscal debe ser de tipo Activo y naturaleza Deudora.")]
    [InlineData(3, "están inactivas")]
    [InlineData(4, "no existen")]
    public async Task Actualizar_ConDatosInvalidos_Responde400SinCambiarLaConfiguracion(int caso, string fragmento)
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var inactiva = await _pg.Data.CrearCuentaInactivaAsync("Pasivo", "Acreedora");
        var cuerpo = caso switch
        {
            0 => Cuerpo(e.CuentaIvaDebito, e.CuentaIvaCredito, regimen: "OTRO"),
            1 => Cuerpo(e.CuentaCxC, e.CuentaIvaCredito),
            2 => Cuerpo(e.CuentaIvaDebito, e.CuentaCxP),
            3 => Cuerpo(inactiva, e.CuentaIvaCredito),
            _ => Cuerpo(IdInexistente, e.CuentaIvaCredito),
        };
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await client.PutAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(fragmento, await response.LeerErrorAsync());
        Assert.Equal(TestData.NitValido, await _pg.Data.ScalarAsync<string>("SELECT nit_empresa FROM configuracionfiscal WHERE id = 1"));
    }

    [Fact]
    public async Task Obtener_IncluyeLasCuentasDeRetencionSembradas()
    {
        var client = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Contador);

        var body = await client.GetFromJsonAsync<JsonElement>(Ruta);

        Assert.Equal(
            (await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIsrRetenido), await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIvaRetenidoClientes)),
            (body.GetProperty("cuentaIsrRetenidoPagarId").GetInt32(), body.GetProperty("cuentaIvaRetenidoClientesId").GetInt32()));
    }

    [Fact]
    public async Task Actualizar_ConLasCuentasDeRetencion_LasPersiste()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var isr = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");
        var ivaClientes = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);
        try
        {
            var response = await client.PutAsJsonAsync(Ruta, CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, isr, ivaClientes));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.LeerJsonAsync();
            Assert.Equal((isr, ivaClientes), (body.GetProperty("cuentaIsrRetenidoPagarId").GetInt32(), body.GetProperty("cuentaIvaRetenidoClientesId").GetInt32()));
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Fact]
    public async Task Actualizar_SinLasCuentasDeRetencion_ConservaLasConfiguradas()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var isr = await _pg.Data.CrearCuentaAsync("Pasivo", "Acreedora");
        var ivaClientes = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);
        try
        {
            await client.PutAsJsonAsync(Ruta, CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, isr, ivaClientes));

            var response = await client.PutAsJsonAsync(Ruta, Cuerpo(e.CuentaIvaDebito, e.CuentaIvaCredito));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.LeerJsonAsync();
            Assert.Equal((isr, ivaClientes), (body.GetProperty("cuentaIsrRetenidoPagarId").GetInt32(), body.GetProperty("cuentaIvaRetenidoClientesId").GetInt32()));
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Theory]
    [InlineData(0, "La cuenta de ISR retenido por pagar debe ser de tipo Pasivo y naturaleza Acreedora.")]
    [InlineData(1, "La cuenta de IVA retenido por clientes debe ser de tipo Activo y naturaleza Deudora.")]
    [InlineData(2, "deben ser distintas entre sí")]
    public async Task Actualizar_ConCuentasDeRetencionInvalidas_Responde400(int caso, string fragmento)
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var cuerpo = caso switch
        {
            0 => CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, e.CuentaCxC, null),
            1 => CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, null, e.CuentaCxP),
            _ => CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, e.CuentaIvaDebito, null),
        };
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await client.PutAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(fragmento, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_ConCuentaIsrNrDeTipoActivo_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var activo = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var cuerpo = CuerpoConRetenciones(e.CuentaIvaDebito, e.CuentaIvaCredito, e.CuentaIsrRetenido, e.CuentaIvaRetenidoClientes, activo);
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await client.PutAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ISR retenido a no residentes", await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_ConLasCuatroCuentasCambiarias_Persiste()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var usuario = await _pg.Data.CrearUsuarioAsync(Roles.Administrador);
        var client = _pg.CreateApiClient(usuario, Roles.Administrador);
        var gananciaRealizada = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var perdidaRealizada = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        var gananciaReval = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var perdidaReval = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        try
        {
            var response = await client.PutAsJsonAsync(
                Ruta, CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, gananciaRealizada, perdidaRealizada, gananciaReval, perdidaReval));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await client.GetFromJsonAsync<JsonElement>(Ruta);
            Assert.Equal(
                (gananciaRealizada, perdidaRealizada, gananciaReval, perdidaReval),
                (body.GetProperty("cuentaDifGananciaRealizadaId").GetInt32(), body.GetProperty("cuentaDifPerdidaRealizadaId").GetInt32(),
                    body.GetProperty("cuentaRevalGananciaId").GetInt32(), body.GetProperty("cuentaRevalPerdidaId").GetInt32()));
            Assert.Contains("cuenta ganancia cambiaria realizada", await _pg.Data.DetalleAuditoriaAsync(usuario, "actualizar_configuracion_fiscal"));
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Theory]
    [InlineData(0, "La cuenta de ganancia cambiaria realizada debe ser de tipo Ingreso y naturaleza Acreedora.")]
    [InlineData(1, "La cuenta de pérdida cambiaria realizada debe ser de tipo Gasto y naturaleza Deudora.")]
    [InlineData(2, "La cuenta de ganancia por revaluación debe ser de tipo Ingreso y naturaleza Acreedora.")]
    [InlineData(3, "La cuenta de pérdida por revaluación debe ser de tipo Gasto y naturaleza Deudora.")]
    public async Task Actualizar_CuentaCambiariaConTipoONaturalezaIncorrecta_Responde400(int ranura, string mensaje)
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var incorrecta = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var cuentas = new int?[4];
        cuentas[ranura] = incorrecta;
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await client.PutAsJsonAsync(
            Ruta, CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, cuentas[0], cuentas[1], cuentas[2], cuentas[3]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(mensaje, await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_CuentaCambiariaDeMayorOInactiva_Responde400()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var (mayor, _) = await _pg.Data.CrearCuentaConSubcuentasAsync("Ingreso", "Acreedora");
        var inactiva = await _pg.Data.CrearCuentaInactivaAsync("Ingreso", "Acreedora");
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var deMayor = await client.PutAsJsonAsync(Ruta, CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, mayor, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, deMayor.StatusCode);
        Assert.Contains("son de mayor", await deMayor.LeerErrorAsync());

        var desactivada = await client.PutAsJsonAsync(Ruta, CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, inactiva, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, desactivada.StatusCode);
        Assert.Contains("están inactivas", await desactivada.LeerErrorAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Actualizar_CuentasCambiariasDuplicadasOIgualesALaDeIva_Responde400(int caso)
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var gananciaRealizada = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var cuerpo = caso == 0
            ? CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, gananciaRealizada, null, gananciaRealizada, null)
            : CuerpoConCambiarias(e.CuentaIvaDebito, e.CuentaIvaCredito, e.CuentaIvaDebito, null, null, null);
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);

        var response = await client.PutAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "Las cuentas de diferencial cambiario y de revaluación deben ser distintas entre sí y de las cuentas de IVA y de retención.",
            await response.LeerErrorAsync());
    }

    [Fact]
    public async Task Actualizar_OmitiendoLasCuentasCambiarias_LasConserva()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Administrador);
        var gananciaRealizada = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var perdidaRealizada = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        var gananciaReval = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var perdidaReval = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        try
        {
            await _pg.Data.ConfigurarCuentasCambiariasAsync(gananciaRealizada, perdidaRealizada, gananciaReval, perdidaReval);

            var response = await client.PutAsJsonAsync(Ruta, Cuerpo(e.CuentaIvaDebito, e.CuentaIvaCredito));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.LeerJsonAsync();
            Assert.Equal(
                (gananciaRealizada, perdidaRealizada, gananciaReval, perdidaReval),
                (body.GetProperty("cuentaDifGananciaRealizadaId").GetInt32(), body.GetProperty("cuentaDifPerdidaRealizadaId").GetInt32(),
                    body.GetProperty("cuentaRevalGananciaId").GetInt32(), body.GetProperty("cuentaRevalPerdidaId").GetInt32()));
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    private static object CuerpoConCambiarias(
        int debitoId, int creditoId, int? gananciaRealizadaId, int? perdidaRealizadaId, int? gananciaRevalId, int? perdidaRevalId) => new
    {
        nitEmpresa = "7654321-8",
        nombreLegal = "Delta Actualizada, S.A.",
        regimenIsr = "UTILIDADES",
        agenteRetencionIva = false,
        tipoAgenteIva = (string?)null,
        cuentaIvaDebitoId = debitoId,
        cuentaIvaCreditoId = creditoId,
        cuentaDifGananciaRealizadaId = gananciaRealizadaId,
        cuentaDifPerdidaRealizadaId = perdidaRealizadaId,
        cuentaRevalGananciaId = gananciaRevalId,
        cuentaRevalPerdidaId = perdidaRevalId,
    };

    private static object CuerpoConRetenciones(int debitoId, int creditoId, int? isrId, int? ivaClientesId, int? isrNrId = null) => new
    {
        nitEmpresa = "7654321-8",
        nombreLegal = "Delta Actualizada, S.A.",
        regimenIsr = "UTILIDADES",
        agenteRetencionIva = false,
        tipoAgenteIva = (string?)null,
        cuentaIvaDebitoId = debitoId,
        cuentaIvaCreditoId = creditoId,
        cuentaIsrRetenidoPagarId = isrId,
        cuentaIvaRetenidoClientesId = ivaClientesId,
        cuentaIsrNrRetenidoPagarId = isrNrId,
    };
}
