using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class MultimonedaEsquemaTests
{
    private const decimal TasaUsd = 7.64157m;
    private const string SqlStateRegla = "55000";
    private const string SqlStateCheck = "23514";
    private const string SqlStateUnico = "23505";
    private const string SqlStateExcepcion = "P0001";

    private const string SqlDocumentoLegado =
        "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado) " +
        "VALUES ($1, 'Factura', $2, '2025-03-15', '2025-03-15', 100, $3, $4, 'Vigente') RETURNING id";

    private readonly PostgresFixture _pg;

    public MultimonedaEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private Task<int> MonedaAsync(string codigo) => _pg.Data.ScalarAsync<int>("SELECT id FROM moneda WHERE codigo = $1", codigo);

    private Task<int> DocumentoCxCAsync(
        int clienteId, DateOnly fecha, decimal totalGtq, int monedaId, decimal original, decimal tasa, int? tipoCambioId,
        int? origenId = null, string tipo = "Factura") =>
        _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado, " +
            "moneda_id, monto_total_original, tipo_cambio_aplicado, tipo_cambio_id, documento_origen_id) " +
            "VALUES ($1, $2, $3, $4, $4, $5, $5, TRUE, 'Vigente', $6, $7, $8, $9::int, $10::int) RETURNING id",
            $"F-{TestData.Sufijo()}", tipo, clienteId, fecha, totalGtq, monedaId, original, tasa,
            (object?)tipoCambioId ?? DBNull.Value, (object?)origenId ?? DBNull.Value);

    private Task<int> DocumentoCxPAsync(int proveedorId, DateOnly fecha) =>
        _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado) " +
            "VALUES ($1, 'Factura', $2, $3, $3, 100, 100, TRUE, 'Vigente') RETURNING id",
            $"P-{TestData.Sufijo()}", proveedorId, fecha);

    private Task<int> ReciboAsync(int clienteId, DateOnly fecha) =>
        _pg.Data.ScalarAsync<int>(
            "INSERT INTO recibopagocliente (cliente_id, fecha, monto_total, metodo_pago) VALUES ($1, $2, 1, 'Efectivo') RETURNING id", clienteId, fecha);

    private Task<int> PagoProveedorAsync(int proveedorId, DateOnly fecha) =>
        _pg.Data.ScalarAsync<int>(
            "INSERT INTO pagoproveedorcabecera (proveedor_id, fecha, monto_total, metodo_pago) VALUES ($1, $2, 1, 'Transferencia') RETURNING id",
            proveedorId, fecha);

    private Task AplicacionAsync(int reciboId, int documentoId, decimal monto, decimal montoGtq, decimal valorGtq) =>
        _pg.Data.EjecutarAsync(
            "INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq) " +
            "VALUES ($1, $2, $3, $4, $5, $5 - $4)", reciboId, documentoId, monto, montoGtq, valorGtq);

    private Task<T> SaldoCxCAsync<T>(string columna, int documentoId) =>
        _pg.Data.ScalarAsync<T>($"SELECT {columna} FROM vw_saldodocumentocxc WHERE documento_id = $1", documentoId);

    private Task InsertarMovimientoAsync(
        int cuentaBancariaId, int asientoId, DateOnly fecha, decimal monto, decimal montoGtq, decimal? tipoCambio, int? tipoCambioId) =>
        _pg.Data.EjecutarAsync(
            "INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen, monto_gtq, tipo_cambio, tipo_cambio_id) " +
            "VALUES ($1, $2, 'Ingreso', $3, $4, 'Movimiento de prueba', 'Manual', $5, $6::numeric, $7::int)",
            cuentaBancariaId, fecha, monto, asientoId, montoGtq, (object?)tipoCambio ?? DBNull.Value, (object?)tipoCambioId ?? DBNull.Value);

    private Task<int> RevaluacionAsync(
        int monedaId, int anio, int mes, DateOnly cierre, int tipoCambioId, decimal ganancia, decimal perdida,
        string estado, int asientoId, int asientoReversaId, int usuarioId) =>
        _pg.Data.ScalarAsync<int>(
            "INSERT INTO revaluacion (moneda_id, anio, mes, fecha_cierre, tipo_cambio_id, tasa, total_ganancia, total_perdida, estado, asiento_id, asiento_reversa_id, creado_por) " +
            "VALUES ($1, $2, $3, $4, $5, 7.65, $6, $7, $8, $9, $10, $11) RETURNING id",
            monedaId, anio, mes, cierre, tipoCambioId, ganancia, perdida, estado, asientoId, asientoReversaId, usuarioId);

    private async Task<(int Cliente, DateOnly Fecha, int Usd, int Tc)> PrepararCxCUsdAsync()
    {
        await _pg.Data.CrearUsuarioAsync();
        var cliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var fecha = TestData.SiguienteDiaHabil();
        var usd = await MonedaAsync("USD");
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", fecha, TasaUsd);
        return (cliente, fecha, usd, tc);
    }

    private async Task<(int Original, int Reversa)> SembrarAsientosRevaluacionAsync(EscenarioTesoreria e, DateOnly fecha)
    {
        var cuentaA = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");
        var cuentaB = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var original = await _pg.Data.SembrarAsientoAsync(e.Periodo, e.Usuario, cuentaA, cuentaB, 10m, fecha);
        var reversa = await _pg.Data.SembrarAsientoAsync(e.Periodo, e.Usuario, cuentaB, cuentaA, 10m, fecha);
        return (original, reversa);
    }

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("EUR", 24)]
    [InlineData("GTQ", 0)]
    public async Task Monedas_CodigoBanguatCorrespondeAlCatalogo(string codigo, int codigoBanguat)
    {
        var real = await _pg.Data.ScalarAsync<int>("SELECT COALESCE(codigo_banguat, 0)::int FROM moneda WHERE codigo = $1", codigo);

        Assert.Equal(codigoBanguat, real);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    public async Task Monedas_ExtranjerasActivasYNoFuncionales(string codigo)
    {
        var activaYNoFuncional = await _pg.Data.ScalarAsync<bool>("SELECT activa AND NOT es_funcional FROM moneda WHERE codigo = $1", codigo);

        Assert.True(activaYNoFuncional);
    }

    [Fact]
    public async Task Moneda_FuncionalConCodigoBanguat_ViolaElCheck()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE moneda SET codigo_banguat = 99 WHERE es_funcional"));

        Assert.Equal("ck_moneda_funcional_sin_banguat", ex.ConstraintName);
    }

    [Fact]
    public async Task Moneda_NoSeEliminaFisicamente()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM moneda WHERE codigo = 'EUR'"));

        Assert.Equal(SqlStateExcepcion, ex.SqlState);
        Assert.Contains("no se elimina físicamente", ex.MessageText);
    }

    [Fact]
    public async Task Tasa_UnaFilaActivaPorMonedaYFecha()
    {
        var fecha = TestData.SiguienteSemana();
        var usd = await MonedaAsync("USD");
        await _pg.Data.SembrarTipoCambioAsync("USD", fecha, TasaUsd);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.SembrarTipoCambioAsync("USD", fecha, 7.65m));

        Assert.Equal(SqlStateUnico, ex.SqlState);
        Assert.Equal("ux_historialtipocambio_activa", ex.ConstraintName);
        await _pg.Data.EjecutarAsync(
            "INSERT INTO historialtipocambio (moneda_id, fecha, tasa, fuente, fecha_referencia, activa, usuario_id) " +
            "VALUES ($1, $2, 7.66, 'Banguat', $2, FALSE, (SELECT MIN(id) FROM usuario))",
            usd, fecha);
    }

    [Theory]
    [InlineData("0", "'Banguat'", "NULL", "$1", "NULL", "ck_historialtipocambio_tasa")]
    [InlineData("7.6", "'Otra'", "NULL", "$1", "NULL", "ck_historialtipocambio_fuente")]
    [InlineData("7.6", "'Manual'", "NULL", "$1", "NULL", "ck_historialtipocambio_manual")]
    [InlineData("7.6", "'Banguat'", "NULL", "$1::date + 1", "NULL", "ck_historialtipocambio_referencia")]
    [InlineData("7.6", "'Banguat'", "NULL", "$1", "1.12", "ck_historialtipocambio_eur")]
    public async Task Tasa_ChecksDeTasaFuenteReferenciaManualYEur(
        string tasa, string fuente, string motivo, string referencia, string cotizacionVenta, string constraint)
    {
        var fecha = TestData.SiguienteSemana();
        var usd = await MonedaAsync("USD");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO historialtipocambio (moneda_id, fecha, tasa, fuente, fecha_referencia, motivo, usuario_id, cotizacion_venta) " +
            $"VALUES ($2, $1, {tasa}, {fuente}, {referencia}, {motivo}, (SELECT MIN(id) FROM usuario), {cotizacionVenta})",
            fecha, usd));

        Assert.Equal(constraint, ex.ConstraintName);
    }

    [Fact]
    public async Task Tasa_MonedaFuncional_LaRechazaElTrigger()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.SembrarTipoCambioAsync("GTQ", TestData.SiguienteSemana(), 1m));

        Assert.Equal(SqlStateRegla, ex.SqlState);
    }

    [Fact]
    public async Task Tasa_Inmutable_SoloAdmiteBaja()
    {
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", TestData.SiguienteSemana(), TasaUsd);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET tasa = tasa + 1 WHERE id = $1", tc));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("solo puede darse de baja", ex.MessageText);

        await _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET activa = FALSE WHERE id = $1", tc);
        var reactivar = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET activa = TRUE WHERE id = $1", tc));
        Assert.Equal(SqlStateRegla, reactivar.SqlState);
    }

    [Fact]
    public async Task Tasa_UsadaPorDocumento_NoSePuedeDarDeBaja()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET activa = FALSE WHERE id = $1", tc));

        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("ya fue usada", ex.MessageText);
    }

    [Fact]
    public async Task Tasa_UsadaPorMovimiento_NoSePuedeDarDeBaja()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var cuenta = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", e.Inicio, TasaUsd);
        var asiento = await _pg.Data.SembrarAsientoAsync(e.Periodo, e.Usuario, cuenta.CuentaContableId, e.Contrapartida, 100m, e.Inicio);
        await InsertarMovimientoAsync(cuenta.Id, asiento, e.Inicio, 100m, 764.16m, TasaUsd, tc);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET activa = FALSE WHERE id = $1", tc));

        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("ya fue usada", ex.MessageText);
    }

    [Fact]
    public async Task Tasa_NoSeEliminaFisicamente()
    {
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", TestData.SiguienteSemana(), TasaUsd);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM historialtipocambio WHERE id = $1", tc));

        Assert.Equal(SqlStateExcepcion, ex.SqlState);
    }

    [Theory]
    [InlineData(100d, 7.5d, false)]
    [InlineData(100d, 1d, true)]
    [InlineData(101d, 1d, false)]
    public async Task Documento_Funcional_RechazaTasaRegistradaOOriginalDistinto(double original, double tasa, bool conTipoCambio)
    {
        var (cliente, fecha, _, tc) = await PrepararCxCUsdAsync();
        var gtq = await MonedaAsync("GTQ");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 100m, gtq, (decimal)original, (decimal)tasa, conTipoCambio ? tc : (int?)null));

        Assert.Equal(SqlStateCheck, ex.SqlState);
    }

    [Fact]
    public async Task Documento_MonedaExtranjera_ValidaMonedaTasaFechaYFilaActiva()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var eur = await _pg.Data.SembrarTipoCambioAsync("EUR", fecha, 8.5m);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, null));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, eur));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 750m, usd, 100m, 7.5m, tc));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha.AddDays(1), 764.16m, usd, 100m, TasaUsd, tc));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        var fechaBaja = TestData.SiguienteSemana();
        var tcBaja = await _pg.Data.SembrarTipoCambioAsync("USD", fechaBaja, TasaUsd);
        await _pg.Data.EjecutarAsync("UPDATE historialtipocambio SET activa = FALSE WHERE id = $1", tcBaja);
        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fechaBaja, 764.16m, usd, 100m, TasaUsd, tcBaja));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        var documento = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        Assert.True(await _pg.Data.ScalarAsync<bool>("SELECT tipo_cambio_fuente IS NULL FROM documentocxc WHERE id = $1", documento));
        Assert.False(await _pg.Data.ScalarAsync<bool>("SELECT tipo_cambio_arrastrado FROM documentocxc WHERE id = $1", documento));
    }

    [Fact]
    public async Task Documento_ConAplicaciones_NoPermiteCambiarMonedaTasaNiOriginal()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        var recibo = await ReciboAsync(cliente, fecha);
        await AplicacionAsync(recibo, factura, 40m, 305.66m, 305.66m);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE documentocxc SET monto_total_original = monto_total_original + 1 WHERE id = $1", factura));
        Assert.Equal(SqlStateRegla, ex.SqlState);

        var sinAplicaciones = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        await _pg.Data.EjecutarAsync("UPDATE documentocxc SET monto_total_original = monto_total_original + 1 WHERE id = $1", sinAplicaciones);
    }

    [Fact]
    public async Task NotaCredito_ConMonedaOTasaDistintaDeSuFactura_Responde55000()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        var gtq = await MonedaAsync("GTQ");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 10m, gtq, 10m, 1m, null, factura, "NotaCredito"));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("debe usar la moneda y el tipo de cambio de su factura", ex.MessageText);

        await DocumentoCxCAsync(cliente, fecha.AddDays(1), 76.42m, usd, 10m, TasaUsd, tc, factura, "NotaCredito");
    }

    [Fact]
    public async Task NotaCredito_ExcedeElSaldoEnMonedaOriginal()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 764.23m, usd, 100.01m, TasaUsd, tc, factura, "NotaCredito"));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("(USD 100.01)", ex.MessageText);
        Assert.Contains("(USD 100.00)", ex.MessageText);

        var gtq = await MonedaAsync("GTQ");
        var facturaGtq = await DocumentoCxCAsync(cliente, fecha, 100m, gtq, 100m, 1m, null);
        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            DocumentoCxCAsync(cliente, fecha, 110m, gtq, 110m, 1m, null, facturaGtq, "NotaCredito"));
        Assert.Contains("(Q110.00)", ex.MessageText);
        Assert.Contains("(Q100.00)", ex.MessageText);
    }

    [Fact]
    public async Task Aplicacion_DiferencialDebeSerValorMenosLibros()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        var recibo = await ReciboAsync(cliente, fecha);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq) " +
            "VALUES ($1, $2, 100, 764.16, 800, 0)",
            recibo, factura));
        Assert.Equal("ck_aplicacionpagocliente_diferencial", ex.ConstraintName);

        var proveedor = await _pg.Data.CrearContraparteAsync("Proveedor");
        var documentoCxp = await DocumentoCxPAsync(proveedor, fecha);
        var pago = await PagoProveedorAsync(proveedor, fecha);

        ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq) " +
            "VALUES ($1, $2, 100, 764.16, 800, 35.84)",
            pago, documentoCxp));
        Assert.Equal("ck_aplicacionpagoproveedor_diferencial", ex.ConstraintName);

        await _pg.Data.EjecutarAsync(
            "INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq) " +
            "VALUES ($1, $2, 100, 764.16, 800, -35.84)",
            pago, documentoCxp);
    }

    [Fact]
    public async Task LimiteDePago_CierraElSaldoEnGtqJuntoConElOriginal()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        var recibo = await ReciboAsync(cliente, fecha);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AplicacionAsync(recibo, factura, 100m, 764.00m, 764.00m));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("RN-26", ex.MessageText);

        ex = await Assert.ThrowsAsync<PostgresException>(() => AplicacionAsync(recibo, factura, 33.33m, 764.16m, 764.16m));
        Assert.Equal(SqlStateRegla, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() => AplicacionAsync(recibo, factura, 50m, 800m, 800m));
        Assert.Equal(SqlStateRegla, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() => AplicacionAsync(recibo, factura, 100.01m, 764.16m, 764.16m));
        Assert.Contains("RN-05", ex.MessageText);

        await AplicacionAsync(recibo, factura, 100m, 764.16m, 764.16m);
    }

    [Fact]
    public async Task LimiteDePago_ResiduoDeCentavos_R9()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 99.98m, TasaUsd, tc, factura, "NotaCredito");
        var recibo = await ReciboAsync(cliente, fecha);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AplicacionAsync(recibo, factura, 0.01m, 0m, 0m));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("RN-26", ex.MessageText);

        await AplicacionAsync(recibo, factura, 0.02m, 0m, 0m);
    }

    [Fact]
    public async Task CuentaBancaria_Moneda_InmutableConMovimientos()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var usd = await MonedaAsync("USD");
        await _pg.Data.CrearMovimientoAsync(
            new(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida), "Ingreso", 100m, e.Inicio, e.Periodo, e.Usuario);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET moneda_id = $1 WHERE id = $2", usd, e.CuentaBancaria));
        Assert.Equal(SqlStateRegla, ex.SqlState);

        var sinMovimientos = await _pg.Data.CrearCuentaBancariaAsync();
        await _pg.Data.EjecutarAsync("UPDATE cuentabancaria SET moneda_id = $1 WHERE id = $2", usd, sinMovimientos.Id);
    }

    [Fact]
    public async Task Movimiento_CuentaMonedaExtranjera_ExigeTasaCoherente()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var cuentaUsd = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");
        var tcUsd = await _pg.Data.SembrarTipoCambioAsync("USD", e.Inicio, TasaUsd);
        var tcEur = await _pg.Data.SembrarTipoCambioAsync("EUR", e.Inicio, 8.5m);
        var asientoUsd = await _pg.Data.SembrarAsientoAsync(e.Periodo, e.Usuario, cuentaUsd.CuentaContableId, e.Contrapartida, 100m, e.Inicio);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarMovimientoAsync(cuentaUsd.Id, asientoUsd, e.Inicio, 100m, 764.16m, TasaUsd, null));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarMovimientoAsync(cuentaUsd.Id, asientoUsd, e.Inicio, 100m, 764.16m, TasaUsd, tcEur));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarMovimientoAsync(cuentaUsd.Id, asientoUsd, e.Inicio, 100m, 750m, 7.5m, tcUsd));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        await InsertarMovimientoAsync(cuentaUsd.Id, asientoUsd, e.Inicio, 100m, 764.16m, TasaUsd, tcUsd);

        var asientoGtq = await _pg.Data.SembrarAsientoAsync(e.Periodo, e.Usuario, e.CuentaContableBanco, e.Contrapartida, 100m, e.Inicio);
        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarMovimientoAsync(e.CuentaBancaria, asientoGtq, e.Inicio, 100m, 100m, 7.5m, null));
        Assert.Equal(SqlStateCheck, ex.SqlState);

        ex = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarMovimientoAsync(e.CuentaBancaria, asientoGtq, e.Inicio, 100m, 99m, null, null));
        Assert.Equal(SqlStateCheck, ex.SqlState);
    }

    [Fact]
    public async Task SpReversarAsiento_RechazaLosAsientosDeRevaluacion()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var usd = await MonedaAsync("USD");
        var cierre = new DateOnly(e.Inicio.Year, 10, 31);
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", cierre, 7.65m);
        var (original, reversa) = await SembrarAsientosRevaluacionAsync(e, cierre);
        await RevaluacionAsync(usd, e.Inicio.Year, 10, cierre, tc, 10m, 0m, "Vigente", original, reversa, e.Usuario);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.ReversarAsientoAsync(original));
        Assert.Equal(SqlStateRegla, ex.SqlState);
        Assert.Contains("pertenece a una revaluación", ex.MessageText);

        ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.ReversarAsientoAsync(reversa));
        Assert.Contains("pertenece a una revaluación", ex.MessageText);
    }

    [Theory]
    [InlineData(0d, 0d, 10, "Vigente", "ck_revaluacion_totales")]
    [InlineData(10d, 0d, 13, "Vigente", "ck_revaluacion_mes")]
    [InlineData(10d, 0d, 10, "Anulada", "ck_revaluacion_anulacion")]
    public async Task Revaluacion_Restricciones(double ganancia, double perdida, int mes, string estado, string constraint)
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var usd = await MonedaAsync("USD");
        var cierre = new DateOnly(e.Inicio.Year, 10, 31);
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", cierre, 7.65m);
        var (original, reversa) = await SembrarAsientosRevaluacionAsync(e, cierre);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            RevaluacionAsync(usd, e.Inicio.Year, mes, cierre, tc, (decimal)ganancia, (decimal)perdida, estado, original, reversa, e.Usuario));

        Assert.Equal(constraint, ex.ConstraintName);
    }

    [Fact]
    public async Task Revaluacion_VigenteUnicaPorMonedaYMes()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var usd = await MonedaAsync("USD");
        var cierre = new DateOnly(e.Inicio.Year, 10, 31);
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", cierre, 7.65m);
        var (original, reversa) = await SembrarAsientosRevaluacionAsync(e, cierre);
        await RevaluacionAsync(usd, e.Inicio.Year, 10, cierre, tc, 10m, 0m, "Vigente", original, reversa, e.Usuario);
        var (otroOriginal, otraReversa) = await SembrarAsientosRevaluacionAsync(e, cierre);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            RevaluacionAsync(usd, e.Inicio.Year, 10, cierre, tc, 10m, 0m, "Vigente", otroOriginal, otraReversa, e.Usuario));

        Assert.Equal(SqlStateUnico, ex.SqlState);
        Assert.Equal("ux_revaluacion_vigente", ex.ConstraintName);
    }

    [Fact]
    public async Task RevaluacionPartida_Restricciones()
    {
        var e = await _pg.Data.SembrarTesoreriaAsync();
        var usd = await MonedaAsync("USD");
        var cierre = new DateOnly(e.Inicio.Year, 10, 31);
        var tc = await _pg.Data.SembrarTipoCambioAsync("USD", cierre, 7.65m);
        var (original, reversa) = await SembrarAsientosRevaluacionAsync(e, cierre);
        var revaluacion = await RevaluacionAsync(usd, e.Inicio.Year, 10, cierre, tc, 10m, 0m, "Vigente", original, reversa, e.Usuario);
        var cuentaBancaria = await _pg.Data.CrearCuentaBancariaAsync(moneda: "USD");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO revaluacionpartida (revaluacion_id, tipo, cuenta_id, saldo_original, valor_libros_gtq, valor_cierre_gtq, ajuste_gtq) " +
            "VALUES ($1, 'CxC', $2, 100, 764.16, 770, 5.84)",
            revaluacion, cuentaBancaria.CuentaContableId));
        Assert.Equal("ck_revaluacionpartida_origen", ex.ConstraintName);

        ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO revaluacionpartida (revaluacion_id, tipo, cuenta_bancaria_id, cuenta_id, saldo_original, valor_libros_gtq, valor_cierre_gtq, ajuste_gtq) " +
            "VALUES ($1, 'Banco', $2, $3, 100, 764.16, 770, 5)",
            revaluacion, cuentaBancaria.Id, cuentaBancaria.CuentaContableId));
        Assert.Equal("ck_revaluacionpartida_ajuste", ex.ConstraintName);
    }

    [Fact]
    public async Task Vistas_DeSaldo_ExponenSaldoOriginalYGtq()
    {
        var (cliente, fecha, usd, tc) = await PrepararCxCUsdAsync();
        var factura = await DocumentoCxCAsync(cliente, fecha, 764.16m, usd, 100m, TasaUsd, tc);
        var nota = await DocumentoCxCAsync(cliente, fecha, 76.42m, usd, 10m, TasaUsd, tc, factura, "NotaCredito");
        var recibo = await ReciboAsync(cliente, fecha);
        await AplicacionAsync(recibo, factura, 40m, 305.66m, 305.66m);

        Assert.Equal(50m, await SaldoCxCAsync<decimal>("saldo_pendiente", factura));
        Assert.Equal(382.08m, await SaldoCxCAsync<decimal>("saldo_pendiente_gtq", factura));
        Assert.Equal(usd, await SaldoCxCAsync<int>("moneda_id", factura));
        Assert.Equal(100m, await SaldoCxCAsync<decimal>("monto_total_original", factura));
        Assert.Equal(764.16m, await SaldoCxCAsync<decimal>("monto_total", factura));
        Assert.Equal(0m, await SaldoCxCAsync<decimal>("saldo_pendiente", nota));
        Assert.Equal(0m, await SaldoCxCAsync<decimal>("saldo_pendiente_gtq", nota));
    }

    [Theory]
    [InlineData("vw_saldodocumentocxc", "documento_id,monto_total,saldo_pendiente,moneda_id,monto_total_original,saldo_pendiente_gtq")]
    [InlineData("vw_saldodocumentocxp", "documento_id,monto_total,saldo_pendiente,moneda_id,monto_total_original,saldo_pendiente_gtq")]
    [InlineData("vw_saldocuentabancaria", "cuenta_bancaria_id,banco,numero,saldo,moneda_id")]
    public async Task Vistas_ConservanElOrdenDeColumnas(string vista, string columnas)
    {
        var real = await _pg.Data.ScalarAsync<string>(
            "SELECT string_agg(column_name, ',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name = $1", vista);

        Assert.Equal(columnas, real);
    }

    [Fact]
    public async Task InsertLegado_SinLasColumnasNuevas_QuedaEnMonedaFuncional()
    {
        await _pg.Data.CrearUsuarioAsync();
        var cliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var gtq = await MonedaAsync("GTQ");
        var fecha = new DateOnly(2025, 3, 15);

        var documento = await _pg.Data.ScalarAsync<int>(SqlDocumentoLegado, $"F-{TestData.Sufijo()}", cliente, 100m, true);
        Assert.Equal(gtq, await _pg.Data.ScalarAsync<int>("SELECT moneda_id FROM documentocxc WHERE id = $1", documento));
        Assert.Equal(100m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_total_original FROM documentocxc WHERE id = $1", documento));
        Assert.Equal(1m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio_aplicado FROM documentocxc WHERE id = $1", documento));

        var documentoNulo = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado, tipo_cambio_aplicado) " +
            "VALUES ($1, 'Factura', $2, '2025-03-15', '2025-03-15', 100, 100, TRUE, 'Vigente', NULL) RETURNING id",
            $"F-{TestData.Sufijo()}", cliente);
        Assert.Equal(1m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio_aplicado FROM documentocxc WHERE id = $1", documentoNulo));

        var recibo = await ReciboAsync(cliente, fecha);
        Assert.True(await _pg.Data.ScalarAsync<bool>("SELECT monto_total_gtq = monto_total FROM recibopagocliente WHERE id = $1", recibo));

        var aplicacion = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado) VALUES ($1, $2, 1) RETURNING id", recibo, documento);
        Assert.True(await _pg.Data.ScalarAsync<bool>(
            "SELECT monto_aplicado_gtq = monto_aplicado AND valor_gtq = monto_aplicado FROM aplicacionpagocliente WHERE id = $1", aplicacion));

        var e = await _pg.Data.SembrarTesoreriaAsync();
        var movimiento = await _pg.Data.CrearMovimientoAsync(
            new(e.CuentaBancaria, e.CuentaContableBanco, e.Contrapartida), "Ingreso", 100m, e.Inicio, e.Periodo, e.Usuario);
        Assert.Equal(100m, await _pg.Data.ScalarAsync<decimal>("SELECT monto_gtq FROM movimientotesoreria WHERE id = $1", movimiento));
        Assert.Equal(1m, await _pg.Data.ScalarAsync<decimal>("SELECT tipo_cambio FROM movimientotesoreria WHERE id = $1", movimiento));

        var cuenta = await _pg.Data.CrearCuentaBancariaAsync();
        Assert.True(await _pg.Data.ScalarAsync<bool>("SELECT moneda_id = $1 FROM cuentabancaria WHERE id = $2", gtq, cuenta.Id));
    }

    [Fact]
    public async Task ConfiguracionFiscal_CuentasCambiariasDuplicadas_ViolanElCheck()
    {
        var cuentaA = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var cuentaB = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        var cuentaC = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var cuentaD = await _pg.Data.CrearCuentaAsync("Gasto", "Deudora");
        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
                "UPDATE configuracionfiscal SET cuenta_dif_ganancia_realizada_id = $1, cuenta_dif_perdida_realizada_id = $1 WHERE id = 1",
                cuentaA));
            Assert.Equal("ck_configuracionfiscal_cuentas_cambiarias", ex.ConstraintName);

            ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
                "UPDATE configuracionfiscal SET cuenta_reval_ganancia_id = cuenta_iva_debito_id WHERE id = 1"));
            Assert.Equal("ck_configuracionfiscal_cuentas_cambiarias", ex.ConstraintName);

            await _pg.Data.ConfigurarCuentasCambiariasAsync(cuentaA, cuentaB, cuentaC, cuentaD);
        }
        finally
        {
            await _pg.Data.RestaurarConfiguracionFiscalAsync();
        }
    }
}
