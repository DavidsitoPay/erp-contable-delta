using System.Net;
using System.Text.Json;

namespace DeltaERP.Tests.Support;

// Cuerpos y flujos compartidos por las pruebas de retenciones.
public static class RetencionPayloads
{
    public const string RutaFacturasCxP = "/api/cxp/facturas";
    public const string RutaPagosCxC = "/api/cxc/pagos";
    public const string RutaPagosCxP = "/api/cxp/pagos";

    private static int _anioAislado = 2014;

    // Cada prueba de reporte usa un año propio para que sus totales no se mezclen con los de otras pruebas.
    public static int SiguienteAnio() => Interlocked.Increment(ref _anioAislado);

    public static string Numero() => $"R-{TestData.Sufijo()}";

    public static string NumeroDe(Dictionary<string, object?> payload) => payload["numero"] as string ?? string.Empty;

    public static Dictionary<string, object?> CompraConIva(Escenario e, int proveedorId, decimal totalConIva) =>
        FacturaPayloads.Cxp(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaGasto, 1m, totalConIva) }, proveedorId).ConDte();

    public static Dictionary<string, object?> CompraExenta(Escenario e, int proveedorId, decimal total) =>
        FacturaPayloads.Cxp(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoExentoId, e.CuentaGasto, 1m, total) }, proveedorId);

    public static Dictionary<string, object?> CompraDePequenoContribuyente(Escenario e, int proveedorId, decimal total) =>
        FacturaPayloads.Cxp(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoPequenoId, e.CuentaGasto, 1m, total) }, proveedorId);

    public static Dictionary<string, object?> VentaConIva(Escenario e, int clienteId, decimal totalConIva) =>
        FacturaPayloads.Cxc(e, Numero(), new[] { FacturaPayloads.Linea(e.ImpuestoIvaId, e.CuentaIngreso, 1m, totalConIva) }, clienteId);

    public static object PagoCxP(int proveedorId, int cuentaBancariaId, int documentoId, decimal monto) => new
    {
        proveedorId,
        fecha = "2025-03-20",
        metodoPago = "Transferencia",
        cuentaBancariaId,
        aplicaciones = new object[] { new { documentoId, montoAplicado = monto } },
    };

    public static object Ajuste(decimal monto, string? motivo) => new { monto, motivo };

    public static object RetencionIva(decimal monto, bool confirmaClienteNoAgente = false, string? numeroConstancia = null, string? fechaConstancia = null) => new
    {
        monto,
        confirmaClienteNoAgente,
        constancia = numeroConstancia is null ? (object?)null : new { numero = numeroConstancia, fecha = fechaConstancia },
    };

    public static object Aplicacion(int documentoId, decimal montoAplicado, object? retencionIva = null) => new
    {
        documentoId,
        montoAplicado,
        retencionIva,
    };

    public static object Cobro(int clienteId, int cuentaBancariaId, string fecha, params object[] aplicaciones) => new
    {
        clienteId,
        fecha,
        metodoPago = "Transferencia",
        cuentaBancariaId,
        aplicaciones,
    };

    public static object AplicacionMe(int documentoId, decimal montoAplicado, decimal? valorEfectivoGtq = null) => new
    {
        documentoId,
        montoAplicado,
        valorEfectivoGtq,
    };

    public static object PagoProveedor(int proveedorId, int cuentaBancariaId, string fecha, params object[] aplicaciones) => new
    {
        proveedorId,
        fecha,
        metodoPago = "Transferencia",
        cuentaBancariaId,
        aplicaciones,
    };

    public static string Dia(this DateOnly fecha) => fecha.ToString("yyyy-MM-dd");

    public static async Task<(int FacturaId, int? RetencionId, JsonElement Cuerpo)> RegistrarCompraAsync(
        this HttpClient client, Dictionary<string, object?> payload)
    {
        var (estado, cuerpo) = await client.EnviarAsync(RutaFacturasCxP, payload);
        Assert.Equal(HttpStatusCode.Created, estado);
        int? retencionId = cuerpo.TryGetProperty("retencionIsr", out var retencion) && retencion.TryGetProperty("retencionId", out var id)
            && id.ValueKind == JsonValueKind.Number
            ? id.GetInt32()
            : null;
        return (cuerpo.GetProperty("id").GetInt32(), retencionId, cuerpo);
    }

    public static async Task<int> RegistrarVentaAsync(this HttpClient client, Dictionary<string, object?> payload)
    {
        var (estado, cuerpo) = await client.EnviarAsync("/api/cxc/facturas", payload);
        Assert.Equal(HttpStatusCode.Created, estado);
        return cuerpo.GetProperty("id").GetInt32();
    }

    public static async Task<Escenario> PrepararEnAnioAsync(this PostgresFixture pg, int anio)
    {
        var e = await pg.Data.SembrarEscenarioAsync();
        var periodo = await pg.Data.CrearPeriodoEnRangoAsync(new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));
        return e with { PeriodoId = periodo };
    }
}
