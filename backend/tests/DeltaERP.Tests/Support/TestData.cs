using Npgsql;

namespace DeltaERP.Tests.Support;

public sealed record Escenario(
    int UsuarioId,
    int ClienteId,
    int ProveedorId,
    int PeriodoId,
    int CuentaCxC,
    int CuentaCxP,
    int CuentaIngreso,
    int CuentaGasto,
    int CentroCostoId);

// Bitácora, saldos, asientos y usuarios son inmutables: no hay limpieza posible, por
// eso cada prueba siembra filas propias con sufijos únicos y nunca cuenta filas globales.
public sealed class TestData
{
    private readonly NpgsqlDataSource _db;

    public TestData(NpgsqlDataSource db)
    {
        _db = db;
    }

    public static string Sufijo() => Guid.NewGuid().ToString("N")[..12];

    public async Task<T> ScalarAsync<T>(string sql, params object[] args)
    {
        await using var command = _db.CreateCommand(sql);
        foreach (var arg in args)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = arg });
        }
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task<int> CrearUsuarioAsync(string perfil = "Contador")
    {
        var sufijo = Sufijo();
        var perfilId = await ScalarAsync<int>("INSERT INTO perfil (nombre) VALUES ($1) RETURNING id", perfil);
        return await ScalarAsync<int>(
            "INSERT INTO usuario (nombre, email, password_hash, perfil_id) VALUES ($1, $2, 'x', $3) RETURNING id",
            $"Usuario {sufijo}", $"{sufijo}@test.local", perfilId);
    }

    public Task<int> CrearContraparteAsync(string tipo) =>
        ScalarAsync<int>("INSERT INTO contraparte (tipo, nombre) VALUES ($1, $2) RETURNING id", tipo, $"{tipo} {Sufijo()}");

    public Task<int> CrearCuentaAsync(string tipo, string naturaleza) =>
        ScalarAsync<int>(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza) VALUES ($1, $2, $3, $4) RETURNING id",
            $"T{Sufijo()}", $"Cuenta {tipo}", tipo, naturaleza);

    public Task<int> CrearCentroCostoAsync() =>
        ScalarAsync<int>("INSERT INTO centrocosto (codigo, nombre) VALUES ($1, $2) RETURNING id", $"C{Sufijo()}", "Centro de prueba");

    public Task<int> CrearPeriodoAsync(string estado = "Abierto") =>
        ScalarAsync<int>(
            "INSERT INTO periodocontable (nombre, fecha_inicio, fecha_fin, estado) VALUES ($1, '2025-01-01', '2025-12-31', $2) RETURNING id",
            $"Periodo {Sufijo()}", estado);

    public Task<int> CrearCuentaInactivaAsync(string tipo, string naturaleza)
    {
        var sufijo = Sufijo();
        return ScalarAsync<int>(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, activa) VALUES ($1, $2, $3, $4, false) RETURNING id",
            $"I{sufijo}", $"Inactiva {tipo}", tipo, naturaleza);
    }

    public async Task<(int PadreId, int HijoId)> CrearCuentaConSubcuentasAsync(string tipo, string naturaleza)
    {
        var sufijo = Sufijo();
        var padreId = await ScalarAsync<int>(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza) VALUES ($1, $2, $3, $4) RETURNING id",
            $"P{sufijo}", $"Padre {tipo}", tipo, naturaleza);
        var hijoId = await ScalarAsync<int>(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id) VALUES ($1, $2, $3, $4, $5) RETURNING id",
            $"H{sufijo}", $"Hijo {tipo}", tipo, naturaleza, padreId);
        return (padreId, hijoId);
    }

    public async Task<Escenario> SembrarEscenarioAsync() => new(
        UsuarioId: await CrearUsuarioAsync(),
        ClienteId: await CrearContraparteAsync("Cliente"),
        ProveedorId: await CrearContraparteAsync("Proveedor"),
        PeriodoId: await CrearPeriodoAsync(),
        CuentaCxC: await CrearCuentaAsync("Activo", "Deudora"),
        CuentaCxP: await CrearCuentaAsync("Pasivo", "Acreedora"),
        CuentaIngreso: await CrearCuentaAsync("Ingreso", "Acreedora"),
        CuentaGasto: await CrearCuentaAsync("Gasto", "Deudora"),
        CentroCostoId: await CrearCentroCostoAsync());
}
