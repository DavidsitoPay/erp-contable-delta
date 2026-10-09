-- =====================================================================
-- 12_reversa_asientos.sql
-- =====================================================================

-- 1. Reversión (RN-03): solo Administrador, con motivo, periodo Abierto y sin vínculos

DROP PROCEDURE IF EXISTS sp_reversar_asiento(INT, INT);

CREATE OR REPLACE PROCEDURE sp_reversar_asiento(p_asiento_id INT, p_usuario_id INT, p_motivo TEXT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_nuevo_asiento_id INT;
    v_periodo_id       INT;
    v_estado           VARCHAR(20);
    v_reversa_de_id    INT;
    v_fecha            DATE;
    v_periodo_nombre   VARCHAR(100);
    v_periodo_estado   VARCHAR(20);
    v_periodo_fin      DATE;
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para reversar un asiento.', p_usuario_id;
    END IF;

    IF p_motivo IS NULL OR btrim(p_motivo) = '' THEN
        RAISE EXCEPTION 'El motivo de la reversa es obligatorio.' USING ERRCODE = '22023';
    END IF;

    SELECT periodo_id, estado, reversa_de_id, fecha
    INTO v_periodo_id, v_estado, v_reversa_de_id, v_fecha
    FROM asientocontable WHERE id = p_asiento_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El asiento % no existe.', p_asiento_id USING ERRCODE = 'P0002';
    END IF;

    IF v_estado <> 'Confirmado' THEN
        RAISE EXCEPTION 'El asiento % no está Confirmado (estado: %); no se puede reversar.', p_asiento_id, v_estado
            USING ERRCODE = '55000';
    END IF;

    IF v_reversa_de_id IS NOT NULL THEN
        RAISE EXCEPTION 'El asiento % es una reversa; no se puede reversar.', p_asiento_id
            USING ERRCODE = '55000';
    END IF;

    SELECT nombre, estado, fecha_fin INTO v_periodo_nombre, v_periodo_estado, v_periodo_fin
    FROM periodocontable WHERE id = v_periodo_id;

    IF v_periodo_estado <> 'Abierto' THEN
        RAISE EXCEPTION 'El asiento % pertenece al periodo % en estado ''%''; solo se puede reversar un asiento de un periodo Abierto.',
            p_asiento_id, v_periodo_nombre, v_periodo_estado
            USING ERRCODE = '55000';
    END IF;

    IF EXISTS (SELECT 1 FROM documentocxc WHERE asiento_id = p_asiento_id) THEN
        RAISE EXCEPTION 'El asiento % está vinculado a un documento de cuentas por cobrar; no se puede reversar.', p_asiento_id
            USING ERRCODE = '55000';
    END IF;

    IF EXISTS (SELECT 1 FROM documentocxp WHERE asiento_id = p_asiento_id) THEN
        RAISE EXCEPTION 'El asiento % está vinculado a un documento de cuentas por pagar; no se puede reversar.', p_asiento_id
            USING ERRCODE = '55000';
    END IF;

    IF EXISTS (SELECT 1 FROM movimientotesoreria WHERE asiento_id = p_asiento_id) THEN
        RAISE EXCEPTION 'El asiento % está vinculado a un movimiento de tesorería (cobros, pagos, transferencias o apertura); reversarlo desincronizaría los libros contables del banco.', p_asiento_id
            USING ERRCODE = '55000';
    END IF;

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id, reversa_de_id)
    SELECT LEFT('REV-' || numero, 30), GREATEST(v_fecha, LEAST(CURRENT_DATE, v_periodo_fin)),
           v_periodo_id, monto, 'Confirmado', p_usuario_id, id
    FROM asientocontable WHERE id = p_asiento_id
    RETURNING id INTO v_nuevo_asiento_id;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_nuevo_asiento_id, cuenta_id, centro_costo_id, credito, debito
    FROM lineaasiento WHERE asiento_id = p_asiento_id;

    UPDATE asientocontable SET estado = 'Anulado' WHERE id = p_asiento_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'reversar_asiento', 'asientocontable',
        format('Asiento %s reversado mediante asiento %s. Motivo: %s', p_asiento_id, v_nuevo_asiento_id, btrim(p_motivo)));
END;
$$;
