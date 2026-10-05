-- Datos de demostración de tesorería aplicados manualmente a production (no los ejecuta run_migrations.sh).

-- 0. Guardia contra doble ejecución ------------------------------------------

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM cuentabancaria) THEN
        RAISE EXCEPTION 'Los datos demo de tesorería ya fueron aplicados.';
    END IF;
END $$;

-- 1. Cuentas contables de los bancos (hijas de Bancos) -------------------------

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT v.codigo, v.nombre, p.tipo, p.naturaleza, p.id
FROM (VALUES
    ('1.1.02.01', 'Bancos - BAC Monetaria',   '1.1.02'),
    ('1.1.02.02', 'Bancos - Banrural Ahorro', '1.1.02')
) AS v(codigo, nombre, padre)
JOIN cuentacontable p ON p.codigo = v.padre;

-- 2. Funciones auxiliares temporales (se descartan al final) --------------------

CREATE FUNCTION pg_temp.demo_tes_asiento(p_prefijo TEXT, p_fecha DATE, p_usuario INT, p_tipo TEXT,
                                         p_cuenta_banco INT, p_cuenta_contra INT, p_monto NUMERIC)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_periodo INT;
    v_id      INT;
    v_debito  BOOLEAN := (p_tipo = 'Ingreso');
BEGIN
    SELECT id INTO STRICT v_periodo FROM periodocontable
    WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin AND estado = 'Abierto';

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    VALUES (p_prefijo || '-' || to_char(p_fecha, 'YYYYMMDD') || '-' || substr(md5(random()::TEXT || clock_timestamp()::TEXT), 1, 8),
            p_fecha, v_periodo, p_monto, 'Confirmado', p_usuario)
    RETURNING id INTO v_id;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    VALUES (v_id, p_cuenta_banco, NULL, CASE WHEN v_debito THEN p_monto ELSE 0 END, CASE WHEN v_debito THEN 0 ELSE p_monto END),
           (v_id, p_cuenta_contra, NULL, CASE WHEN v_debito THEN 0 ELSE p_monto END, CASE WHEN v_debito THEN p_monto ELSE 0 END);
    RETURN v_id;
END $$;

CREATE FUNCTION pg_temp.demo_tes_cuenta(p_banco TEXT, p_numero TEXT, p_tipo TEXT, p_codigo TEXT,
                                        p_saldo NUMERIC, p_fecha DATE, p_email TEXT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_contable INT;
    v_capital INT;
    v_cuenta  INT;
    v_asiento INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id INTO STRICT v_contable FROM cuentacontable WHERE codigo = p_codigo;
    SELECT id INTO STRICT v_capital FROM cuentacontable WHERE codigo = '3.1.01';

    INSERT INTO cuentabancaria (banco, numero, tipo, cuenta_contable_id, saldo_apertura, fecha_apertura)
    VALUES (p_banco, p_numero, p_tipo, v_contable, p_saldo, p_fecha)
    RETURNING id INTO v_cuenta;

    v_asiento := pg_temp.demo_tes_asiento('TES-APE', p_fecha, v_usuario, 'Ingreso', v_contable, v_capital, p_saldo);
    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, origen)
    VALUES (v_cuenta, p_fecha, 'Ingreso', p_saldo, v_asiento, 'Saldo de apertura', 'Apertura');

    CALL sp_registrar_auditoria(v_usuario, 'crear_cuenta_bancaria', 'cuentabancaria',
        format('Cuenta bancaria %s (%s %s) creada, saldo de apertura %s', v_cuenta, p_banco, p_numero, p_saldo));
    RETURN v_cuenta;
END $$;

CREATE FUNCTION pg_temp.demo_tes_movimiento(p_numero_cuenta TEXT, p_fecha DATE, p_tipo TEXT, p_monto NUMERIC,
                                            p_descripcion TEXT, p_referencia TEXT, p_codigo_contra TEXT, p_email TEXT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_cuenta  INT;
    v_contable INT;
    v_asiento INT;
    v_id      INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id, cuenta_contable_id INTO STRICT v_cuenta, v_contable FROM cuentabancaria WHERE numero = p_numero_cuenta;

    v_asiento := pg_temp.demo_tes_asiento('TES-MAN', p_fecha, v_usuario, p_tipo, v_contable,
        (SELECT id FROM cuentacontable WHERE codigo = p_codigo_contra), p_monto);
    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, referencia, origen)
    VALUES (v_cuenta, p_fecha, p_tipo, p_monto, v_asiento, p_descripcion, p_referencia, 'Manual')
    RETURNING id INTO v_id;

    CALL sp_registrar_auditoria(v_usuario, 'registrar_movimiento_tesoreria', 'movimientotesoreria',
        format('Movimiento %s (%s %s) en cuenta %s', v_id, p_tipo, p_monto, p_numero_cuenta));
    RETURN v_id;
