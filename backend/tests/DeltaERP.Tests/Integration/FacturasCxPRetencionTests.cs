using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FacturasCxPRetencionTests
{
    private const int IdInexistente = 2_000_000_000;

    private readonly PostgresFixture _pg;

    public FacturasCxPRetencionTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static decimal D(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private static (string Estado, decimal Monto) Estado(JsonElement cuerpo)
    {
        var retencion = cuerpo.GetProperty("retencionIsr");
        return (retencion.GetProperty("estado").GetString() ?? string.Empty, retencion.GetProperty("monto").GetDecimal());
    }

    private static string[] Advertencias(JsonElement cuerpo) =>
        cuerpo.GetProperty("advertencias").EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToArray();

    private Task<long> ContarRetencionesAsync(int facturaId) =>
        _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM retencion WHERE documento_cxp_id = $1", facturaId);

    private Task<string> DetalleAuditoriaAsync(int usuarioId, string accion) =>
        _pg.Data.ScalarAsync<string>("SELECT detalle FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = $2 ORDER BY id DESC LIMIT 1", usuarioId, accion);

    private static void AssertDetalle(string detalle, params string[] fragmentos) =>
        Assert.All(fragmentos, f => Assert.Contains(f, detalle));

    private async Task<T> ConConfiguracionAsync<T>(Func<Task> alterar, Func<Task<T>> accion)
    {
        try
        {
            await alterar();
            return await accion();
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
            await _pg.Data.EjecutarAsync("UPDATE parametroretencion SET activo = TRUE WHERE codigo = 'ISR_PROVEEDOR_ROS'");
        }
    }

    [Fact]
    public async Task CrearFactura_DeProveedorSimplificado_RetieneIsrYDejaElSaldoNeto()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");

        var (facturaId, retencionId, cuerpo) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        var retencion = cuerpo.GetProperty("retencionIsr");
        Assert.Equal(("Retenida", 10000m, 500m, 10700m), (
            retencion.GetProperty("estado").GetString(),
            retencion.GetProperty("base").GetDecimal(),
            retencion.GetProperty("monto").GetDecimal(),
            retencion.GetProperty("neto").GetDecimal()));
        Assert.Equal((500m, 10700m, "NINGUNA"), (
            cuerpo.GetProperty("montoRetencionIsr").GetDecimal(),
            cuerpo.GetProperty("saldoPendiente").GetDecimal(),
            cuerpo.GetProperty("revisionRetencion").GetString()));
        Assert.Empty(Advertencias(cuerpo));
        var lineas = await _pg.Data.LineasDeAsientoAsync(cuerpo.GetProperty("asientoId").GetInt32());
        Assert.Equal(
            new[] { (e.CuentaCxP, 0m, 10700m), (e.CuentaGasto, 10000m, 0m), (e.CuentaIvaCredito, 1200m, 0m), (e.CuentaIsrRetenido, 0m, 500m) },
            lineas);
        Assert.NotNull(retencionId);
        Assert.Equal(retencionId.Value, await _pg.Data.ScalarAsync<int>("SELECT id FROM retencion WHERE documento_cxp_id = $1", facturaId));
        Assert.Equal((500m, 500m, 5m, 10000m), (
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_calculado FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<decimal>("SELECT tasa FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<decimal>("SELECT base FROM retencion WHERE id = $1", retencionId.Value)));
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "registrar_factura_cxp"));
    }

    [Theory]
    [InlineData("2799.99", "0", "NoAplica")]
    [InlineData("2800.00", "125.00", "Retenida")]
    [InlineData("33600.00", "1500.00", "Retenida")]
    [InlineData("33712.00", "1507.00", "Retenida")]
    [InlineData("44800.00", "2200.00", "Retenida")]
    [InlineData("112000.00", "6400.00", "Retenida")]
    public async Task CrearFactura_AplicaLaEscalaYElUmbralSobreLaBaseSinIva(string total, string retencionEsperada, string estadoEsperado)
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");

        var (facturaId, retencionId, cuerpo) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, D(total)));

        Assert.Equal((estadoEsperado, D(retencionEsperada)), Estado(cuerpo));
        Assert.Equal(D(total) - D(retencionEsperada), cuerpo.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal(estadoEsperado == "Retenida", retencionId is not null);
        Assert.Equal(estadoEsperado == "Retenida" ? 1L : 0L, await ContarRetencionesAsync(facturaId));
    }

    [Theory]
    [InlineData("UTILIDADES", null, "utilidades")]
    [InlineData("PAGO_DIRECTO", "Resolución SAT-123", "Resolución SAT-123")]
    [InlineData("EXENTO", "Art. 8 LISR", "Art. 8 LISR")]
    public async Task CrearFactura_DeProveedorSinRetencionPorRegimen_NoRetieneNiMarcaRevision(string regimen, string? fundamento, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync(regimen, fundamento);

        var (facturaId, _, cuerpo) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        Assert.Equal(("NoAplica", 0m), Estado(cuerpo));
        Assert.Contains(fragmento, cuerpo.GetProperty("retencionIsr").GetProperty("motivo").GetString());
        Assert.Equal(("NINGUNA", 11200m), (cuerpo.GetProperty("revisionRetencion").GetString(), cuerpo.GetProperty("saldoPendiente").GetDecimal()));
        Assert.Empty(Advertencias(cuerpo));
        Assert.Equal(0L, await ContarRetencionesAsync(facturaId));
    }

    [Fact]
    public async Task CrearFactura_DeNoResidenteConTipoDeRenta_DifiereLaRetencionAlPago()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorNoResidenteAsync("NR_REGALIAS");

        var (_, _, cuerpo) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        Assert.Equal(("NoAplica", 0m), Estado(cuerpo));
        Assert.Contains("se retiene al registrar el pago", cuerpo.GetProperty("retencionIsr").GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task CrearFactura_DePequenoContribuyente_NoRetieneIsr()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO", regimenIva: "PEQUENO_CONTRIBUYENTE");

        var (_, _, cuerpo) = await client.RegistrarCompraAsync(CompraDePequenoContribuyente(e, proveedor, 5000m));

        Assert.Equal(("NoAplica", 0m), Estado(cuerpo));
        Assert.Contains("pequeño contribuyente", cuerpo.GetProperty("retencionIsr").GetProperty("motivo").GetString());
    }

    [Theory]
    [InlineData("NO_DEFINIDO", true, "no tiene régimen de ISR definido")]
    [InlineData("SIMPLIFICADO", false, "es no residente")]
    public async Task CrearFactura_DeTerceroSinDatosDeRetencion_AdviertePeroNoBloquea(string regimen, bool residente, string fragmento)
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync(regimen, esResidente: residente);

        var (facturaId, _, cuerpo) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        Assert.Equal(("PendienteRevision", 0m), Estado(cuerpo));
        var advertencia = Assert.Single(Advertencias(cuerpo));
        Assert.Contains(fragmento, advertencia);
        Assert.Contains("marcada para revisión", advertencia);
        Assert.Equal(("PENDIENTE", 11200m), (cuerpo.GetProperty("revisionRetencion").GetString(), cuerpo.GetProperty("saldoPendiente").GetDecimal()));
        Assert.Equal("PENDIENTE", await _pg.Data.ScalarAsync<string>("SELECT revision_retencion FROM documentocxp WHERE id = $1", facturaId));
        Assert.Equal(0L, await ContarRetencionesAsync(facturaId));
    }

    [Fact]
    public async Task CrearFactura_ConRevisionEnviadaPorElCliente_LaIgnora()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("NO_DEFINIDO");
        var payload = CompraConIva(e, proveedor, 11200m).Con("revisionRetencion", "RESUELTA").Con("revisionMotivo", "forzado");

        var (facturaId, _, cuerpo) = await client.RegistrarCompraAsync(payload);

        Assert.Equal("PENDIENTE", cuerpo.GetProperty("revisionRetencion").GetString());
        Assert.Equal(1L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE id = $1 AND revision_motivo IS NULL", facturaId));
    }

    [Fact]
    public async Task ResolverRevision_ExigeMotivoYSoloProcedeUnaVez()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("NO_DEFINIDO");
        var (facturaId, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        var ruta = $"/api/cxp/facturas/{facturaId}/revision-retencion";

        var sinMotivo = await client.PostAsJsonAsync(ruta, new { motivo = " " });
        var resuelta = await client.PostAsJsonAsync(ruta, new { motivo = "Régimen confirmado: utilidades" });
        var repetida = await client.PostAsJsonAsync(ruta, new { motivo = "Otra vez" });
        var inexistente = await client.PostAsJsonAsync($"/api/cxp/facturas/{IdInexistente}/revision-retencion", new { motivo = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resuelta.StatusCode);
        var cuerpo = await resuelta.LeerJsonAsync();
        Assert.Equal(("RESUELTA", "Régimen confirmado: utilidades"), (cuerpo.GetProperty("revisionRetencion").GetString(), cuerpo.GetProperty("revisionMotivo").GetString()));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(1, await _pg.Data.ContarAuditoriaAsync(e.UsuarioId, "resolver_revision_retencion"));
        AssertDetalle(await DetalleAuditoriaAsync(e.UsuarioId, "resolver_revision_retencion"), "Régimen confirmado: utilidades", "calculado Q0", "aplicado Q0");
    }

    [Fact]
    public async Task ResolverRevision_DeFacturaSinRevisionPendiente_Responde409()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("UTILIDADES");
        var (facturaId, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        var response = await client.PostAsJsonAsync($"/api/cxp/facturas/{facturaId}/revision-retencion", new { motivo = "x" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CrearFactura_ConAjusteDeMontoYMotivo_RegistraLaRetencionAjustada()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var payload = CompraConIva(e, proveedor, 11200m).Con("retencionIsrAjuste", Ajuste(300m, "Acuerdo con el proveedor"));

        var (facturaId, retencionId, cuerpo) = await client.RegistrarCompraAsync(payload);

        Assert.Equal(("Retenida", 300m), Estado(cuerpo));
        Assert.Equal(10900m, cuerpo.GetProperty("saldoPendiente").GetDecimal());
        Assert.NotNull(retencionId);
        Assert.Equal((500m, 300m, "Acuerdo con el proveedor"), (
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_calculado FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<string>("SELECT motivo_ajuste FROM retencion WHERE id = $1", retencionId.Value)));
        Assert.Equal(300m, await _pg.Data.CreditoAsync(cuerpo.GetProperty("asientoId").GetInt32(), e.CuentaIsrRetenido));
        Assert.Equal(10900m, await _pg.Data.CreditoAsync(cuerpo.GetProperty("asientoId").GetInt32(), e.CuentaCxP));
        Assert.Equal(1L, await ContarRetencionesAsync(facturaId));
        AssertDetalle(await DetalleAuditoriaAsync(e.UsuarioId, "registrar_factura_cxp"), "calculado Q500", "aplicado Q300", "motivo: Acuerdo con el proveedor");
    }

    [Fact]
    public async Task CrearFactura_ConAjusteEnCero_RegistraSinRetencionPorExcepcion()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var payload = CompraConIva(e, proveedor, 11200m).Con("retencionIsrAjuste", Ajuste(0m, "Proveedor presentó resolución de pago directo"));

        var (facturaId, retencionId, cuerpo) = await client.RegistrarCompraAsync(payload);

        Assert.Equal(("Retenida", 0m), Estado(cuerpo));
        Assert.Null(retencionId);
        Assert.Equal(11200m, cuerpo.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal(0m, await _pg.Data.CreditoAsync(cuerpo.GetProperty("asientoId").GetInt32(), e.CuentaIsrRetenido));
        Assert.Equal(0L, await ContarRetencionesAsync(facturaId));
        AssertDetalle(await DetalleAuditoriaAsync(e.UsuarioId, "registrar_factura_cxp"), "calculado Q500", "aplicado Q0", "motivo: Proveedor presentó resolución de pago directo");
    }

    [Fact]
    public async Task CrearFactura_ConAjusteEnRevisionPendiente_ResuelveLaRevisionYRetiene()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("NO_DEFINIDO");
        var payload = CompraConIva(e, proveedor, 11200m).Con("retencionIsrAjuste", Ajuste(250m, "Contador confirma régimen simplificado"));

        var (facturaId, retencionId, cuerpo) = await client.RegistrarCompraAsync(payload);

        Assert.Equal(("PendienteRevision", 250m), Estado(cuerpo));
        Assert.Equal(("RESUELTA", "Contador confirma régimen simplificado"), (
            cuerpo.GetProperty("revisionRetencion").GetString(), cuerpo.GetProperty("revisionMotivo").GetString()));
        Assert.Empty(Advertencias(cuerpo));
        Assert.NotNull(retencionId);
        Assert.Equal((0m, 250m), (
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_calculado FROM retencion WHERE id = $1", retencionId.Value),
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_retenido FROM retencion WHERE id = $1", retencionId.Value)));
        Assert.Equal(10950m, cuerpo.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal(1L, await ContarRetencionesAsync(facturaId));
        AssertDetalle(await DetalleAuditoriaAsync(e.UsuarioId, "registrar_factura_cxp"), "calculado Q0", "aplicado Q250", "motivo: Contador confirma régimen simplificado");
    }

    [Theory]
    [InlineData("SIMPLIFICADO", "300", null, HttpStatusCode.BadRequest)]
    [InlineData("SIMPLIFICADO", "-1", "Motivo", HttpStatusCode.BadRequest)]
    [InlineData("SIMPLIFICADO", "11200", "Motivo", HttpStatusCode.BadRequest)]
    [InlineData("UTILIDADES", "0", "Motivo", HttpStatusCode.BadRequest)]
    [InlineData("UTILIDADES", "100", "Motivo", HttpStatusCode.Conflict)]
    public async Task CrearFactura_ConAjusteNoPermitido_RespondeSinRegistrarNada(string regimen, string monto, string? motivo, HttpStatusCode esperado)
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync(regimen);
        var payload = CompraConIva(e, proveedor, 11200m).Con("retencionIsrAjuste", Ajuste(D(monto), motivo));

        var (estado, _) = await client.EnviarAsync(RutaFacturasCxP, payload);

        Assert.Equal(esperado, estado);
        Assert.Equal(0L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", NumeroDe(payload)));
    }

    [Fact]
    public async Task CrearFactura_SinCuentaDeIsrConfigurada_Responde409SinRegistrarNada()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var payload = CompraConIva(e, proveedor, 11200m);

        var (estado, cuerpo) = await ConConfiguracionAsync(
            () => _pg.Data.ConfigurarCuentasRetencionAsync(null, null),
            () => client.EnviarAsync(RutaFacturasCxP, payload));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.StartsWith("Falta configurar la cuenta de ISR retenido por pagar", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE numero = $1", NumeroDe(payload)));
    }

    [Fact]
    public async Task CrearFactura_SinParametroVigente_Responde409YNoOmiteLaRetencionEnSilencio()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");

        var (estado, cuerpo) = await ConConfiguracionAsync(
            () => _pg.Data.EjecutarAsync("UPDATE parametroretencion SET activo = FALSE WHERE codigo = 'ISR_PROVEEDOR_ROS'"),
            () => client.EnviarAsync(RutaFacturasCxP, CompraConIva(e, proveedor, 11200m)));

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("No hay un parámetro de retención de ISR vigente", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearFactura_ConCuentaDeControlIgualALaDeIsr_Responde400()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");

        var (estado, cuerpo) = await client.EnviarAsync(RutaFacturasCxP, CompraConIva(e with { CuentaCxP = e.CuentaIsrRetenido }, proveedor, 11200m));

        Assert.Equal(HttpStatusCode.BadRequest, estado);
        Assert.Contains("no puede ser la cuenta de ISR retenido por pagar", cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CrearFactura_ConEmpresaAgenteDeIva_AdvierteQueNoSeCalculo()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("UTILIDADES");

        var (_, _, cuerpo) = await ConConfiguracionAsync(
            _pg.Data.MarcarEmpresaComoAgenteIvaAsync,
            () => client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m)));

        Assert.Contains(RetencionService.AvisoAgenteIva, Advertencias(cuerpo));
    }

    [Fact]
    public async Task Pago_PosteriorAFacturaRetenida_SeLimitaAlSaldoNeto()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var banco = await _pg.Data.CrearCuentaBancariaAsync();
        var (facturaId, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));

        var excedido = await client.EnviarAsync(RutaPagosCxP, PagoCxP(proveedor, banco.Id, facturaId, 10700.01m));
        var exacto = await client.EnviarAsync(RutaPagosCxP, PagoCxP(proveedor, banco.Id, facturaId, 10700m));

        Assert.Equal(HttpStatusCode.BadRequest, excedido.Estado);
        Assert.Contains("RN-05", excedido.Cuerpo.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, exacto.Estado);
        var detalle = await client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxP}/{facturaId}");
        Assert.Equal(0m, detalle.GetProperty("saldoPendiente").GetDecimal());
        var asientoPago = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", exacto.Cuerpo.GetProperty("id").GetInt32());
        Assert.Equal((10700m, 10700m), (await _pg.Data.DebitoAsync(asientoPago, e.CuentaCxP), await _pg.Data.CreditoAsync(asientoPago, banco.CuentaContableId)));
    }

    [Fact]
    public async Task NotaDeCredito_SobreFacturaRetenida_SeLimitaAlSaldoNetoYAdvierte()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var (facturaId, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        Dictionary<string, object?> Nota(decimal total) =>
            CompraConIva(e, proveedor, total).Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", facturaId);

        var bruta = await client.EnviarAsync(RutaFacturasCxP, Nota(11200m));
        var neta = await client.EnviarAsync(RutaFacturasCxP, Nota(10700m));

        Assert.Equal(HttpStatusCode.Conflict, bruta.Estado);
        Assert.Contains("excede el saldo pendiente del documento de origen (Q10700.00)", bruta.Cuerpo.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Created, neta.Estado);
        Assert.Contains("La factura de origen tiene retención de ISR registrada (Q500.00)", Advertencias(neta.Cuerpo).Single());
        Assert.Equal(("NoAplica", 0m), Estado(neta.Cuerpo));
        Assert.Equal(0L, await ContarRetencionesAsync(neta.Cuerpo.GetProperty("id").GetInt32()));
        var origen = await client.GetFromJsonAsync<JsonElement>($"{RutaFacturasCxP}/{facturaId}");
        Assert.Equal(0m, origen.GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task ListarFacturas_IncluyeRetencionYRevision()
    {
        var (e, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var pendiente = await _pg.Data.CrearProveedorIsrAsync("NO_DEFINIDO");
        var (retenida, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, proveedor, 11200m));
        var (porRevisar, _, _) = await client.RegistrarCompraAsync(CompraConIva(e, pendiente, 11200m));

        var lista = await client.GetFromJsonAsync<JsonElement>(RutaFacturasCxP);

        var filas = lista.EnumerateArray().ToDictionary(f => f.GetProperty("id").GetInt32());
        Assert.Equal((500m, "NINGUNA", 10700m), (
            filas[retenida].GetProperty("montoRetencionIsr").GetDecimal(),
            filas[retenida].GetProperty("revisionRetencion").GetString(),
            filas[retenida].GetProperty("saldoPendiente").GetDecimal()));
        Assert.Equal((0m, "PENDIENTE"), (filas[porRevisar].GetProperty("montoRetencionIsr").GetDecimal(), filas[porRevisar].GetProperty("revisionRetencion").GetString()));
    }

    [Fact]
    public async Task RetencionPrevia_CalculaLaEscalaSinEscribirNada()
    {
        var (_, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");

        var (estado, cuerpo) = await client.EnviarAsync($"{RutaFacturasCxP}/retencion-previa", new { proveedorId = proveedor, fecha = "2025-03-15", montoBase = 40000m, montoTotal = 44800m });

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(("Retenida", 2200m, 42600m, 5m, 30000m, 7m, 2500m, true), (
            cuerpo.GetProperty("estado").GetString(),
            cuerpo.GetProperty("retencion").GetDecimal(),
            cuerpo.GetProperty("neto").GetDecimal(),
            cuerpo.GetProperty("tasaBase").GetDecimal(),
            cuerpo.GetProperty("tramoLimite").GetDecimal(),
            cuerpo.GetProperty("tasaExcedente").GetDecimal(),
            cuerpo.GetProperty("umbralMinimo").GetDecimal(),
            cuerpo.GetProperty("referencial").GetBoolean()));
        Assert.NotEqual(JsonValueKind.Null, cuerpo.GetProperty("parametroId").ValueKind);
        Assert.Equal(0L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM documentocxp WHERE proveedor_id = $1", proveedor));
    }

    [Fact]
    public async Task RetencionPrevia_DeTerceroSinRegimen_DevuelveRevisionPendiente()
    {
        var (_, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("NO_DEFINIDO");

        var (estado, cuerpo) = await client.EnviarAsync($"{RutaFacturasCxP}/retencion-previa", new { proveedorId = proveedor, fecha = "2025-03-15", montoBase = 40000m, montoTotal = 44800m });

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(("PendienteRevision", 0m, 44800m), (cuerpo.GetProperty("estado").GetString(), cuerpo.GetProperty("retencion").GetDecimal(), cuerpo.GetProperty("neto").GetDecimal()));
        Assert.Single(cuerpo.GetProperty("advertencias").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("tasaBase").ValueKind);
    }

    [Fact]
    public async Task RetencionPrevia_ConEmpresaAgenteDeIva_IncluyeElAviso()
    {
        var (_, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("UTILIDADES");

        var (_, cuerpo) = await ConConfiguracionAsync(
            _pg.Data.MarcarEmpresaComoAgenteIvaAsync,
            () => client.EnviarAsync($"{RutaFacturasCxP}/retencion-previa", new { proveedorId = proveedor, fecha = "2025-03-15", montoBase = 100m, montoTotal = 112m }));

        Assert.Contains(RetencionService.AvisoAgenteIva, Advertencias(cuerpo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RetencionPrevia_ConDatosInvalidosOSinConfiguracion_Responde400O409(int caso)
    {
        var (_, client) = await _pg.PrepararAsync();
        var proveedor = await _pg.Data.CrearProveedorIsrAsync("SIMPLIFICADO");
        var cuerpo = caso switch
        {
            0 => new { proveedorId = IdInexistente, fecha = "2025-03-15", montoBase = 100m, montoTotal = 112m },
            1 => new { proveedorId = proveedor, fecha = "2025-03-15", montoBase = 200m, montoTotal = 112m },
            _ => new { proveedorId = proveedor, fecha = "2025-03-15", montoBase = 40000m, montoTotal = 44800m },
        };
        var ruta = $"{RutaFacturasCxP}/retencion-previa";

        var (estado, _) = caso switch
        {
            2 => await ConConfiguracionAsync(() => _pg.Data.ConfigurarCuentasRetencionAsync(null, null), () => client.EnviarAsync(ruta, cuerpo)),
            3 => await ConConfiguracionAsync(
                () => _pg.Data.EjecutarAsync("UPDATE parametroretencion SET activo = FALSE WHERE codigo = 'ISR_PROVEEDOR_ROS'"),
                () => client.EnviarAsync(ruta, cuerpo)),
            _ => await client.EnviarAsync(ruta, cuerpo),
        };

        Assert.Equal(caso < 2 ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, estado);
    }

    [Fact]
    public async Task EscrituraDeRetencionesEnCxP_ComoVendedor_Responde403()
    {
        var e = await _pg.Data.SembrarEscenarioAsync();
        var client = _pg.CreateApiClient(e.UsuarioId, Roles.Vendedor);

        var previa = await client.PostAsJsonAsync($"{RutaFacturasCxP}/retencion-previa", new { proveedorId = e.ProveedorId, fecha = "2025-03-15", montoBase = 100m, montoTotal = 112m });
        var revision = await client.PostAsJsonAsync($"{RutaFacturasCxP}/{IdInexistente}/revision-retencion", new { motivo = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, previa.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revision.StatusCode);
    }
}
