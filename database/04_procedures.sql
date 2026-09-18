-- =====================================================================
-- 04_procedures.sql
-- =====================================================================

-- --- RN-08: registro de auditoría, invocado por la API ----------------------
-- La API resuelve usuario_id desde el token JWT ya validado y llama este
-- procedimiento DENTRO de la misma transacción de la operación de negocio,
-- de modo que si esta se revierte, el registro de auditoría también.

CREATE OR REPLACE PROCEDURE sp_registrar_auditoria(
    p_usuario_id     INT,
    p_accion         VARCHAR(100),
    p_tabla_afectada VARCHAR(100),
    p_detalle        TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO bitacoraauditoria (usuario_id, fecha, accion, tabla_afectada, detalle)
    VALUES (p_usuario_id, CURRENT_DATE, p_accion, p_tabla_afectada, p_detalle);
END;
$$;

-- --- Cierre de periodo contable ----------------------------------------------
-- Consolida el saldo de cada cuenta contable con movimiento en el periodo,
-- de forma inmutable (ver trg_saldoperiodo_inmutable), y cierra el periodo.
-- p_usuario_id se usa para la auditoría; la verificación de perfil
-- (solo un usuario autorizado puede cerrar/reabrir un periodo) se hace aquí
-- mismo, consultando perfil.nombre — perfiles reales del proyecto, ver
-- 06_seed_dev.sql (Administrador del sistema, Contador, Vendedor, Técnico).

CREATE OR REPLACE PROCEDURE sp_cerrar_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_perfil_nombre VARCHAR(100);
BEGIN
    SELECT p.nombre INTO v_perfil_nombre
    FROM usuario u JOIN perfil p ON p.id = u.perfil_id
    WHERE u.id = p_usuario_id;

    IF v_perfil_nombre IS DISTINCT FROM 'Contador' AND v_perfil_nombre IS DISTINCT FROM 'Administrador del sistema' THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para cerrar un periodo.', p_usuario_id;
    END IF;

    INSERT INTO saldocuentaperiodo (periodo_id, cuenta_id, total_debito, total_credito, saldo_final)
    SELECT
        p_periodo_id,
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
    JOIN asientocontable a ON a.id = l.asiento_id AND a.periodo_id = p_periodo_id AND a.estado = 'Confirmado'
    GROUP BY c.id, c.naturaleza;

    UPDATE periodocontable SET estado = 'Cerrado' WHERE id = p_periodo_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'cerrar_periodo', 'periodocontable',
        format('Periodo %s cerrado', p_periodo_id));
END;
$$;

-- --- Reapertura de periodo (excepcional, requiere perfil autorizado) --------

CREATE OR REPLACE PROCEDURE sp_reabrir_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_perfil_nombre VARCHAR(100);
BEGIN
    SELECT p.nombre INTO v_perfil_nombre
    FROM usuario u JOIN perfil p ON p.id = u.perfil_id
    WHERE u.id = p_usuario_id;

    IF v_perfil_nombre IS DISTINCT FROM 'Administrador del sistema' THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para reabrir un periodo.', p_usuario_id;
    END IF;

    -- Nota: los registros ya escritos en saldocuentaperiodo para este periodo
    -- NO se eliminan (son inmutables); un nuevo cierre debe usar otra estrategia
    -- de conciliación si el periodo se reabre. Definir con el equipo antes de usar.
    UPDATE periodocontable SET estado = 'Abierto' WHERE id = p_periodo_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reabrir_periodo', 'periodocontable',
        format('Periodo %s reabierto', p_periodo_id));
END;
$$;

-- --- RN-03: reversión de un asiento (nunca eliminación) ----------------------

CREATE OR REPLACE PROCEDURE sp_reversar_asiento(p_asiento_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_nuevo_asiento_id INT;
    v_periodo_id       INT;
BEGIN
    SELECT periodo_id INTO v_periodo_id FROM asientocontable WHERE id = p_asiento_id;

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    SELECT 'REV-' || numero, CURRENT_DATE, v_periodo_id, monto, 'Confirmado', p_usuario_id
    FROM asientocontable WHERE id = p_asiento_id
    RETURNING id INTO v_nuevo_asiento_id;

    -- Invierte cada línea del asiento original (débito <-> crédito).
    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_nuevo_asiento_id, cuenta_id, centro_costo_id, credito, debito
    FROM lineaasiento WHERE asiento_id = p_asiento_id;

    UPDATE asientocontable SET estado = 'Anulado' WHERE id = p_asiento_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reversar_asiento', 'asientocontable',
        format('Asiento %s reversado mediante asiento %s', p_asiento_id, v_nuevo_asiento_id));
END;
$$;

-- --- Finalización de conciliación bancaria (verifica perfil) ----------------

CREATE OR REPLACE PROCEDURE sp_finalizar_conciliacion(p_conciliacion_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_perfil_nombre VARCHAR(100);
BEGIN
    SELECT p.nombre INTO v_perfil_nombre
    FROM usuario u JOIN perfil p ON p.id = u.perfil_id
    WHERE u.id = p_usuario_id;

    IF v_perfil_nombre IS DISTINCT FROM 'Contador' AND v_perfil_nombre IS DISTINCT FROM 'Administrador del sistema' THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para finalizar una conciliación.', p_usuario_id;
    END IF;

    UPDATE conciliacionbancaria SET estado = 'Conciliado' WHERE id = p_conciliacion_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'finalizar_conciliacion', 'conciliacionbancaria',
        format('Conciliación %s finalizada', p_conciliacion_id));
END;
$$;