END $$;

CREATE FUNCTION pg_temp.demo_tes_transferencia(p_origen TEXT, p_destino TEXT, p_fecha DATE, p_monto NUMERIC,
                                               p_descripcion TEXT, p_referencia TEXT, p_email TEXT)
RETURNS UUID LANGUAGE plpgsql AS $$
DECLARE
    v_usuario  INT;
    v_cuenta_o INT;
    v_cuenta_d INT;
    v_contable_o INT;
    v_contable_d INT;
    v_asiento  INT;
    v_transf   UUID := gen_random_uuid();
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id, cuenta_contable_id INTO STRICT v_cuenta_o, v_contable_o FROM cuentabancaria WHERE numero = p_origen;
    SELECT id, cuenta_contable_id INTO STRICT v_cuenta_d, v_contable_d FROM cuentabancaria WHERE numero = p_destino;

    v_asiento := pg_temp.demo_tes_asiento('TES-TRF', p_fecha, v_usuario, 'Ingreso', v_contable_d, v_contable_o, p_monto);
    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, referencia, origen, transferencia_id)
    VALUES (v_cuenta_o, p_fecha, 'Egreso',  p_monto, v_asiento, p_descripcion, p_referencia, 'Transferencia', v_transf),
           (v_cuenta_d, p_fecha, 'Ingreso', p_monto, v_asiento, p_descripcion, p_referencia, 'Transferencia', v_transf);

    CALL sp_registrar_auditoria(v_usuario, 'registrar_transferencia_tesoreria', 'movimientotesoreria',
        format('Transferencia %s de cuenta %s a cuenta %s, monto %s', v_transf, p_origen, p_destino, p_monto));
    RETURN v_transf;
END $$;

-- p_aplicaciones: [numero_factura, monto_aplicado]
CREATE FUNCTION pg_temp.demo_tes_pago(p_sigla TEXT, p_tercero TEXT, p_fecha DATE, p_metodo TEXT, p_referencia TEXT,
                                      p_numero_cuenta TEXT, p_email TEXT, p_aplicaciones JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_es_cxc   BOOLEAN := (p_sigla = 'CxC');
    v_tipo     TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Cliente' ELSE 'Proveedor' END;
    v_usuario  INT;
    v_tercero  INT;
    v_cuenta   INT;
    v_contable INT;
    v_control  INT;
    v_monto    NUMERIC;
    v_pago     INT;
    v_asiento  INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id INTO STRICT v_tercero FROM contraparte WHERE tipo = v_tipo AND nombre = p_tercero;
    SELECT id, cuenta_contable_id INTO STRICT v_cuenta, v_contable FROM cuentabancaria WHERE numero = p_numero_cuenta;
    SELECT id INTO STRICT v_control FROM cuentacontable WHERE codigo = CASE WHEN v_es_cxc THEN '1.1.03' ELSE '2.1.01' END;
    SELECT SUM((e->>1)::NUMERIC) INTO v_monto FROM jsonb_array_elements(p_aplicaciones) e;

    IF v_es_cxc THEN
        INSERT INTO recibopagocliente (cliente_id, fecha, monto_total, metodo_pago, referencia_bancaria, cuenta_bancaria_id)
        VALUES (v_tercero, p_fecha, v_monto, p_metodo, p_referencia, v_cuenta)
        RETURNING id INTO v_pago;

        INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado)
        SELECT v_pago, (SELECT id FROM documentocxc WHERE numero = e->>0 AND cliente_id = v_tercero), (e->>1)::NUMERIC
        FROM jsonb_array_elements(p_aplicaciones) e;

        CALL sp_registrar_auditoria(v_usuario, 'registrar_pago_cxc', 'recibopagocliente',
            format('Recibo %s (cliente %s) registrado, monto %s', v_pago, p_tercero, v_monto));
    ELSE
        INSERT INTO pagoproveedorcabecera (proveedor_id, fecha, monto_total, metodo_pago, referencia_bancaria, cuenta_bancaria_id)
        VALUES (v_tercero, p_fecha, v_monto, p_metodo, p_referencia, v_cuenta)
        RETURNING id INTO v_pago;

        INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado)
        SELECT v_pago, (SELECT id FROM documentocxp WHERE numero = e->>0 AND proveedor_id = v_tercero), (e->>1)::NUMERIC
        FROM jsonb_array_elements(p_aplicaciones) e;

        CALL sp_registrar_auditoria(v_usuario, 'registrar_pago_cxp', 'pagoproveedorcabecera',
            format('Pago %s (proveedor %s) registrado, monto %s', v_pago, p_tercero, v_monto));
    END IF;

    v_asiento := pg_temp.demo_tes_asiento('TES-' || UPPER(p_sigla), p_fecha, v_usuario,
        CASE WHEN v_es_cxc THEN 'Ingreso' ELSE 'Egreso' END, v_contable, v_control, v_monto);
    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, referencia, origen,
                                     recibo_pago_id, pago_proveedor_id)
    VALUES (v_cuenta, p_fecha, CASE WHEN v_es_cxc THEN 'Ingreso' ELSE 'Egreso' END, v_monto, v_asiento,
            CASE WHEN v_es_cxc THEN 'Cobro ' ELSE 'Pago ' END || v_pago || ' - ' || p_tercero, p_referencia, p_sigla,
            CASE WHEN v_es_cxc THEN v_pago END, CASE WHEN v_es_cxc THEN NULL ELSE v_pago END);
    RETURN v_pago;
