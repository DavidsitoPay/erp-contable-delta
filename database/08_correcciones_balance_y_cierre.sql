-- =====================================================================
-- 08_correcciones_balance_y_cierre.sql
-- =====================================================================

-- 1. Balance de saldos: solo asientos contabilizados (Confirmado o Anulado por reversa)

CREATE OR REPLACE VIEW vw_balance_saldos AS
SELECT
    c.id AS cuenta_id,
    c.codigo,
    c.nombre,
    c.naturaleza,
    COALESCE(SUM(l.debito), 0) AS total_debito,
    COALESCE(SUM(l.credito), 0) AS total_credito,
    CASE
        WHEN c.naturaleza = 'Deudora' THEN COALESCE(SUM(l.debito), 0) - COALESCE(SUM(l.credito), 0)
        ELSE COALESCE(SUM(l.credito), 0) - COALESCE(SUM(l.debito), 0)
    END AS saldo
FROM cuentacontable c
LEFT JOIN (
    lineaasiento l
    JOIN asientocontable a ON a.id = l.asiento_id AND a.estado IN ('Confirmado', 'Anulado')
) ON l.cuenta_id = c.id
GROUP BY c.id, c.codigo, c.nombre, c.naturaleza;

-- 2. Cierres versionados de saldocuentaperiodo

ALTER TABLE saldocuentaperiodo ADD COLUMN cierre_numero INT NOT NULL DEFAULT 1;

ALTER TABLE saldocuentaperiodo DROP CONSTRAINT saldocuentaperiodo_periodo_id_cuenta_id_key;

ALTER TABLE saldocuentaperiodo
    ADD CONSTRAINT saldocuentaperiodo_periodo_cierre_cuenta_key UNIQUE (periodo_id, cierre_numero, cuenta_id);

CREATE OR REPLACE VIEW vw_saldocuentaperiodo_vigente AS
SELECT s.id, s.periodo_id, s.cuenta_id, s.total_debito, s.total_credito, s.saldo_final, s.cierre_numero
FROM saldocuentaperiodo s
WHERE s.cierre_numero = (
    SELECT MAX(m.cierre_numero) FROM saldocuentaperiodo m WHERE m.periodo_id = s.periodo_id
);

-- 3. Cierre y reapertura de periodo: solo desde el estado esperado

CREATE OR REPLACE PROCEDURE sp_cerrar_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado_periodo VARCHAR(20);
    v_cierre_numero  INT;
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_contador(), fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para cerrar un periodo.', p_usuario_id;
    END IF;

    SELECT estado INTO v_estado_periodo FROM periodocontable WHERE id = p_periodo_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El periodo % no existe.', p_periodo_id USING ERRCODE = 'P0002';
    END IF;

    IF v_estado_periodo <> 'Abierto' THEN
        RAISE EXCEPTION 'El periodo % no está Abierto (estado: %); no se puede cerrar.', p_periodo_id, v_estado_periodo
            USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(MAX(cierre_numero), 0) + 1 INTO v_cierre_numero
    FROM saldocuentaperiodo WHERE periodo_id = p_periodo_id;

    INSERT INTO saldocuentaperiodo (periodo_id, cierre_numero, cuenta_id, total_debito, total_credito, saldo_final)
    SELECT
        p_periodo_id,
        v_cierre_numero,
        c.id,
        COALESCE(SUM(l.debito), 0),
        COALESCE(SUM(l.credito), 0),
        CASE
            WHEN c.naturaleza = 'Deudora'
                THEN COALESCE(SUM(l.debito), 0) - COALESCE(SUM(l.credito), 0)
            ELSE COALESCE(SUM(l.credito), 0) - COALESCE(SUM(l.debito), 0)
        END
    FROM cuentacontable c
    JOIN lineaasiento l ON l.cuenta_id = c.id
    JOIN asientocontable a ON a.id = l.asiento_id AND a.periodo_id = p_periodo_id
        AND a.estado IN ('Confirmado', 'Anulado')
    GROUP BY c.id, c.naturaleza;

    UPDATE periodocontable SET estado = 'Cerrado' WHERE id = p_periodo_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'cerrar_periodo', 'periodocontable',
        format('Periodo %s cerrado', p_periodo_id));
END;
$$;

CREATE OR REPLACE PROCEDURE sp_reabrir_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado_periodo VARCHAR(20);
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para reabrir un periodo.', p_usuario_id;
    END IF;

    SELECT estado INTO v_estado_periodo FROM periodocontable WHERE id = p_periodo_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El periodo % no existe.', p_periodo_id USING ERRCODE = 'P0002';
    END IF;

    IF v_estado_periodo <> 'Cerrado' THEN
        RAISE EXCEPTION 'El periodo % no está Cerrado (estado: %); no se puede reabrir.', p_periodo_id, v_estado_periodo
            USING ERRCODE = '55000';
    END IF;

    UPDATE periodocontable SET estado = 'Abierto' WHERE id = p_periodo_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reabrir_periodo', 'periodocontable',
        format('Periodo %s reabierto', p_periodo_id));
END;
$$;

-- 4. Reversión: solo un asiento Confirmado que no sea a su vez una reversa

CREATE OR REPLACE PROCEDURE sp_reversar_asiento(p_asiento_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_nuevo_asiento_id INT;
    v_periodo_id       INT;
    v_estado           VARCHAR(20);
    v_numero           VARCHAR(30);
BEGIN
    SELECT periodo_id, estado, numero INTO v_periodo_id, v_estado, v_numero
    FROM asientocontable WHERE id = p_asiento_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El asiento % no existe.', p_asiento_id USING ERRCODE = 'P0002';
    END IF;

    IF v_estado <> 'Confirmado' THEN
        RAISE EXCEPTION 'El asiento % no está Confirmado (estado: %); no se puede reversar.', p_asiento_id, v_estado
            USING ERRCODE = '55000';
    END IF;

    -- Las reversas nacen con el prefijo 'REV-' y no se vuelven a reversar.
    IF v_numero LIKE 'REV-%' THEN
        RAISE EXCEPTION 'El asiento % es una reversa; no se puede reversar.', p_asiento_id
            USING ERRCODE = '55000';
    END IF;

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    SELECT 'REV-' || numero, CURRENT_DATE, v_periodo_id, monto, 'Confirmado', p_usuario_id
    FROM asientocontable WHERE id = p_asiento_id
    RETURNING id INTO v_nuevo_asiento_id;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_nuevo_asiento_id, cuenta_id, centro_costo_id, credito, debito
    FROM lineaasiento WHERE asiento_id = p_asiento_id;

    UPDATE asientocontable SET estado = 'Anulado' WHERE id = p_asiento_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reversar_asiento', 'asientocontable',
        format('Asiento %s reversado mediante asiento %s', p_asiento_id, v_nuevo_asiento_id));
END;
$$;
