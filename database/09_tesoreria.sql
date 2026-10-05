-- =====================================================================
-- 09_tesoreria.sql
-- Tesorería y conciliación bancaria (E6): cuentas, movimientos con asiento,
-- conciliación con saldo de extracto (RN-10).
-- =====================================================================

-- 1. Cuenta bancaria

ALTER TABLE cuentabancaria
    ADD COLUMN activa BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN cuenta_contable_id INT NOT NULL REFERENCES cuentacontable(id),
    ADD COLUMN saldo_apertura DECIMAL(14,2) NOT NULL,
    ADD COLUMN fecha_apertura DATE;

ALTER TABLE cuentabancaria
    ADD CONSTRAINT ck_cuentabancaria_tipo CHECK (tipo IN ('Monetaria', 'Ahorro')),
    ADD CONSTRAINT ck_cuentabancaria_apertura CHECK (saldo_apertura >= 0 AND (saldo_apertura = 0 OR fecha_apertura IS NOT NULL)),
    ADD CONSTRAINT uq_cuentabancaria_banco_numero UNIQUE (banco, numero),
    ADD CONSTRAINT uq_cuentabancaria_cuenta_contable UNIQUE (cuenta_contable_id);

CREATE TRIGGER trg_prevenir_eliminacion_cuentabancaria
BEFORE DELETE ON cuentabancaria
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();

-- 2. Movimiento de tesorería

ALTER TABLE movimientotesoreria
    ADD COLUMN asiento_id INT NOT NULL REFERENCES asientocontable(id),
    ADD COLUMN descripcion VARCHAR(255) NOT NULL,
    ADD COLUMN referencia VARCHAR(100),
    ADD COLUMN origen VARCHAR(20) NOT NULL,
    ADD COLUMN transferencia_id UUID,
    ADD COLUMN recibo_pago_id INT REFERENCES recibopagocliente(id),
    ADD COLUMN pago_proveedor_id INT REFERENCES pagoproveedorcabecera(id);

ALTER TABLE movimientotesoreria
    ADD CONSTRAINT ck_movimiento_tipo CHECK (tipo IN ('Ingreso', 'Egreso')),
    ADD CONSTRAINT ck_movimiento_monto CHECK (monto > 0),
    ADD CONSTRAINT ck_movimiento_origen CHECK (origen IN ('Manual', 'Transferencia', 'CxC', 'CxP', 'Apertura')),
    ADD CONSTRAINT ck_movimiento_vinculo CHECK (
        (origen IN ('Manual', 'Apertura') AND transferencia_id IS NULL AND recibo_pago_id IS NULL AND pago_proveedor_id IS NULL AND (origen = 'Manual' OR tipo = 'Ingreso')) OR
        (origen = 'Transferencia' AND transferencia_id IS NOT NULL AND recibo_pago_id IS NULL     AND pago_proveedor_id IS NULL) OR
        (origen = 'CxC'           AND transferencia_id IS NULL     AND recibo_pago_id IS NOT NULL AND pago_proveedor_id IS NULL AND tipo = 'Ingreso') OR
        (origen = 'CxP'           AND transferencia_id IS NULL     AND recibo_pago_id IS NULL     AND pago_proveedor_id IS NOT NULL AND tipo = 'Egreso'));

CREATE UNIQUE INDEX ux_movimiento_recibo_pago ON movimientotesoreria (recibo_pago_id) WHERE recibo_pago_id IS NOT NULL;
CREATE UNIQUE INDEX ux_movimiento_pago_proveedor ON movimientotesoreria (pago_proveedor_id) WHERE pago_proveedor_id IS NOT NULL;
CREATE UNIQUE INDEX ux_movimiento_apertura ON movimientotesoreria (cuenta_bancaria_id) WHERE origen = 'Apertura';
CREATE UNIQUE INDEX ux_movimiento_transferencia_tipo ON movimientotesoreria (transferencia_id, tipo) WHERE transferencia_id IS NOT NULL;
CREATE INDEX ix_movimiento_cuenta_fecha ON movimientotesoreria (cuenta_bancaria_id, fecha);

CREATE OR REPLACE FUNCTION fn_movimiento_inmutable() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'MovimientoTesoreria es inmutable: no se permite UPDATE ni DELETE (registro %).', OLD.id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_movimiento_inmutable
BEFORE UPDATE OR DELETE ON movimientotesoreria
FOR EACH ROW EXECUTE FUNCTION fn_movimiento_inmutable();

-- 3. Conciliación bancaria

ALTER TABLE conciliacionbancaria ADD COLUMN saldo_extracto DECIMAL(14,2) NOT NULL;

ALTER TABLE conciliacionbancaria
    ADD CONSTRAINT ck_conciliacion_estado CHECK (estado IN ('Pendiente', 'Conciliado', 'Cancelada'));

CREATE UNIQUE INDEX ux_conciliacion_pendiente_por_cuenta
    ON conciliacionbancaria (cuenta_bancaria_id) WHERE estado = 'Pendiente';

CREATE OR REPLACE FUNCTION fn_conciliacion_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' OR OLD.estado IN ('Conciliado', 'Cancelada') THEN
        RAISE EXCEPTION 'La conciliación % no admite modificación ni eliminación.', OLD.id USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_conciliacion_inmutable