END $$;

-- Marca los movimientos de la cuenta con fecha en [p_desde, p_hasta] (sin apertura) y devuelve el id de la conciliación.
CREATE FUNCTION pg_temp.demo_tes_conciliacion(p_numero_cuenta TEXT, p_fecha DATE, p_saldo_extracto NUMERIC,
                                              p_desde DATE, p_hasta DATE)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_cuenta  INT;
    v_periodo INT;
    v_id      INT;
BEGIN
    SELECT id INTO STRICT v_cuenta FROM cuentabancaria WHERE numero = p_numero_cuenta;
    SELECT id INTO STRICT v_periodo FROM periodocontable WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin;

    INSERT INTO conciliacionbancaria (cuenta_bancaria_id, periodo_id, fecha, estado, saldo_extracto)
    VALUES (v_cuenta, v_periodo, p_fecha, 'Pendiente', p_saldo_extracto)
    RETURNING id INTO v_id;

    INSERT INTO detalleconciliacion (conciliacion_id, movimiento_id)
    SELECT v_id, m.id FROM movimientotesoreria m
    WHERE m.cuenta_bancaria_id = v_cuenta AND m.origen <> 'Apertura' AND m.fecha BETWEEN p_desde AND p_hasta
    ORDER BY m.fecha, m.id;
    RETURN v_id;
END $$;

-- 3. Cuentas bancarias con saldo de apertura ------------------------------------

