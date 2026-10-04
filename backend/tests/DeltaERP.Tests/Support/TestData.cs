using Microsoft.AspNetCore.Identity;
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

public sealed record OpcionesAsiento(string Estado = "Confirmado", int? CentroCostoId = null, string? Numero = null);

// Bitácora, saldos, asientos y usuarios son inmutables: no hay limpieza posible, por
// eso cada prueba siembra filas propias con sufijos únicos y nunca cuenta filas globales.
public sealed class TestData
{
    private readonly NpgsqlDataSource _db;
    private static int _anioPeriodoUnico = 2099;

    public TestData(NpgsqlDataSource db)
    {
        _db = db;
    }

    public static string Sufijo() => Guid.NewGuid().ToString("N")[..12];

    public static (DateOnly Inicio, DateOnly Fin) RangoPeriodoUnico()
    {
        var anio = Interlocked.Increment(ref _anioPeriodoUnico);
        return (new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));
    }

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

    public async Task<(int Id, string Email, string Password)> CrearUsuarioConCredencialesAsync(string perfil, string password, bool activo = true)
    {
        var sufijo = Sufijo();
        var perfilId = await ScalarAsync<int>("INSERT INTO perfil (nombre) VALUES ($1) RETURNING id", perfil);
        var email = $"{sufijo}@test.local";
        var hash = new PasswordHasher<object>().HashPassword(new object(), password);
        var id = await ScalarAsync<int>(
            "INSERT INTO usuario (nombre, email, password_hash, perfil_id, activo) VALUES ($1, $2, $3, $4, $5) RETURNING id",
            $"Usuario {sufijo}", email, hash, perfilId, activo);
        return (id, email, password);
    }

    public Task<int> CrearPeriodoEnRangoAsync(DateOnly inicio, DateOnly fin, string estado = "Abierto") =>
        ScalarAsync<int>(
            "INSERT INTO periodocontable (nombre, fecha_inicio, fecha_fin, estado) VALUES ($1, $2, $3, $4) RETURNING id",
            $"Periodo {Sufijo()}", inicio, fin, estado);

    public async Task<int> SembrarAsientoAsync(
        int periodoId, int usuarioId, int cuentaDebito, int cuentaCredito, decimal monto, DateOnly fecha,
        OpcionesAsiento? opciones = null)
    {
        opciones ??= new OpcionesAsiento();
        var asientoId = await ScalarAsync<int>(
            "INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id) VALUES ($1, $2, $3, $4, $5, $6) RETURNING id",
            opciones.Numero ?? $"A-{Sufijo()}", fecha, periodoId, monto, opciones.Estado, usuarioId);
        await ScalarAsync<int>(
            "INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito) VALUES ($1, $2, $5::int, $4, 0), ($1, $3, $5::int, 0, $4) RETURNING id",
            asientoId, cuentaDebito, cuentaCredito, monto, (object?)opciones.CentroCostoId ?? DBNull.Value);
        return asientoId;
    }

    public async Task EjecutarAsync(string sql, params object[] args)
    {
        await using var command = _db.CreateCommand(sql);
        foreach (var arg in args)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = arg });
        }
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ReversarAsientoAsync(int asientoId, int usuarioId, string numeroOriginal)
    {
        await EjecutarAsync("CALL sp_reversar_asiento($1, $2)", asientoId, usuarioId);
        return await ScalarAsync<int>("SELECT id FROM asientocontable WHERE numero = $1", $"REV-{numeroOriginal}");
    }
}
