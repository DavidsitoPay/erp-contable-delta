using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class TesoreriaEsquemaTests
{
    private readonly PostgresFixture _pg;

    public TesoreriaEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task MovimientoInmutable_UpdateDirect_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var movId = await _pg.Data.CrearMovimientoAsync(
            new(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida), "Ingreso", 100m, e.Inicio.AddDays(5), e.Periodo, e.Usuario);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE movimientotesoreria SET descripcion = 'Alterado' WHERE id = $1", movId));

        Assert.Equal("P0001", ex.SqlState);
        Assert.Contains("inmutable", ex.MessageText);
    }

    [Fact]
    public async Task MovimientoInmutable_DeleteDirect_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var movId = await _pg.Data.CrearMovimientoAsync(
            new(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida), "Ingreso", 100m, e.Inicio.AddDays(5), e.Periodo, e.Usuario);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM movimientotesoreria WHERE id = $1", movId));

        Assert.Equal("P0001", ex.SqlState);
        Assert.Contains("inmutable", ex.MessageText);
    }

    [Fact]
    public async Task CuentaBancariaEliminacion_DeleteDirect_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM cuentabancaria WHERE id = $1", e.CuentaBancaria));

        Assert.Equal("P0001", ex.SqlState);
        Assert.Contains("no se elimina", ex.MessageText);
    }

    [Fact]
    public async Task AperturaUnica_SegundaApertura_ViolaUniqueIndex()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var cuentaCapital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");

        var asientoPrimera = await _pg.Data.SembrarAsientoAsync(
            e.Periodo, e.Usuario, e.CuentaContableBanco, cuentaCapital, 500m, e.Inicio);
        await _pg.Data.EjecutarAsync(
            "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen) VALUES ($1, $2, 'Ingreso', 500, $3, 'Primera apertura', 'Apertura')",
            e.CuentaBancaria, e.Inicio, asientoPrimera);

        var asientoSegunda = await _pg.Data.SembrarAsientoAsync(
            e.Periodo, e.Usuario, e.CuentaContableBanco, cuentaCapital, 600m, e.Inicio.AddDays(1));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync(
                "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen) VALUES ($1, $2, 'Ingreso', 600, $3, 'Segunda apertura', 'Apertura')",
                e.CuentaBancaria, e.Inicio.AddDays(1), asientoSegunda));

        Assert.Equal("23505", ex.SqlState);
        Assert.Contains("ux_movimiento_apertura", ex.ConstraintName);
    }

    [Fact]
    public async Task ConciliacionInmutable_UpdateConciliado_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var concilId = await _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(10), 1000m);
        await _pg.Data.ForzarEstadoConciliacionAsync(concilId, "Conciliado");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE conciliacionbancaria SET saldo_extracto = 2000 WHERE id = $1", concilId));

        Assert.Equal("55000", ex.SqlState);
        Assert.Contains("no admite modificación", ex.MessageText);
    }

    [Fact]
    public async Task ConciliacionInmutable_DeleteConciliado_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var concilId = await _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(10), 1000m);
        await _pg.Data.ForzarEstadoConciliacionAsync(concilId, "Conciliado");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM conciliacionbancaria WHERE id = $1", concilId));

        Assert.Equal("55000", ex.SqlState);
        Assert.Contains("no admite modificación", ex.MessageText);
    }

    [Fact]
    public async Task DetalleValidacion_ConciliacionNoPendiente_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var movId = await _pg.Data.CrearMovimientoAsync(
            new(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida), "Ingreso", 100m, e.Inicio.AddDays(5), e.Periodo, e.Usuario);
        var concilId = await _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(10), 1000m);
        await _pg.Data.ForzarEstadoConciliacionAsync(concilId, "Conciliado");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("INSERT INTO detalleconciliacion (conciliacion_id, movimiento_id) VALUES ($1, $2)", concilId, movId));

        Assert.Equal("55000", ex.SqlState);
        Assert.Contains("no está pendiente", ex.MessageText);
    }

    [Fact]
    public async Task DetalleValidacion_MovimientoOtraCuenta_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var (cuenta2, ctaBanco2) = await _pg.Data.CrearCuentaBancariaAsync();
        var movOtraCuenta = await _pg.Data.CrearMovimientoAsync(
            new(cuenta2, ctaBanco2, e.Contrapartida), "Ingreso", 100m, e.Inicio.AddDays(5), e.Periodo, e.Usuario);
        var concilId = await _pg.Data.CrearConciliacionAsync(e.CuentaBancaria, e.Periodo, e.Inicio.AddDays(10), 1000m);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("INSERT INTO detalleconciliacion (conciliacion_id, movimiento_id) VALUES ($1, $2)", concilId, movOtraCuenta));

        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("otra cuenta", ex.MessageText);
    }

    [Fact]
    public async Task MovimientoVinculo_AperturaConTransferenciaid_Rechaza()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var cuentaCapital = await _pg.Data.CrearCuentaAsync("Capital", "Acreedora");
        var asientoApertura = await _pg.Data.SembrarAsientoAsync(
            e.Periodo, e.Usuario, e.CuentaContableBanco, cuentaCapital, 500m, e.Inicio);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync(
                "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen, transferencia_id) VALUES ($1, $2, 'Ingreso', 500, $3, 'Apertura', 'Apertura', $4)",
                e.CuentaBancaria, e.Inicio, asientoApertura, Guid.NewGuid()));

        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("vinculo", ex.MessageText);
    }
}
