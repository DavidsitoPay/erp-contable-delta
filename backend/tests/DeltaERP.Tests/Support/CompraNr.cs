using System.Net;
using System.Text.Json;
using DeltaERP.Domain.Rules;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Support;

public sealed record Compra(EscenarioMe M, HttpClient Client, int ProveedorId, int FacturaId)
{
    public DateOnly Periodo => new(M.Jueves.Year, M.Jueves.Month, 1);
}

public static class CompraNrExtensions
{
    public const string RutaEnteramientos = "/api/retenciones/enteramientos";

    public static async Task<Compra> CompraNrAsync(
        this PostgresFixture pg, string moneda = "USD", string? tipoRenta = "NR_REGALIAS", string regimen = "SIMPLIFICADO", string? fundamento = null,
        decimal precio = 1000m, DateOnly? inicio = null)
    {
        var m = await pg.Data.SembrarEscenarioMeAsync(inicio);
        await pg.Data.SembrarTipoCambioAsync("USD", m.Miercoles, 7.60m);
        await pg.Data.SembrarTipoCambioAsync("USD", m.Jueves, 7.70m);
        var proveedor = await pg.Data.CrearProveedorNoResidenteAsync(tipoRenta, regimen, fundamento);
        var client = pg.CreateApiClient(m.E.UsuarioId);
        var (estado, cuerpo) = await client.EnviarAsync(RutaFacturasCxP, FacturaPayloads.CxpEn(m, moneda, m.Miercoles, precio, proveedor));
        Assert.Equal(HttpStatusCode.Created, estado);
        return new Compra(m, client, proveedor, cuerpo.GetProperty("id").GetInt32());
    }

    // Factura de 1000 en quetzales pagada en 600 el jueves: retiene 90 en el periodo del mes del pago.
    public static async Task<Compra> CompraNrPagadaAsync(this PostgresFixture pg, DateOnly? inicio = null)
    {
        var c = await pg.CompraNrAsync("GTQ", inicio: inicio);
        Assert.Equal(HttpStatusCode.Created, (await c.PagarNrAsync(c.M.BancoGtqId, c.M.Jueves, 600m)).Estado);
        return c;
    }

    public static Task<(HttpStatusCode Estado, JsonElement Cuerpo)> PagarNrAsync(this Compra c, int cuentaId, DateOnly fecha, decimal monto) =>
        c.Client.EnviarAsync(RutaPagosCxP, PagoProveedor(c.ProveedorId, cuentaId, fecha.Dia(), AplicacionNr(c.FacturaId, monto)));

    public static Dictionary<string, object?> SolicitudEntero(this Compra c, decimal monto) => new()
    {
        ["formulario"] = "SAT-1352",
        ["periodo"] = c.Periodo.Dia(),
        ["fechaPago"] = c.M.Jueves.Dia(),
        ["boleta"] = $"B-{TestData.Sufijo()}",
        ["monto"] = monto,
        ["cuentaBancariaId"] = c.M.BancoGtqId,
    };

    public static async Task<int> EnterarAsync(this Compra c, decimal monto)
    {
        var (estado, cuerpo) = await c.Client.EnviarAsync(RutaEnteramientos, c.SolicitudEntero(monto));
        Assert.Equal(HttpStatusCode.Created, estado);
        return cuerpo.GetProperty("id").GetInt32();
    }

    // Devuelve el periodo que cubre hoy, abierto: reabre el cerrado si existe y crea uno solo si no hay ninguno (evita traslapes).
    public static async Task<int> AsegurarPeriodoDeHoyAsync(this PostgresFixture pg)
    {
        var hoy = CambioRules.HoyGuatemala(DateTimeOffset.UtcNow);
        await pg.Data.EjecutarAsync(
            "UPDATE periodocontable SET estado = 'Abierto' WHERE fecha_inicio <= $1 AND fecha_fin >= $1 AND estado = 'Cerrado'", hoy);
        var existe = await pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM periodocontable WHERE fecha_inicio <= $1 AND fecha_fin >= $1", hoy);
        return existe == 0
            ? await pg.Data.CrearPeriodoEnRangoAsync(hoy.AddDays(-1), hoy.AddDays(1))
            : await pg.Data.ScalarAsync<int>("SELECT id FROM periodocontable WHERE fecha_inicio <= $1 AND fecha_fin >= $1", hoy);
    }
}
