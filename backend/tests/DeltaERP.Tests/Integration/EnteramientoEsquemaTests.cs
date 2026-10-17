using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class EnteramientoEsquemaTests
{
    private const string Estado55000 = "55000";

    private readonly PostgresFixture _pg;

    public EnteramientoEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private sealed record Escenario(
        int Usuario, int PeriodoContable, DateOnly Periodo, DateOnly FechaPago, CuentasMovimiento Cuentas,
        int RetencionId, int ProveedorId, int DocumentoId, int AsientoDocumentoId);

    private async Task<Escenario> SembrarRetencionAsync(decimal retenido)
    {
        var data = _pg.Data;
        var (inicio, fin) = TestData.RangoPeriodoUnico();
        var usuario = await data.CrearUsuarioAsync();
        var periodoContable = await data.CrearPeriodoEnRangoAsync(inicio, fin);
        var periodo = new DateOnly(inicio.Year, 3, 1);
        var fechaOperacion = periodo.AddDays(9);
        var proveedor = await data.CrearProveedorNoResidenteAsync("NR_OTRAS_RENTAS");
        var gasto = await data.CrearCuentaAsync("Gasto", "Deudora");
        var pasivo = await data.CrearCuentaAsync("Pasivo", "Acreedora");
        var asientoDocumento = await data.SembrarAsientoAsync(periodoContable, usuario, gasto, pasivo, 100m, fechaOperacion);
        var documento = await data.VincularFacturaCxPAsync(proveedor, asientoDocumento, fechaOperacion);
        var pago = await data.ScalarAsync<int>(
            "INSERT INTO pagoproveedorcabecera (proveedor_id, fecha, monto_total, metodo_pago) VALUES ($1, $2, 50, 'Transferencia') RETURNING id",
            proveedor, fechaOperacion);
        var aplicacion = await data.ScalarAsync<int>(
            "INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado) VALUES ($1, $2, 50) RETURNING id",
            pago, documento);
        var asientoRetencion = await data.SembrarAsientoAsync(periodoContable, usuario, pasivo, gasto, retenido, fechaOperacion);
        var retencion = await data.ScalarAsync<int>(
            "INSERT INTO retencion (concepto, contraparte_id, documento_cxp_id, aplicacion_pago_proveedor_id, parametro_id, fecha_documento, fecha_operacion, " +
            "cuenta_id, base, tasa, monto_calculado, monto_retenido, asiento_id, registrado_por, moneda_id, monto_origen, tipo_cambio, tipo_cambio_fecha, tipo_renta_codigo) " +
            "VALUES ('ISR_NORES', $1, $2, $3, (SELECT id FROM parametroretencion WHERE codigo = 'NR_OTRAS_RENTAS'), $4, $4, $5, 200, 25, $6, $6, $7, $8, " +
            "(SELECT id FROM moneda WHERE es_funcional), 200, 1, $4, 'NR_OTRAS_RENTAS') RETURNING id",
            proveedor, documento, aplicacion, fechaOperacion, gasto, retenido, asientoRetencion, usuario);
        var (banco, cuentaBanco) = await data.CrearCuentaBancariaAsync();
        var contrapartida = await data.CrearCuentaAsync("Pasivo", "Acreedora");
        return new Escenario(
            usuario, periodoContable, periodo, periodo.AddMonths(1).AddDays(4),
            new CuentasMovimiento(banco, cuentaBanco, contrapartida), retencion, proveedor, documento, asientoDocumento);
    }

    private async Task<(int Movimiento, int Asiento)> MovimientoAsync(Escenario e, string tipo, CuentasMovimiento? cuentas = null)
    {
        var movimiento = await _pg.Data.CrearMovimientoAsync(cuentas ?? e.Cuentas, tipo, 10m, e.FechaPago, e.PeriodoContable, e.Usuario);
        var asiento = await _pg.Data.ScalarAsync<int>("SELECT asiento_id FROM movimientotesoreria WHERE id = $1", movimiento);
        return (movimiento, asiento);
    }

    private async Task<int> EnterarAsync(
        Escenario e, decimal monto, string boleta, CuentasMovimiento? cuentas = null, string formulario = "SAT-1352", int diasFechaPago = 0,
        int? cuentaBancariaPagoId = null)
    {
        var (movimiento, asiento) = await MovimientoAsync(e, "Egreso", cuentas);
        return await _pg.Data.ScalarAsync<int>(
            "INSERT INTO enteramiento (formulario, periodo, fecha_pago, boleta, monto, cuenta_bancaria_id, asiento_id, movimiento_tesoreria_id, registrado_por) " +
            "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9) RETURNING id",
            formulario, e.Periodo, e.FechaPago.AddDays(diasFechaPago), boleta, monto, cuentaBancariaPagoId ?? (cuentas ?? e.Cuentas).CuentaBancariaId, asiento, movimiento, e.Usuario);
    }

    private async Task AnularAsync(Escenario e, int enteramientoId)
    {
        var (movimiento, asiento) = await MovimientoAsync(e, "Ingreso");
        await _pg.Data.EjecutarAsync(
            "UPDATE enteramiento SET estado = 'ANULADO', motivo_anulacion = 'Boleta errada', anulado_por = $2, anulado_en = now(), " +
            "asiento_reversa_id = $3, movimiento_reversa_id = $4 WHERE id = $1",
            enteramientoId, e.Usuario, asiento, movimiento);
    }

    [Theory]
    [InlineData("enteramiento", "id,concepto,formulario,periodo,fecha_pago,boleta,monto,cuenta_bancaria_id,asiento_id,movimiento_tesoreria_id,estado,motivo_anulacion,anulado_por,anulado_en,asiento_reversa_id,movimiento_reversa_id,registrado_por,creado_en")]
    [InlineData("ajuste_retencion", "id,retencion_id,nota_credito_id,monto_nc,estado,creado_por,creado_en,motivo_resolucion,referencia_resolucion,resuelto_por,resuelto_en")]
    [InlineData("feriado", "fecha,nombre,medio_dia,creado_por,creado_en")]
    [InlineData("vw_saldo_enterar", "periodo,retenido,enterado,saldo")]
    [InlineData("vw_retencion_reporte", "pais,ajuste_pendiente,enterado_gtq,saldo_por_enterar_gtq")]
    [InlineData("contraparte", "pais")]
    public async Task Migracion_CreaLasColumnasEsperadas(string tabla, string columnas)
    {
        var esperadas = columnas.Split(',');

        var presentes = await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = $1 AND column_name = ANY($2)",
            tabla, esperadas);

        Assert.Equal(esperadas.Length, presentes);
    }

    [Fact]
    public async Task Migracion_SiembraLosFeriadosDe2026Y2027()
    {
        var sembrados = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM feriado WHERE creado_por IS NULL");
        var ejercito = await _pg.Data.ScalarAsync<bool>("SELECT medio_dia FROM feriado WHERE fecha = DATE '2026-06-29'");
        var nochebuena = await _pg.Data.ScalarAsync<bool>("SELECT medio_dia FROM feriado WHERE fecha = DATE '2026-12-24'");
        var omitidos = await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM feriado WHERE fecha IN (DATE '2026-06-30', DATE '2026-08-15')");

        Assert.Equal(21L, sembrados);
        Assert.False(ejercito);
        Assert.True(nochebuena);
        Assert.Equal(0L, omitidos);
    }

    [Fact]
    public async Task Migracion_SiembraLaCadenaDeCuentasDelIsrNoResidente()
    {
        var cadena = await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM cuentacontable h JOIN cuentacontable p ON p.id = h.cuenta_padre_id JOIN cuentacontable a ON a.id = p.cuenta_padre_id " +
            "WHERE h.codigo = '2.1.03' AND h.tipo = 'Pasivo' AND p.codigo = '2.1' AND a.codigo = '2' AND a.cuenta_padre_id IS NULL");

        Assert.Equal(1L, cadena);
    }

    [Fact]
    public async Task Migracion_Reejecutada_NoDuplicaLaSiembra()
    {
        var cuentasAntes = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM cuentacontable WHERE codigo IN ('2', '2.1', '2.1.03')");
        var feriadosAntes = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM feriado");
        var sql = await File.ReadAllTextAsync(
            Path.Combine(PostgresFixture.LocalizarDirectorioDatabase(), "18_enteramiento_ajuste_feriado.sql"));

        await using (var command = _pg.DataSource.CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(cuentasAntes, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM cuentacontable WHERE codigo IN ('2', '2.1', '2.1.03')"));
        Assert.Equal(feriadosAntes, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM feriado"));
    }

    [Theory]
    [InlineData("gt")]
    [InlineData("G1")]
    [InlineData("1A")]
    public async Task Contraparte_ConPaisInvalido_ViolaElCheck(string pais)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO contraparte (tipo, nombre, pais) VALUES ('Proveedor', 'Pais invalido', $1)", pais));

        Assert.Equal("ck_contraparte_pais", ex.ConstraintName);
    }

    [Fact]
    public async Task Contraparte_ConPaisValidoOSinPais_SeInserta()
    {
        await _pg.Data.EjecutarAsync("INSERT INTO contraparte (tipo, nombre, pais) VALUES ('Proveedor', 'Con pais', 'US'), ('Proveedor', 'Sin pais', NULL)");

        Assert.Equal(1L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM contraparte WHERE nombre = 'Con pais' AND pais = 'US'"));
    }

    [Theory]
    [InlineData("SAT-9999", "B-1", 10, 0, "ck_enteramiento_formulario")]
    [InlineData("SAT-1352", "  ", 10, 0, "ck_enteramiento_boleta")]
    [InlineData("SAT-1352", "B-2", 0, 0, "ck_enteramiento_monto")]
    [InlineData("SAT-1352", "B-3", 10, -60, "ck_enteramiento_fecha_pago")]
    public async Task Enteramiento_ConValoresInconsistentes_ViolaElCheck(string formulario, string boleta, int monto, int diasFechaPago, string restriccion)
    {
        var e = await SembrarRetencionAsync(100m);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => EnterarAsync(e, monto, boleta, null, formulario, diasFechaPago));

        Assert.Equal(restriccion, ex.ConstraintName);
    }

    [Fact]
    public async Task Enteramiento_QueExcedeElSaldoDelPeriodo_SeRechaza()
    {
        var e = await SembrarRetencionAsync(100m);
        await EnterarAsync(e, 60m, $"B-{TestData.Sufijo()}");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => EnterarAsync(e, 50m, $"B-{TestData.Sufijo()}"));

        Assert.Equal(Estado55000, ex.SqlState);
        Assert.Contains("excede", ex.MessageText);
    }

    [Fact]
    public async Task Enteramiento_DePeriodoSinRetenciones_SeRechaza()
    {
        var e = await SembrarRetencionAsync(100m);
        var sinRetenciones = e with { Periodo = e.Periodo.AddMonths(2) };

        var ex = await Assert.ThrowsAsync<PostgresException>(() => EnterarAsync(sinRetenciones, 10m, $"B-{TestData.Sufijo()}"));

        Assert.Equal(Estado55000, ex.SqlState);
    }

    [Fact]
    public async Task Enteramiento_DesdeCuentaEnDolares_SeRechaza()
    {
        var e = await SembrarRetencionAsync(100m);
        var (bancoUsd, _) = await _pg.Data.CrearCuentaBancariaAsync("Monetaria", "USD");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => EnterarAsync(e, 10m, $"B-{TestData.Sufijo()}", cuentaBancariaPagoId: bancoUsd));

        Assert.Equal(Estado55000, ex.SqlState);
        Assert.Contains("quetzales", ex.MessageText);
    }

    [Fact]
    public async Task Enteramiento_ConBoletaRepetidaVigente_ViolaElIndiceUnico()
    {
        var e = await SembrarRetencionAsync(100m);
        var boleta = $"B-{TestData.Sufijo()}";
        await EnterarAsync(e, 10m, boleta);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => EnterarAsync(e, 10m, boleta));

        Assert.Equal("ux_enteramiento_boleta", ex.ConstraintName);
    }

    [Fact]
    public async Task Enteramiento_NoSeModificaNiSeElimina()
    {
        var e = await SembrarRetencionAsync(100m);
        var id = await EnterarAsync(e, 10m, $"B-{TestData.Sufijo()}");

        var modificar = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync("UPDATE enteramiento SET monto = 5 WHERE id = $1", id));
        var eliminar = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync("DELETE FROM enteramiento WHERE id = $1", id));

        Assert.Equal(Estado55000, modificar.SqlState);
        Assert.Equal(Estado55000, eliminar.SqlState);
    }

    [Fact]
    public async Task Enteramiento_Anulado_ReabreElSaldoYNoVuelveAVigente()
    {
        var e = await SembrarRetencionAsync(100m);
        var id = await EnterarAsync(e, 100m, $"B-{TestData.Sufijo()}");
        await AnularAsync(e, id);

        var saldo = await _pg.Data.ScalarAsync<decimal>("SELECT saldo FROM vw_saldo_enterar WHERE periodo = $1", e.Periodo);
        var revivir = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync("UPDATE enteramiento SET estado = 'VIGENTE' WHERE id = $1", id));
        await EnterarAsync(e, 100m, $"B-{TestData.Sufijo()}");

        Assert.Equal(100m, saldo);
        Assert.Equal(Estado55000, revivir.SqlState);
    }

    [Fact]
    public async Task Vistas_ReportanEnteradoYSaldoDelPeriodo()
    {
        var e = await SembrarRetencionAsync(100m);
        await EnterarAsync(e, 40m, $"B-{TestData.Sufijo()}");

        var enterado = await _pg.Data.ScalarAsync<decimal>("SELECT enterado_gtq FROM vw_retencion_reporte WHERE retencion_id = $1", e.RetencionId);
        var saldo = await _pg.Data.ScalarAsync<decimal>("SELECT saldo_por_enterar_gtq FROM vw_retencion_reporte WHERE retencion_id = $1", e.RetencionId);
        var pendiente = await _pg.Data.ScalarAsync<bool>("SELECT ajuste_pendiente FROM vw_retencion_reporte WHERE retencion_id = $1", e.RetencionId);

        Assert.Equal(40m, enterado);
        Assert.Equal(60m, saldo);
        Assert.False(pendiente);
    }

    private async Task<int> NotaCreditoAsync(Escenario e) =>
        await _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado, asiento_id, documento_origen_id) " +
            "VALUES ($1, 'NotaCredito', $2, $3, $3, 20, 20, TRUE, 'Vigente', $4, $5) RETURNING id",
            $"NC-{TestData.Sufijo()}", e.ProveedorId, e.Periodo.AddDays(12), e.AsientoDocumentoId, e.DocumentoId);

    [Fact]
    public async Task Ajuste_ConDocumentoQueNoEsNotaDeCreditoDelOrigen_SeRechaza()
    {
        var e = await SembrarRetencionAsync(100m);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO ajuste_retencion (retencion_id, nota_credito_id, monto_nc, creado_por) VALUES ($1, $2, 20, $3)",
            e.RetencionId, e.DocumentoId, e.Usuario));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task Ajuste_PendienteSeResuelveUnaVezYNoSeElimina()
    {
        var e = await SembrarRetencionAsync(100m);
        var nc = await NotaCreditoAsync(e);
        var id = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO ajuste_retencion (retencion_id, nota_credito_id, monto_nc, creado_por) VALUES ($1, $2, 20, $3) RETURNING id",
            e.RetencionId, nc, e.Usuario);

        var pendiente = await _pg.Data.ScalarAsync<bool>("SELECT ajuste_pendiente FROM vw_retencion_reporte WHERE retencion_id = $1", e.RetencionId);
        var sinMotivo = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "UPDATE ajuste_retencion SET estado = 'RESUELTO' WHERE id = $1", id));
        await _pg.Data.EjecutarAsync(
            "UPDATE ajuste_retencion SET estado = 'RESUELTO', motivo_resolucion = 'Rectificada', referencia_resolucion = 'RECT-1', resuelto_por = $2, resuelto_en = now() WHERE id = $1",
            id, e.Usuario);
        var eliminar = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync("DELETE FROM ajuste_retencion WHERE id = $1", id));

        Assert.True(pendiente);
        Assert.Equal("ck_ajuste_retencion_resolucion", sinMotivo.ConstraintName);
        Assert.Equal(Estado55000, eliminar.SqlState);
        Assert.False(await _pg.Data.ScalarAsync<bool>("SELECT ajuste_pendiente FROM vw_retencion_reporte WHERE retencion_id = $1", e.RetencionId));
    }

    [Fact]
    public async Task Rol_DeltaApi_TienePermisosMinimosSobreLasTablasNuevas()
    {
        var concedidos = await _pg.Data.ScalarAsync<bool>(
            "SELECT has_table_privilege('delta_api', 'enteramiento', 'SELECT, INSERT, UPDATE') " +
            "AND has_table_privilege('delta_api', 'ajuste_retencion', 'SELECT, INSERT, UPDATE') " +
            "AND has_table_privilege('delta_api', 'feriado', 'SELECT, INSERT, UPDATE, DELETE') " +
            "AND has_table_privilege('delta_api', 'vw_saldo_enterar', 'SELECT') " +
            "AND has_sequence_privilege('delta_api', 'enteramiento_id_seq', 'USAGE') " +
            "AND has_sequence_privilege('delta_api', 'ajuste_retencion_id_seq', 'USAGE')");
        var borrado = await _pg.Data.ScalarAsync<bool>(
            "SELECT has_table_privilege('delta_api', 'enteramiento', 'DELETE') OR has_table_privilege('delta_api', 'ajuste_retencion', 'DELETE')");

        Assert.True(concedidos);
        Assert.False(borrado);
    }
}
