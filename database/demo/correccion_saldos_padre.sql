-- Reclasifica el saldo propio de cuentas de mayor a subcuentas hoja. Se aplica antes de 10_balance_jerarquico.sql; run_migrations.sh no lo ejecuta.

BEGIN;

-- 1. Subcuenta hoja para el saldo propio de Bancos ----------------------------

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT '1.1.02.03', 'Bancos - Otros', p.tipo, p.naturaleza, p.id
FROM cuentacontable p
WHERE p.codigo = '1.1.02'
  AND NOT EXISTS (SELECT 1 FROM cuentacontable WHERE codigo = '1.1.02.03');

-- 2. Función auxiliar temporal (se descarta al final) -----------------------------

CREATE FUNCTION pg_temp.corr_reclasificar(p_origen TEXT, p_destino TEXT, p_fecha DATE, p_email TEXT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_origen  INT;
    v_destino INT;
    v_periodo INT;
    v_neto    NUMERIC;
    v_monto   NUMERIC;
    v_id      INT;
BEGIN
    SELECT id INTO v_origen FROM cuentacontable WHERE codigo = p_origen;
    IF v_origen IS NULL THEN
        RAISE NOTICE 'La cuenta % no existe; se omite.', p_origen;
        RETURN NULL;
    END IF;
    SELECT id INTO STRICT v_destino FROM cuentacontable WHERE codigo = p_destino;
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;

    SELECT COALESCE(SUM(l.debito - l.credito), 0) INTO v_neto
    FROM lineaasiento l
    JOIN asientocontable a ON a.id = l.asiento_id AND a.estado IN ('Confirmado', 'Anulado')
    WHERE l.cuenta_id = v_origen;

    IF v_neto = 0 THEN
        RETURN NULL;
    END IF;
    v_monto := abs(v_neto);

    SELECT id INTO STRICT v_periodo FROM periodocontable
    WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin AND estado = 'Abierto';

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    VALUES ('AJU-' || to_char(p_fecha, 'YYYYMMDD') || '-' || substr(md5(random()::TEXT || clock_timestamp()::TEXT), 1, 8),
            p_fecha, v_periodo, v_monto, 'Confirmado', v_usuario)
    RETURNING id INTO v_id;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    VALUES (v_id, v_destino, NULL, CASE WHEN v_neto > 0 THEN v_monto ELSE 0 END, CASE WHEN v_neto > 0 THEN 0 ELSE v_monto END),
           (v_id, v_origen,  NULL, CASE WHEN v_neto > 0 THEN 0 ELSE v_monto END, CASE WHEN v_neto > 0 THEN v_monto ELSE 0 END);

    CALL sp_registrar_auditoria(v_usuario, 'reclasificar_saldo_cuenta', 'asientocontable',
        format('Asiento %s: saldo propio de %s (%s) reclasificado a %s', v_id, p_origen, v_neto, p_destino));
    RETURN v_id;
END $$;

-- 3. Reclasificaciones ----------------------------------------------------------------

SELECT pg_temp.corr_reclasificar('1.1.02', '1.1.02.03', '2026-10-31', 'contador@delta.com.gt');
SELECT pg_temp.corr_reclasificar('1234',   '1.1.01',    '2026-10-31', 'contador@delta.com.gt');

DROP FUNCTION pg_temp.corr_reclasificar(TEXT, TEXT, DATE, TEXT);

COMMIT;

-- 4. Verificación -----------------------------------------------------------------------

SELECT c.codigo, c.nombre, c.activa,
       COALESCE(SUM(l.debito - l.credito) FILTER (WHERE a.estado IN ('Confirmado', 'Anulado')), 0) AS neto_propio_contabilizado,
       COUNT(*) FILTER (WHERE a.estado = 'Borrador') AS lineas_borrador
FROM cuentacontable c
LEFT JOIN lineaasiento l ON l.cuenta_id = c.id
LEFT JOIN asientocontable a ON a.id = l.asiento_id
WHERE c.codigo IN ('1.1.02', '1.1.02.03', '1.1.01', '1234')
GROUP BY c.id, c.codigo, c.nombre, c.activa
ORDER BY c.codigo;

-- Debe devolver 0 filas antes de aplicar 10_balance_jerarquico.sql
SELECT p.codigo AS cuenta_de_mayor_con_saldo_propio_o_borrador
FROM cuentacontable p
WHERE EXISTS (SELECT 1 FROM cuentacontable h WHERE h.cuenta_padre_id = p.id)
  AND (
      EXISTS (SELECT 1 FROM lineaasiento l JOIN asientocontable a ON a.id = l.asiento_id
              WHERE l.cuenta_id = p.id AND a.estado = 'Borrador')
      OR (SELECT COALESCE(SUM(l.debito - l.credito), 0) FROM lineaasiento l JOIN asientocontable a ON a.id = l.asiento_id
          WHERE l.cuenta_id = p.id AND a.estado IN ('Confirmado', 'Anulado')) <> 0
  )
ORDER BY p.codigo;

SELECT SUM(l.debito) AS total_debito, SUM(l.credito) AS total_credito, SUM(l.debito) = SUM(l.credito) AS balanceado
FROM lineaasiento l
JOIN asientocontable a ON a.id = l.asiento_id AND a.estado IN ('Confirmado', 'Anulado');