BEFORE UPDATE OR DELETE ON conciliacionbancaria
FOR EACH ROW EXECUTE FUNCTION fn_conciliacion_inmutable();

-- 4. Detalle de conciliación

ALTER TABLE detalleconciliacion
    ADD CONSTRAINT uq_detalleconciliacion_movimiento UNIQUE (movimiento_id);

CREATE OR REPLACE FUNCTION fn_detalle_conciliacion_valida() RETURNS TRIGGER AS $$
DECLARE
    v_conc conciliacionbancaria%ROWTYPE;
    v_mov  movimientotesoreria%ROWTYPE;
BEGIN
    SELECT * INTO v_conc FROM conciliacionbancaria
    WHERE id = COALESCE(NEW.conciliacion_id, OLD.conciliacion_id) FOR SHARE;

    IF v_conc.estado <> 'Pendiente' THEN
        RAISE EXCEPTION 'La conciliación % no está pendiente; su detalle no se modifica.', v_conc.id USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;

    SELECT * INTO v_mov FROM movimientotesoreria WHERE id = NEW.movimiento_id;
    IF v_mov.cuenta_bancaria_id <> v_conc.cuenta_bancaria_id OR v_mov.fecha > v_conc.fecha OR v_mov.origen = 'Apertura' THEN
        RAISE EXCEPTION 'El movimiento % no es conciliable en la conciliación % (otra cuenta, posterior al corte o de apertura).',
            NEW.movimiento_id, v_conc.id USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_detalle_conciliacion_valida
BEFORE INSERT OR UPDATE OR DELETE ON detalleconciliacion
FOR EACH ROW EXECUTE FUNCTION fn_detalle_conciliacion_valida();

-- 5. Resumen de conciliación (única fuente de la matemática de RN-10)

CREATE OR REPLACE VIEW vw_conciliacion_resumen AS
SELECT
    t.*,
    t.saldo_inicial + t.total_marcado AS saldo_conciliado,
    t.saldo_extracto - (t.saldo_inicial + t.total_marcado) AS diferencia
FROM (
    SELECT
        c.id AS conciliacion_id,
        c.cuenta_bancaria_id,
        c.periodo_id,
        c.fecha,
        c.estado,
        c.saldo_extracto,
        COALESCE((SELECT p.saldo_extracto FROM conciliacionbancaria p
                  WHERE p.cuenta_bancaria_id = c.cuenta_bancaria_id AND p.estado = 'Conciliado' AND p.id < c.id
                  ORDER BY p.id DESC LIMIT 1),
                 (SELECT cb.saldo_apertura FROM cuentabancaria cb WHERE cb.id = c.cuenta_bancaria_id)) AS saldo_inicial,
        COALESCE((SELECT SUM(CASE WHEN m.tipo = 'Ingreso' THEN m.monto ELSE -m.monto END)
                  FROM detalleconciliacion d JOIN movimientotesoreria m ON m.id = d.movimiento_id
                  WHERE d.conciliacion_id = c.id), 0) AS total_marcado,
        (SELECT COUNT(*) FROM detalleconciliacion d WHERE d.conciliacion_id = c.id)::INT AS cantidad_movimientos
    FROM conciliacionbancaria c
) t;

-- 6. sp_finalizar_conciliacion endurecido (misma firma que 07)

CREATE OR REPLACE PROCEDURE sp_finalizar_conciliacion(p_conciliacion_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado     VARCHAR(20);
    v_diferencia DECIMAL(14,2);
BEGIN
    IF NOT fn_usuario_tiene_perfil_autorizado(p_usuario_id, fn_perfil_contador(), fn_perfil_administrador_sistema()) THEN
        RAISE EXCEPTION 'El usuario % no tiene perfil autorizado para finalizar una conciliación.', p_usuario_id;
    END IF;

    SELECT estado INTO v_estado FROM conciliacionbancaria WHERE id = p_conciliacion_id FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La conciliación % no existe.', p_conciliacion_id USING ERRCODE = 'P0002';
    END IF;
    IF v_estado = 'Conciliado' THEN
        RAISE EXCEPTION 'La conciliación % ya está finalizada.', p_conciliacion_id USING ERRCODE = '55000';
    END IF;
    IF v_estado = 'Cancelada' THEN
        RAISE EXCEPTION 'La conciliación % está cancelada.', p_conciliacion_id USING ERRCODE = '55000';
    END IF;

    SELECT diferencia INTO v_diferencia FROM vw_conciliacion_resumen WHERE conciliacion_id = p_conciliacion_id;
    IF v_diferencia <> 0 THEN
        RAISE EXCEPTION 'La conciliación % no puede finalizarse: la diferencia contra el extracto es % (debe ser 0).',
            p_conciliacion_id, v_diferencia USING ERRCODE = '55000';
    END IF;

    UPDATE conciliacionbancaria SET estado = 'Conciliado' WHERE id = p_conciliacion_id;

    CALL sp_registrar_auditoria(p_usuario_id, 'finalizar_conciliacion', 'conciliacionbancaria',
        format('Conciliación %s finalizada', p_conciliacion_id));
END;
$$;
