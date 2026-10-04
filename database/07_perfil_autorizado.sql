-- =====================================================================
-- 07_perfil_autorizado.sql
-- =====================================================================

-- Postgres no tiene constantes de módulo; una función SQL IMMUTABLE es el idiom.
CREATE OR REPLACE FUNCTION fn_perfil_administrador_sistema() RETURNS VARCHAR AS $$
    SELECT 'Administrador del sistema'::VARCHAR;
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE FUNCTION fn_perfil_contador() RETURNS VARCHAR AS $$
    SELECT 'Contador'::VARCHAR;
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE FUNCTION fn_usuario_tiene_perfil_autorizado(
    p_usuario_id INT,
    VARIADIC p_perfiles_permitidos VARCHAR[]
) RETURNS BOOLEAN AS $$
DECLARE
    v_perfil_nombre VARCHAR(100);
BEGIN
    SELECT p.nombre INTO v_perfil_nombre
    FROM usuario u JOIN perfil p ON p.id = u.perfil_id
    WHERE u.id = p_usuario_id;

    RETURN v_perfil_nombre = ANY(p_perfiles_permitidos);
END;
$$ LANGUAGE plpgsql;

-- --- RN-01 ---
CREATE OR REPLACE FUNCTION fn_validar_partida_doble() RETURNS TRIGGER AS $$
DECLARE
    c_estado_confirmado CONSTANT VARCHAR(20) := 'Confirmado';
    v_total_debito  DECIMAL(14,2);
    v_total_credito DECIMAL(14,2);
    v_debe_validar  BOOLEAN;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_debe_validar := NEW.estado = c_estado_confirmado;
    ELSE
        v_debe_validar := NEW.estado = c_estado_confirmado AND (OLD.estado IS DISTINCT FROM c_estado_confirmado);
    END IF;

    IF v_debe_validar THEN
        SELECT COALESCE(SUM(debito), 0), COALESCE(SUM(credito), 0)
        INTO v_total_debito, v_total_credito
        FROM lineaasiento
        WHERE asiento_id = NEW.id;

        IF v_total_debito <> v_total_credito THEN
            RAISE EXCEPTION 'RN-01: el asiento % no cumple partida doble (débito % != crédito %)',
                NEW.id, v_total_debito, v_total_credito;
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE sp_cerrar_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_contador(), fn_perfil_administrador_sistema()) THEN
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

CREATE OR REPLACE PROCEDURE sp_reabrir_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para reabrir un periodo.', p_usuario_id;
    END IF;

    -- saldocuentaperiodo ya escrito para este periodo no se borra (es inmutable);
    -- un nuevo cierre tras reabrir necesita su propia estrategia de conciliación.
    UPDATE periodocontable SET estado = 'Abierto' WHERE id = p_periodo_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reabrir_periodo', 'periodocontable',
        format('Periodo %s reabierto', p_periodo_id));
END;
$$;

CREATE OR REPLACE PROCEDURE sp_finalizar_conciliacion(p_conciliacion_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_contador(), fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para finalizar una conciliación.', p_usuario_id;
    END IF;

    UPDATE conciliacionbancaria SET estado = 'Conciliado' WHERE id = p_conciliacion_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'finalizar_conciliacion', 'conciliacionbancaria',
        format('Conciliación %s finalizada', p_conciliacion_id));
END;
$$;
