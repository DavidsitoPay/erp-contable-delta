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
    int CentroCostoId,
    int CuentaIvaDebito,
    int CuentaIvaCredito,
    int ImpuestoIvaId,
    int ImpuestoExentoId,
    int ImpuestoSinCreditoId,
    int ImpuestoPequenoId);

public sealed record EscenarioTesoreria(int Usuario, int Periodo, DateOnly Inicio, int CuentaBancaria, int CuentaContableBanco, int Contrapartida);

public sealed record EscenarioJerarquia(int Usuario, int Periodo, int Padre, int HijoA, int HijoB, int Ingreso);

public sealed record PeriodoSembrado(int Id, DateOnly Inicio, DateOnly Fin);

public sealed record CuentasReporte(int Usuario, int Activo, int Pasivo, int Capital, int Ingreso, int Gasto);

public record CuentasMovimiento(int CuentaBancariaId, int CuentaContableBancoId, int ContrapartidaId);

public sealed record OpcionesAsiento(string Estado = "Confirmado", int? CentroCostoId = null, string? Numero = null);

public sealed record OpcionesImpuesto(
    string Tipo = "IVA_GENERAL",
    decimal Tasa = 12m,
    string AplicaA = "AMBOS",
    bool GeneraCredito = true,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    bool Activo = true);

// Bitácora, saldos, asientos y usuarios son inmutables: no hay limpieza posible, por
// eso cada prueba siembra filas propias con sufijos únicos y nunca cuenta filas globales.
public sealed class TestData
{
    private readonly NpgsqlDataSource _db;
    private static int _anioPeriodoUnico = 2099;
    private static int _anioPeriodoHistorico = 1900;
    public static readonly DateOnly FechaSinPeriodo = new(1990, 6, 15);
    public const string NitValido = "1234567-9";
    public const string CodigoCuentaIvaDebito = "T-IVA-DEBITO";
    public const string CodigoCuentaIvaCredito = "T-IVA-CREDITO";

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
        CrearContraparteFiscalAsync(tipo, "GENERAL", tipo == "Proveedor" ? NitValido : null);

    public Task<int> CrearContraparteFiscalAsync(string tipo, string regimenIva, string? nit) =>
        ScalarAsync<int>(
            "INSERT INTO contraparte (tipo, nombre, nit, regimen_iva) VALUES ($1, $2, $3::varchar, $4) RETURNING id",
            tipo, $"{tipo} {Sufijo()}", (object?)nit ?? DBNull.Value, regimenIva);

