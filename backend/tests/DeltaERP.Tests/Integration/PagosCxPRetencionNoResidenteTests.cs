using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class PagosCxPRetencionNoResidenteTests
{
    private const string RutaPrevia = "/api/cxp/pagos/retencion-previa";

    private readonly PostgresFixture _pg;

    public PagosCxPRetencionNoResidenteTests(PostgresFixture pg)
    {
        _pg = pg;
        _pg.Banguat.Reiniciar();
    }

    private static decimal Dec(JsonElement e, string propiedad) => e.GetProperty(propiedad).GetDecimal();

    private Task<Compra> CompraAsync(
        string moneda = "USD", string? tipoRenta = "NR_REGALIAS", string regimen = "SIMPLIFICADO", string? fundamento = null, decimal precio = 1000m) =>
        _pg.CompraNrAsync(moneda, tipoRenta, regimen, fundamento, precio);

    private static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> Pagar(
        Compra c, int cuentaId, DateOnly fecha, decimal monto, decimal? efectivo = null, string? tipoRenta = null) =>
        c.Client.EnviarAsync(RutaPagosCxP, PagoProveedor(c.ProveedorId, cuentaId, fecha.Dia(), AplicacionNr(c.FacturaId, monto, efectivo, tipoRenta)));

    private static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> Previa(
        Compra c, int cuentaId, DateOnly fecha, decimal monto, decimal? efectivo = null, string? tipoRenta = null) =>
        c.Client.EnviarAsync(RutaPrevia, new
        {
            proveedorId = c.ProveedorId,
            fecha = fecha.Dia(),
            cuentaBancariaId = cuentaId,
            aplicaciones = new[] { AplicacionNr(c.FacturaId, monto, efectivo, tipoRenta) },
        });

    private Task<T> Fila<T>(int retencionId, string columna) =>
        _pg.Data.ScalarAsync<T>($"SELECT {columna} FROM retencion WHERE id = $1", retencionId);

    private Task<long> ContarAsync(int facturaId) =>
        _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM retencion WHERE documento_cxp_id = $1", facturaId);

    private async Task<int> AsientoDePagoAsync(JsonElement pago) =>
        await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE pago_proveedor_id = $1", pago.GetProperty("id").GetInt32());

    [Fact]
    public async Task Pago_UsdACuentaUsd_RetieneConElTcDelDiaDelPagoYCuadraElAsiento()
    {
        var c = await CompraAsync();
        var cuentaNr = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIsrNrRetenido);

        var (estado, cuerpo) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);

        Assert.Equal(HttpStatusCode.Created, estado);
        var retencion = Assert.Single(cuerpo.GetProperty("retenciones").EnumerateArray());
        Assert.Equal((c.FacturaId, 462m), (retencion.GetProperty("documentoId").GetInt32(), Dec(retencion, "monto")));
        var id = retencion.GetProperty("retencionId").GetInt32();
        Assert.Equal(
            ("ISR_NORES", 3080m, 15m, 462m, 60m, 400m, 7.7m, c.M.Jueves.ToDateTime(TimeOnly.MinValue), "NR_REGALIAS"),
            (await Fila<string>(id, "concepto"), await Fila<decimal>(id, "base"), await Fila<decimal>(id, "tasa"), await Fila<decimal>(id, "monto_retenido"),
                await Fila<decimal>(id, "monto_retenido_original"), await Fila<decimal>(id, "monto_origen"), await Fila<decimal>(id, "tipo_cambio"),
                await Fila<DateTime>(id, "tipo_cambio_fecha"), await Fila<string>(id, "tipo_renta_codigo")));
        var asientoId = await AsientoDePagoAsync(cuerpo);
        Assert.Equal((2618m, 462m, 3040m), (
            await _pg.Data.CreditoAsync(asientoId, c.M.BancoUsdContableId), await _pg.Data.CreditoAsync(asientoId, cuentaNr),
            await _pg.Data.DebitoAsync(asientoId, c.M.E.CuentaCxP)));
        Assert.True(await _pg.Data.AsientoCuadraAsync(asientoId));
        Assert.Equal((340m, 2618m), (
            await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE asiento_id = $1", asientoId),
            await _pg.Data.ScalarAsync<decimal>("SELECT monto_gtq FROM movimientotesoreria WHERE asiento_id = $1", asientoId)));
        Assert.Contains("ISR no residente Q462", await _pg.Data.DetalleAuditoriaAsync(c.M.E.UsuarioId, "registrar_pago_cxp"));
    }

    [Fact]
    public async Task Pago_UsdACuentaGtq_BaseConTcDelDiaYBancoPorElEfectivoMenosLaRetencion()
    {
        var c = await CompraAsync();

        var (estado, cuerpo) = await Pagar(c, c.M.BancoGtqId, c.M.Jueves, 400m, efectivo: 3100m);

        Assert.Equal(HttpStatusCode.Created, estado);
        var asientoId = await AsientoDePagoAsync(cuerpo);
        Assert.Equal((2638m, 462m), (
            await _pg.Data.CreditoAsync(asientoId, c.M.BancoGtqContableId),
            await _pg.Data.CreditoAsync(asientoId, await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIsrNrRetenido))));
        Assert.True(await _pg.Data.AsientoCuadraAsync(asientoId));
        Assert.Equal(3080m, await Fila<decimal>(cuerpo.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32(), "base"));
    }

    [Fact]
    public async Task Pago_FacturaGtq_UsaTcUnoYFechaDelPago()
    {
        var c = await CompraAsync("GTQ");

        var (estado, cuerpo) = await Pagar(c, c.M.BancoGtqId, c.M.Jueves, 1000m);

        Assert.Equal(HttpStatusCode.Created, estado);
        var id = cuerpo.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32();
        Assert.Equal((1000m, 150m, 1m, c.M.Jueves.ToDateTime(TimeOnly.MinValue)), (
            await Fila<decimal>(id, "base"), await Fila<decimal>(id, "monto_retenido"), await Fila<decimal>(id, "tipo_cambio"), await Fila<DateTime>(id, "tipo_cambio_fecha")));
        Assert.Equal(850m, await _pg.Data.ScalarAsync<decimal>("SELECT monto FROM movimientotesoreria WHERE pago_proveedor_id = $1", cuerpo.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Pago_ConNotaDeCreditoPrevia_RetieneSobreLaBaseNeta()
    {
        var c = await CompraAsync("GTQ");
        var nota = FacturaPayloads.CxpEn(c.M, "GTQ", c.M.Miercoles, 200m, c.ProveedorId)
            .Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", c.FacturaId);
        var (estadoNota, _) = await c.Client.EnviarAsync(RutaFacturasCxP, nota);
        Assert.Equal(HttpStatusCode.Created, estadoNota);

        var (estado, cuerpo) = await Pagar(c, c.M.BancoGtqId, c.M.Jueves, 800m);

        Assert.Equal(HttpStatusCode.Created, estado);
        var id = cuerpo.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32();
        Assert.Equal((800m, 120m), (await Fila<decimal>(id, "base"), await Fila<decimal>(id, "monto_retenido")));
    }

    [Fact]
    public async Task Pago_ParcialesDeLaMismaFactura_RetienenCadaUnoConSuTcDelDia()
    {
        var c = await CompraAsync();
        await _pg.Data.SembrarTipoCambioAsync("USD", c.M.Viernes, 7.80m);

        var primero = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);
        var segundo = await Pagar(c, c.M.BancoUsdId, c.M.Viernes, 600m);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (primero.Estado, segundo.Estado));
        Assert.Equal((2L, 1164m), (
            await ContarAsync(c.FacturaId),
            await _pg.Data.ScalarAsync<decimal>("SELECT SUM(monto_retenido) FROM retencion WHERE documento_cxp_id = $1", c.FacturaId)));
    }

    [Fact]
    public async Task Pago_ConTipoDeRentaDistintoDelProveedor_GuardaElElegido()
    {
        var c = await CompraAsync();

        var (estado, cuerpo) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m, tipoRenta: "nr_intereses");

        Assert.Equal(HttpStatusCode.Created, estado);
        var id = cuerpo.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32();
        Assert.Equal(("NR_INTERESES", 308m), (await Fila<string>(id, "tipo_renta_codigo"), await Fila<decimal>(id, "monto_retenido")));
    }

    [Fact]
    public async Task Pago_SinTipoDeRentaNiExencion_Responde409SinEscribirNada()
    {
        var c = await CompraAsync(tipoRenta: null);

        var (estado, cuerpo) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Contains("tipo de renta", cuerpo.GetProperty("error").GetString());
        Assert.Equal(0L, await ContarAsync(c.FacturaId));
    }

    [Fact]
    public async Task Pago_ConTipoDeRentaInexistenteOVencido_Responde400Y409()
    {
        var c = await CompraAsync();
        var vencido = await _pg.Data.SembrarParametroNrVencidoAsync();

        var inexistente = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m, tipoRenta: "NR_NO_EXISTE");
        var sinVigencia = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m, tipoRenta: vencido);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.Conflict), (inexistente.Estado, sinVigencia.Estado));
        Assert.Contains("vigente", sinVigencia.Cuerpo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Pago_SinCuentaIsrNr_Responde409()
    {
        var c = await CompraAsync();
        try
        {
            await _pg.Data.ConfigurarCuentaIsrNrAsync(null);

            var (estado, cuerpo) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);

            Assert.Equal(HttpStatusCode.Conflict, estado);
            Assert.Contains("ISR retenido a no residentes", cuerpo.GetProperty("error").GetString());
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }

    [Fact]
    public async Task Pago_ConBanguatCaidoYSinTcDelDia_Responde409()
    {
        var c = await CompraAsync();
        _pg.Banguat.Falla = true;

        var (estado, _) = await Pagar(c, c.M.BancoGtqId, c.M.Viernes, 400m, efectivo: 3000m);

        Assert.Equal(HttpStatusCode.Conflict, estado);
        Assert.Equal(0L, await ContarAsync(c.FacturaId));
    }

    [Fact]
    public async Task Reversar_ElAsientoDelPagoConRetencion_Responde409PorTesoreria()
    {
        var c = await CompraAsync();
        var (_, pago) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);
        var asientoId = await AsientoDePagoAsync(pago);
        var administrador = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(Roles.Administrador), Roles.Administrador);

        var respuesta = await administrador.PostAsJsonAsync($"/api/asientos/{asientoId}/reversar", new { motivo = "Prueba" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(1L, await ContarAsync(c.FacturaId));
    }

    [Fact]
    public async Task RetencionPrevia_CoincideConElPagoYNoEscribeFilas()
    {
        var c = await CompraAsync();

        var (estado, previa) = await Previa(c, c.M.BancoUsdId, c.M.Jueves, 400m);

        Assert.Equal(HttpStatusCode.OK, estado);
        var linea = Assert.Single(previa.GetProperty("lineas").EnumerateArray());
        Assert.Equal(
            (true, "NR_REGALIAS", 15m, "USD", 400m, 7.7m, 3080m, 462m),
            (previa.GetProperty("aplica").GetBoolean(), linea.GetProperty("tipoRentaCodigo").GetString(), Dec(linea, "tasa"),
                linea.GetProperty("monedaCodigo").GetString(), Dec(linea, "montoOrigen"), Dec(linea, "tipoCambio"), Dec(linea, "baseGtq"), Dec(linea, "retencionGtq")));
        Assert.Equal((3080m, 462m, 2618m), (Dec(previa, "totalBaseGtq"), Dec(previa, "totalRetencionGtq"), Dec(previa, "netoBancoGtq")));
        Assert.Equal(0L, await ContarAsync(c.FacturaId));
    }

    [Theory]
    [InlineData("SIMPLIFICADO", true)]
    [InlineData("EXENTO", false)]
    public async Task RetencionPrevia_ResidenteOExento_AplicaFalseConNetoIgualAlMonto(string regimen, bool residente)
    {
        var c = await CompraAsync("GTQ", "NR_REGALIAS", regimen, regimen == "EXENTO" ? "Art. 8" : null);
        if (residente)
        {
            await _pg.Data.EjecutarAsync("UPDATE contraparte SET es_residente = TRUE WHERE id = $1", c.ProveedorId);
        }

        var (estado, previa) = await Previa(c, c.M.BancoGtqId, c.M.Jueves, 1000m);

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal((false, 0m, JsonValueKind.Null), (previa.GetProperty("aplica").GetBoolean(), Dec(previa, "totalRetencionGtq"), previa.GetProperty("netoBancoGtq").ValueKind));
        Assert.Empty(previa.GetProperty("lineas").EnumerateArray());
    }

    [Fact]
    public async Task RetencionPrevia_ExentoEnUsd_NetoBancoGtqEsNull()
    {
        var c = await CompraAsync("USD", "NR_REGALIAS", "EXENTO", "Art. 8");

        var (estado, previa) = await Previa(c, c.M.BancoUsdId, c.M.Jueves, 400m);

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal((false, JsonValueKind.Null), (previa.GetProperty("aplica").GetBoolean(), previa.GetProperty("netoBancoGtq").ValueKind));
    }

    [Fact]
    public async Task RetencionPrevia_ProveedorInexistente_Responde400()
    {
        var c = await CompraAsync();

        var (estado, _) = await c.Client.EnviarAsync(RutaPrevia, new
        {
            proveedorId = 987654,
            fecha = c.M.Jueves.Dia(),
            cuentaBancariaId = c.M.BancoUsdId,
            aplicaciones = new[] { AplicacionNr(c.FacturaId, 400m) },
        });

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task RetencionPrevia_FacturaDeOtroProveedor_Responde400()
    {
        var c = await CompraAsync();
        var otroProveedor = await _pg.Data.CrearProveedorNoResidenteAsync("NR_REGALIAS");

        var (estado, _) = await c.Client.EnviarAsync(RutaPrevia, new
        {
            proveedorId = otroProveedor,
            fecha = c.M.Jueves.Dia(),
            cuentaBancariaId = c.M.BancoUsdId,
            aplicaciones = new[] { AplicacionNr(c.FacturaId, 400m) },
        });

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task RetencionPrevia_MontoMayorAlSaldo_Responde400()
    {
        var c = await CompraAsync();

        var (estado, _) = await Previa(c, c.M.BancoUsdId, c.M.Jueves, 5000m);

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task RetencionPrevia_CuentaGtqConEfectivoMenorQueLaRetencion_Responde400()
    {
        var c = await CompraAsync();

        var (estado, _) = await Previa(c, c.M.BancoGtqId, c.M.Jueves, 400m, efectivo: 100m);

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task RetencionPrevia_SoloNotaDeCredito_Responde400()
    {
        var c = await CompraAsync();
        var (estadoNota, nota) = await c.Client.EnviarAsync(RutaFacturasCxP,
            FacturaPayloads.CxpEn(c.M, "USD", c.M.Miercoles, 100m, c.ProveedorId).Con("tipoDocumento", "NotaCredito").Con("documentoOrigenId", c.FacturaId));
        Assert.Equal(HttpStatusCode.Created, estadoNota);

        var (estado, cuerpo) = await Previa(c with { FacturaId = nota.GetProperty("id").GetInt32() }, c.M.BancoUsdId, c.M.Jueves, 50m);

        Assert.Equal((HttpStatusCode.BadRequest, true), (estado, cuerpo.GetProperty("error").GetString()!.Contains("no admite pagos")));
    }

    [Fact]
    public async Task Reporte_IncluyeLaRetencionEnElMesDelPagoSinConstanciaYConTotalesPorTipoDeRenta()
    {
        var c = await CompraAsync();
        var (_, pago) = await Pagar(c, c.M.BancoUsdId, c.M.Jueves, 400m);
        var id = pago.GetProperty("retenciones")[0].GetProperty("retencionId").GetInt32();

        var reporte = await c.Client.GetFromJsonAsync<JsonElement>(
            $"/api/retenciones/reporte?anio={c.M.Jueves.Year}&mes={c.M.Jueves.Month}&concepto=ISR_NORES&estadoConstancia=NO_APLICA");

        var fila = reporte.GetProperty("filas").EnumerateArray().Single(f => f.GetProperty("retencionId").GetInt32() == id);
        Assert.Equal(("NR_REGALIAS", "USD", 462m, 0m, 462m, "NO_APLICA"), (
            fila.GetProperty("tipoRentaCodigo").GetString(), fila.GetProperty("monedaCodigo").GetString(), Dec(fila, "monto"),
            Dec(fila, "enteradoGtq"), Dec(fila, "saldoPorEnterarGtq"), fila.GetProperty("estadoConstancia").GetString()));
        Assert.Contains(reporte.GetProperty("totalesTipoRenta").EnumerateArray(), t => t.GetProperty("tipoRentaCodigo").GetString() == "NR_REGALIAS");
        Assert.All(reporte.GetProperty("totales").EnumerateArray(), t => Assert.Equal(0, t.GetProperty("sinConstancia").GetInt32()));

        var constancia = await c.Client.PostAsJsonAsync($"/api/retenciones/{id}/constancia", new { numero = "C-1", fecha = c.M.Jueves.Dia() });
        Assert.Equal(HttpStatusCode.Conflict, constancia.StatusCode);
    }
}
