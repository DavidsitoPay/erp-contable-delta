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
}