SELECT pg_temp.demo_tes_cuenta('BAC', '40-123456-7', 'Monetaria', '1.1.02.01', 150000, '2026-09-01', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_cuenta('Banrural', '3341-88219-0', 'Ahorro', '1.1.02.02', 40000, '2026-09-01', 'contador@delta.com.gt');

-- 4. Movimientos manuales y transferencia ------------------------------------------

SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-09-03', 'Ingreso', 25000, 'Depósito de ventas de contado', 'DEP-501233', '4.1.01', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-09-05', 'Egreso', 35, 'Comisión bancaria mensual', NULL, '5.1.02', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-09-15', 'Ingreso', 12500, 'Depósito por servicios prestados', 'DEP-501987', '4.1.02', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-09-20', 'Egreso', 1850, 'Pago de energía eléctrica de oficina', 'DEBITO-EEGSA', '5.1.02', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_transferencia('40-123456-7', '3341-88219-0', '2026-09-25', 30000, 'Traslado a cuenta de ahorro', 'TRF-910045', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('3341-88219-0', '2026-09-30', 'Ingreso', 48.75, 'Intereses ganados del mes', NULL, '4.1.02', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-10-02', 'Egreso', 3200, 'Compra de papelería y útiles', 'CHQ-005301', '5.1.04', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('3341-88219-0', '2026-10-08', 'Egreso', 35, 'Comisión bancaria mensual', NULL, '5.1.02', 'contador@delta.com.gt');
SELECT pg_temp.demo_tes_movimiento('40-123456-7', '2026-10-12', 'Ingreso', 9000, 'Depósito de ventas de contado', 'DEP-502410', '4.1.01', 'contador@delta.com.gt');

-- 5. Cobros y pagos con cuenta bancaria ----------------------------------------------

SELECT pg_temp.demo_tes_pago('CxC', 'Colegio Santa Lucía, S.A.', '2026-09-12', 'Transferencia', 'TRF-903311', '40-123456-7', 'contador@delta.com.gt',
    '[["F-1003",6720]]');
SELECT pg_temp.demo_tes_pago('CxP', 'Servicios Informáticos Nova, S.A.', '2026-09-18', 'Transferencia', 'TRF-771801', '40-123456-7', 'contador@delta.com.gt',
    '[["P-2003",3000]]');
SELECT pg_temp.demo_tes_pago('CxC', 'Clínica Médica San Rafael, S.A.', '2026-10-06', 'Transferencia', 'TRF-904120', '40-123456-7', 'contador@delta.com.gt',
    '[["F-1006",2500]]');

-- 6. Conciliaciones --------------------------------------------------------------------

DO $$
DECLARE
    v_conciliacion INT;
    v_contador     INT;
BEGIN
    SELECT id INTO STRICT v_contador FROM usuario WHERE email = 'contador@delta.com.gt';

    v_conciliacion := pg_temp.demo_tes_conciliacion('40-123456-7', '2026-09-30', 159335, '2026-09-01', '2026-09-30');
    CALL sp_finalizar_conciliacion(v_conciliacion, v_contador);

    PERFORM pg_temp.demo_tes_conciliacion('40-123456-7', '2026-10-15', 167635, '2026-10-01', '2026-10-10');
END $$;

DROP FUNCTION pg_temp.demo_tes_conciliacion(TEXT, DATE, NUMERIC, DATE, DATE);
DROP FUNCTION pg_temp.demo_tes_pago(TEXT, TEXT, DATE, TEXT, TEXT, TEXT, TEXT, JSONB);
DROP FUNCTION pg_temp.demo_tes_transferencia(TEXT, TEXT, DATE, NUMERIC, TEXT, TEXT, TEXT);
DROP FUNCTION pg_temp.demo_tes_movimiento(TEXT, DATE, TEXT, NUMERIC, TEXT, TEXT, TEXT, TEXT);
DROP FUNCTION pg_temp.demo_tes_cuenta(TEXT, TEXT, TEXT, TEXT, NUMERIC, DATE, TEXT);
DROP FUNCTION pg_temp.demo_tes_asiento(TEXT, DATE, INT, TEXT, INT, INT, NUMERIC);

-- 7. Verificación -------------------------------------------------------------------------

SELECT 'cuentabancaria' AS tabla, COUNT(*) AS filas FROM cuentabancaria
UNION ALL SELECT 'movimientotesoreria', COUNT(*) FROM movimientotesoreria
UNION ALL SELECT 'conciliacionbancaria', COUNT(*) FROM conciliacionbancaria
UNION ALL SELECT 'detalleconciliacion', COUNT(*) FROM detalleconciliacion
UNION ALL SELECT 'recibopagocliente con banco', COUNT(*) FROM recibopagocliente WHERE cuenta_bancaria_id IS NOT NULL
UNION ALL SELECT 'pagoproveedorcabecera con banco', COUNT(*) FROM pagoproveedorcabecera WHERE cuenta_bancaria_id IS NOT NULL;

SELECT * FROM vw_saldocuentabancaria;

SELECT conciliacion_id, cuenta_bancaria_id, fecha, estado, saldo_inicial, total_marcado, saldo_extracto, diferencia, cantidad_movimientos
FROM vw_conciliacion_resumen ORDER BY conciliacion_id;

SELECT SUM(total_debito) AS total_debito, SUM(total_credito) AS total_credito,
       SUM(total_debito) = SUM(total_credito) AS balanceado
FROM vw_balance_saldos;