    public async Task SembrarConfiguracionFiscalAsync()
    {
        await EjecutarAsync(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza) VALUES ($1, 'IVA débito fiscal (pruebas)', 'Pasivo', 'Acreedora'), ($2, 'IVA crédito fiscal (pruebas)', 'Activo', 'Deudora')",
            CodigoCuentaIvaDebito, CodigoCuentaIvaCredito);
        await RestaurarConfiguracionFiscalAsync();
    }

    public Task RestaurarConfiguracionFiscalAsync() =>
        EjecutarAsync(
            "UPDATE configuracionfiscal SET nit_empresa = $1, nombre_legal = 'Delta de Pruebas, S.A.', regimen_isr = 'UTILIDADES', agente_retencion_iva = FALSE, tipo_agente_iva = NULL, " +
            "cuenta_iva_debito_id = (SELECT id FROM cuentacontable WHERE codigo = $2), cuenta_iva_credito_id = (SELECT id FROM cuentacontable WHERE codigo = $3) WHERE id = 1",
            NitValido, CodigoCuentaIvaDebito, CodigoCuentaIvaCredito);

    public Task ConfigurarCuentasFiscalesAsync(int? debitoId, int? creditoId) =>
        EjecutarAsync(
            "UPDATE configuracionfiscal SET cuenta_iva_debito_id = $1::int, cuenta_iva_credito_id = $2::int WHERE id = 1",
            (object?)debitoId ?? DBNull.Value, (object?)creditoId ?? DBNull.Value);

    public Task<int> CuentaPorCodigoAsync(string codigo) =>
        ScalarAsync<int>("SELECT id FROM cuentacontable WHERE codigo = $1", codigo);

    public Task<int> ImpuestoPorCodigoAsync(string codigo) =>
        ScalarAsync<int>("SELECT id FROM impuesto WHERE codigo = $1", codigo);

    public Task<int> CrearImpuestoAsync(OpcionesImpuesto? opciones = null)
    {
        opciones ??= new OpcionesImpuesto();
        var sufijo = Sufijo();
        return ScalarAsync<int>(
            "INSERT INTO impuesto (codigo, nombre, tipo, tasa, aplica_a, genera_credito, articulo_legal, vigente_desde, vigente_hasta, activo) " +
            "VALUES ($1, $2, $3, $4, $5, $6, 'Impuesto de prueba', $7, $8::date, $9) RETURNING id",
            $"T{sufijo}", $"Impuesto {sufijo}", opciones.Tipo, opciones.Tasa, opciones.AplicaA, opciones.GeneraCredito,
            opciones.Desde ?? new DateOnly(2020, 1, 1), (object?)opciones.Hasta ?? DBNull.Value, opciones.Activo);
    }

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

    public Task<int> CrearSubcuentaAsync(int padreId, string tipo, string naturaleza) =>
        ScalarAsync<int>(
            "INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id) VALUES ($1, $2, $3, $4, $5) RETURNING id",
            $"S{Sufijo()}", $"Sub {tipo}", tipo, naturaleza, padreId);

    public async Task<(int AsientoId, int UsuarioId)> SembrarSaldoPropioAsync(int cuentaId, string estado = "Confirmado")
    {
        var usuario = await CrearUsuarioAsync();
        var periodo = await CrearPeriodoAsync();
        var contrapartida = await CrearCuentaAsync("Ingreso", "Acreedora");
        var asiento = await SembrarAsientoAsync(
            periodo, usuario, cuentaId, contrapartida, 100m, new DateOnly(2025, 3, 1), new OpcionesAsiento(Estado: estado));
        return (asiento, usuario);
    }

    public async Task<int> CrearCuentaConSaldoPropioAsync(string estado = "Confirmado")
    {
        var cuenta = await CrearCuentaAsync("Activo", "Deudora");
        await SembrarSaldoPropioAsync(cuenta, estado);
        return cuenta;
    }

    public async Task<EscenarioJerarquia> SembrarJerarquiaConSaldosAsync()
    {
        var usuario = await CrearUsuarioAsync();
        var periodo = await CrearPeriodoAsync();
        var padre = await CrearCuentaAsync("Activo", "Deudora");
        var hijoA = await CrearSubcuentaAsync(padre, "Activo", "Deudora");
        var hijoB = await CrearSubcuentaAsync(padre, "Activo", "Deudora");
        var ingreso = await CrearCuentaAsync("Ingreso", "Acreedora");
        await SembrarAsientoAsync(periodo, usuario, hijoA, ingreso, 100m, new DateOnly(2025, 1, 10));
        await SembrarAsientoAsync(periodo, usuario, hijoB, ingreso, 50m, new DateOnly(2025, 1, 20));
        return new EscenarioJerarquia(usuario, periodo, padre, hijoA, hijoB, ingreso);
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
        CentroCostoId: await CrearCentroCostoAsync(),
        CuentaIvaDebito: await CuentaPorCodigoAsync(CodigoCuentaIvaDebito),
        CuentaIvaCredito: await CuentaPorCodigoAsync(CodigoCuentaIvaCredito),
        ImpuestoIvaId: await ImpuestoPorCodigoAsync("IVA_GENERAL"),
        ImpuestoExentoId: await ImpuestoPorCodigoAsync("EXENTO_EXPORTACION"),
        ImpuestoSinCreditoId: await ImpuestoPorCodigoAsync("IVA_GENERAL_SIN_CREDITO"),
        ImpuestoPequenoId: await ImpuestoPorCodigoAsync("PEQUENO_CONTRIBUYENTE"));

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

    public async Task<IReadOnlyList<PeriodoSembrado>> CrearPeriodosHistoricosAsync(int cantidad)
    {
        var primerAnio = Interlocked.Add(ref _anioPeriodoHistorico, -cantidad);
        var periodos = new List<PeriodoSembrado>();
        for (var i = 0; i < cantidad; i++)
        {
            var inicio = new DateOnly(primerAnio + i, 1, 1);
            var fin = new DateOnly(primerAnio + i, 12, 31);
            periodos.Add(new PeriodoSembrado(await CrearPeriodoEnRangoAsync(inicio, fin), inicio, fin));
        }
        return periodos;
    }

    public async Task<CuentasReporte> SembrarCuentasReporteAsync() => new(
        Usuario: await CrearUsuarioAsync(),
        Activo: await CrearCuentaAsync("Activo", "Deudora"),
        Pasivo: await CrearCuentaAsync("Pasivo", "Acreedora"),
        Capital: await CrearCuentaAsync("Capital", "Acreedora"),
        Ingreso: await CrearCuentaAsync("Ingreso", "Acreedora"),
        Gasto: await CrearCuentaAsync("Gasto", "Deudora"));

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

    public async Task<List<(int Cuenta, decimal Debito, decimal Credito)>> LineasDeAsientoAsync(int asientoId)
    {
        await using var command = _db.CreateCommand("SELECT cuenta_id, debito, credito FROM lineaasiento WHERE asiento_id = $1 ORDER BY id");
        command.Parameters.Add(new NpgsqlParameter { Value = asientoId });
        await using var lector = await command.ExecuteReaderAsync();
        var lineas = new List<(int, decimal, decimal)>();
        while (await lector.ReadAsync())
        {
            lineas.Add((lector.GetInt32(0), lector.GetDecimal(1), lector.GetDecimal(2)));
        }
        return lineas;
    }

    public async Task<int> ReversarAsientoAsync(int asientoId, string motivo = "Reversa de prueba")
    {
        var administrador = await CrearUsuarioAsync("Administrador del sistema");
        await EjecutarAsync("CALL sp_reversar_asiento($1, $2, $3)", asientoId, administrador, motivo);
        return await ScalarAsync<int>("SELECT id FROM asientocontable WHERE reversa_de_id = $1", asientoId);
    }

    public Task<int> VincularFacturaCxCAsync(int clienteId, int asientoId, DateOnly fecha) =>
        ScalarAsync<int>(
            "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado, asiento_id) VALUES ($1, 'Factura', $2, $3, $3, 100, 100, TRUE, 'Vigente', $4) RETURNING id",
            $"F-{Sufijo()}", clienteId, fecha, asientoId);

    public Task<int> VincularFacturaCxPAsync(int proveedorId, int asientoId, DateOnly fecha) =>
        ScalarAsync<int>(
            "INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado, asiento_id) VALUES ($1, 'Factura', $2, $3, $3, 100, 100, TRUE, 'Vigente', $4) RETURNING id",
            $"F-{Sufijo()}", proveedorId, fecha, asientoId);

    public async Task<int> SembrarAsientoDeTesoreriaAsync()
    {
        var t = await SembrarTesoreriaAsync();
        var cuentas = new CuentasMovimiento(t.CuentaBancaria, t.CuentaContableBanco, t.Contrapartida);
        var movimiento = await CrearMovimientoAsync(cuentas, "Ingreso", 100m, t.Inicio, t.Periodo, t.Usuario);
        return await ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE id = $1", movimiento);
    }

    public async Task VincularMovimientoTesoreriaAsync(int asientoId, DateOnly fecha)
    {
        var (cuentaBancaria, _) = await CrearCuentaBancariaAsync();
        await EjecutarAsync(
            "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen) VALUES ($1, $2, 'Ingreso', 100, $3, 'Movimiento de prueba', 'Manual')",
            cuentaBancaria, fecha, asientoId);
    }

    public Task CerrarPeriodoDirectoAsync(int periodoId) =>
        EjecutarAsync("UPDATE periodocontable SET estado = 'Cerrado' WHERE id = $1", periodoId);

    public async Task<(int Id, int CuentaContableId)> CrearCuentaBancariaAsync(string tipo = "Monetaria")
    {
        var cuentaContableId = await CrearCuentaAsync("Activo", "Deudora");
        var id = await ScalarAsync<int>(
            "INSERT INTO cuentabancaria (banco, numero, tipo, cuenta_contable_id, saldo_apertura) VALUES ($1, $2, $3, $4, 0) RETURNING id",
            $"Banco {Sufijo()}", $"N{Sufijo()}", tipo, cuentaContableId);
        return (id, cuentaContableId);
    }

    public async Task<int> CrearMovimientoAsync(
        CuentasMovimiento cuentas, string tipo, decimal monto, DateOnly fecha, int periodoId, int usuarioId)
    {
        var esIngreso = tipo == "Ingreso";
        var asientoId = await SembrarAsientoAsync(
            periodoId, usuarioId,
            esIngreso ? cuentas.CuentaContableBancoId : cuentas.ContrapartidaId,
            esIngreso ? cuentas.ContrapartidaId : cuentas.CuentaContableBancoId,
            monto, fecha);
        return await ScalarAsync<int>(
            "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen) VALUES ($1, $2, $3, $4, $5, 'Movimiento de prueba', 'Manual') RETURNING id",
            cuentas.CuentaBancariaId, fecha, tipo, monto, asientoId);
    }

    public Task<int> CrearConciliacionAsync(int cuentaBancariaId, int periodoId, DateOnly fecha, decimal saldoExtracto, string estado = "Pendiente") =>
        ScalarAsync<int>(
            "INSERT INTO conciliacionbancaria (cuenta_bancaria_id, periodo_id, fecha, estado, saldo_extracto) VALUES ($1, $2, $3, $4, $5) RETURNING id",
            cuentaBancariaId, periodoId, fecha, estado, saldoExtracto);

    public Task MarcarMovimientoAsync(int conciliacionId, int movimientoId) =>
        EjecutarAsync("INSERT INTO detalleconciliacion (conciliacion_id, movimiento_id) VALUES ($1, $2)", conciliacionId, movimientoId);

    public Task ForzarEstadoConciliacionAsync(int conciliacionId, string estado) =>
        EjecutarAsync("UPDATE conciliacionbancaria SET estado = $1 WHERE id = $2", estado, conciliacionId);

    public async Task<EscenarioTesoreria> SembrarTesoreriaAsync()
    {
        var (inicio, fin) = RangoPeriodoUnico();
        var usuario = await CrearUsuarioAsync();
        var periodo = await CrearPeriodoEnRangoAsync(inicio, fin);
        var (cuentaBancaria, cuentaContableBanco) = await CrearCuentaBancariaAsync();
        var contrapartida = await CrearCuentaAsync("Ingreso", "Acreedora");
        return new EscenarioTesoreria(usuario, periodo, inicio, cuentaBancaria, cuentaContableBanco, contrapartida);
    }

    public Task<decimal> SaldoBancarioAsync(int cuentaBancariaId) =>
        ScalarAsync<decimal>("SELECT saldo FROM vw_saldocuentabancaria WHERE cuenta_bancaria_id = $1", cuentaBancariaId);

    public Task<decimal> DebitoAsync(int asientoId, int cuentaId) =>
        ScalarAsync<decimal>("SELECT COALESCE(SUM(debito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, cuentaId);

    public Task<decimal> CreditoAsync(int asientoId, int cuentaId) =>
        ScalarAsync<decimal>("SELECT COALESCE(SUM(credito), 0) FROM lineaasiento WHERE asiento_id = $1 AND cuenta_id = $2", asientoId, cuentaId);

    public Task<long> ContarAuditoriaAsync(int usuarioId, string accion) =>
        ScalarAsync<long>("SELECT COUNT(*) FROM bitacoraauditoria WHERE usuario_id = $1 AND accion = $2", usuarioId, accion);
}
