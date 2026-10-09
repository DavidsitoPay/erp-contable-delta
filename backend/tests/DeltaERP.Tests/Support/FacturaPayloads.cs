using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeltaERP.Tests.Support;

// Construye los cuerpos JSON de factura para CxC y CxP; cada prueba modifica solo lo que necesita con Con(...).
public static class FacturaPayloads
{
    public const string FechaFactura = "2025-03-15";

    public static object Linea(
        int impuestoId, int cuentaId, decimal cantidad = 1m, decimal precio = 100m, int? centroCostoId = null, string tipoBienServicio = "SERVICIO") => new
    {
        descripcion = "Linea de prueba",
        cantidad,
        precioUnitario = precio,
        impuestoId,
        tipoBienServicio,
        centroCostoId,
        cuentaContableId = cuentaId,
    };

    public static object LineaExenta(Escenario e, int cuentaId, int? centroCostoId = null) =>
        Linea(e.ImpuestoExentoId, cuentaId, centroCostoId: centroCostoId);

    public static Dictionary<string, object?> Cxc(Escenario e, string numero, IEnumerable<object> lineas, int? clienteId = null) =>
        Cabecera(e, numero, lineas, "clienteId", clienteId ?? e.ClienteId, e.CuentaCxC).ConDte();

    public static Dictionary<string, object?> Cxp(Escenario e, string numero, IEnumerable<object> lineas, int? proveedorId = null) =>
        Cabecera(e, numero, lineas, "proveedorId", proveedorId ?? e.ProveedorId, e.CuentaCxP);

    public static Dictionary<string, object?> ConDte(this Dictionary<string, object?> payload)
    {
        foreach (var (clave, valor) in Dte())
        {
            payload[clave] = valor;
        }
        return payload;
    }

    public static Dictionary<string, object?> Con(this Dictionary<string, object?> payload, string clave, object? valor)
    {
        payload[clave] = valor;
        return payload;
    }

    public static Dictionary<string, object?> EnFecha(this Dictionary<string, object?> payload, DateOnly fecha)
    {
        payload["fecha"] = fecha.ToString("yyyy-MM-dd");
        payload["fechaVencimiento"] = fecha.AddDays(30).ToString("yyyy-MM-dd");
        if (payload.ContainsKey("dteFechaCertificacion"))
        {
            payload["dteFechaCertificacion"] = $"{fecha:yyyy-MM-dd}T10:15:00-06:00";
        }
        return payload;
    }

    public static Dictionary<string, object?> Sin(this Dictionary<string, object?> payload, string clave)
    {
        payload.Remove(clave);
        return payload;
    }

    public static async Task<(HttpStatusCode Estado, JsonElement Cuerpo)> EnviarAsync(this HttpClient client, string ruta, object payload)
    {
        var response = await client.PostAsJsonAsync(ruta, payload);
        return (response.StatusCode, await response.LeerJsonAsync());
    }

    public static async Task<(Escenario E, HttpClient Client)> PrepararAsync(this PostgresFixture pg)
    {
        var e = await pg.Data.SembrarEscenarioAsync();
        return (e, pg.CreateApiClient(e.UsuarioId));
    }

    public static Dictionary<string, object?> Dte() => new()
    {
        ["dteUuid"] = Guid.NewGuid(),
        ["dteSerie"] = $"S{TestData.Sufijo()[..8]}",
        ["dteNumero"] = $"N{TestData.Sufijo()}",
        ["dteFechaCertificacion"] = "2025-03-15T10:15:00-06:00",
    };

    private static Dictionary<string, object?> Cabecera(
        Escenario e, string numero, IEnumerable<object> lineas, string claveTercero, int terceroId, int cuentaControlId) => new()
    {
        ["numero"] = numero,
        ["tipoDocumento"] = "Factura",
        [claveTercero] = terceroId,
        ["fecha"] = FechaFactura,
        ["fechaVencimiento"] = "2025-04-15",
        ["periodoId"] = e.PeriodoId,
        ["cuentaControlId"] = cuentaControlId,
        ["lineas"] = lineas.ToArray(),
    };
}
