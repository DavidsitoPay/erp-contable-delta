-- =====================================================================
-- 03_triggers.sql
-- Reglas de integridad que NO pueden delegarse en la aplicación:
-- partida doble, bloqueo de periodo cerrado, no eliminación de asientos,
-- límites de aplicación de pagos, e inmutabilidad de bitácora y saldos.
-- La trazabilidad de auditoría (quién ejecutó la acción) SIGUE sin poder
-- resolverse aquí — eso vive en sp_registrar_auditoria (04_procedures.sql),
-- invocado por la API. Ver docs/architecture.md.
-- =====================================================================

-- --- RN-01: partida doble al confirmar un asiento --------------------------

CREATE OR REPLACE FUNCTION fn_validar_partida_doble() RETURNS TRIGGER AS $$
DECLARE
    v_total_debito  DECIMAL(14,2);
    v_total_credito DECIMAL(14,2);
    v_debe_validar  BOOLEAN;
BEGIN
    -- En INSERT no existe OLD (es NULL para triggers de INSERT en Postgres), así que
    -- la comparación de transición OLD.estado solo aplica en UPDATE. Un INSERT que ya
    -- nace 'Confirmado' (el único flujo real hoy: AsientosController.Registrar hace un
    -- solo INSERT, sin borrador->confirmación por UPDATE) debe validarse igual.
    --
    -- Este trigger corre AFTER INSERT ... DEFERRABLE INITIALLY DEFERRED (no BEFORE):
    -- EF Core inserta primero la fila de asientocontable (para obtener el id generado)
    -- y luego, en la MISMA transacción, las filas de lineaasiento que referencian ese
    -- id. Un BEFORE INSERT vería lineaasiento vacío para este asiento_id (0 filas ->
    -- 0 = 0 -> "balanceado" siempre, sin importar el contenido real) y nunca detectaría
    -- un desbalance. Al diferir la validación al COMMIT, ya existen las líneas.
    IF TG_OP = 'INSERT' THEN
        v_debe_validar := NEW.estado = 'Confirmado';
    ELSE
        v_debe_validar := NEW.estado = 'Confirmado' AND (OLD.estado IS DISTINCT FROM 'Confirmado');
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

CREATE CONSTRAINT TRIGGER trg_validar_partida_doble
AFTER INSERT OR UPDATE ON asientocontable
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION fn_validar_partida_doble();

-- --- RN-02: bloqueo de periodo cerrado --------------------------------------

CREATE OR REPLACE FUNCTION fn_bloquear_periodo_cerrado() RETURNS TRIGGER AS $$
DECLARE
    v_estado_periodo VARCHAR(20);
    v_periodo_id     INT;
BEGIN
    SELECT periodo_id INTO v_periodo_id
    FROM asientocontable
    WHERE id = COALESCE(NEW.asiento_id, OLD.asiento_id);

    SELECT estado INTO v_estado_periodo FROM periodocontable WHERE id = v_periodo_id;

    IF v_estado_periodo = 'Cerrado' THEN
        RAISE EXCEPTION 'RN-02: no se puede modificar un asiento de un periodo cerrado (periodo %)',
            v_periodo_id;
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_bloquear_periodo_cerrado_linea
BEFORE INSERT OR UPDATE OR DELETE ON lineaasiento
FOR EACH ROW EXECUTE FUNCTION fn_bloquear_periodo_cerrado();

-- --- RN-03: un asiento no se elimina, se reversa ----------------------------

CREATE OR REPLACE FUNCTION fn_prevenir_eliminacion_asiento() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'RN-03: los asientos contables no se eliminan; use sp_reversar_asiento(%).', OLD.id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_prevenir_eliminacion_asiento
BEFORE DELETE ON asientocontable
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_asiento();

-- --- RN-05: límite de aplicación de pagos (CxC) -----------------------------

CREATE OR REPLACE FUNCTION fn_limite_pago_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_monto_total    DECIMAL(14,2);
    v_ya_aplicado    DECIMAL(14,2);
BEGIN
    SELECT monto_total INTO v_monto_total FROM documentocxc WHERE id = NEW.documento_id;

    SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_ya_aplicado
    FROM aplicacionpagocliente
    WHERE documento_id = NEW.documento_id AND id <> COALESCE(NEW.id, -1);

    IF (v_ya_aplicado + NEW.monto_aplicado) > v_monto_total THEN
        RAISE EXCEPTION 'RN-05: el pago aplicado (%) excede el saldo pendiente del documento % (saldo: %)',
            NEW.monto_aplicado, NEW.documento_id, (v_monto_total - v_ya_aplicado);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_limite_pago_cxc
BEFORE INSERT OR UPDATE ON aplicacionpagocliente
FOR EACH ROW EXECUTE FUNCTION fn_limite_pago_cxc();

-- --- RN-05: límite de aplicación de pagos (CxP) -----------------------------

CREATE OR REPLACE FUNCTION fn_limite_pago_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_monto_total  DECIMAL(14,2);
    v_ya_aplicado  DECIMAL(14,2);
BEGIN
    SELECT monto_total INTO v_monto_total FROM documentocxp WHERE id = NEW.documento_id;

    SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_ya_aplicado
    FROM aplicacionpagoproveedor
    WHERE documento_id = NEW.documento_id AND id <> COALESCE(NEW.id, -1);

    IF (v_ya_aplicado + NEW.monto_aplicado) > v_monto_total THEN
        RAISE EXCEPTION 'RN-05: el pago aplicado (%) excede el saldo pendiente del documento % (saldo: %)',
            NEW.monto_aplicado, NEW.documento_id, (v_monto_total - v_ya_aplicado);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_limite_pago_cxp
BEFORE INSERT OR UPDATE ON aplicacionpagoproveedor
FOR EACH ROW EXECUTE FUNCTION fn_limite_pago_cxp();

-- --- Inmutabilidad de BitacoraAuditoria --------------------------------------

CREATE OR REPLACE FUNCTION fn_bitacora_inmutable() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'BitacoraAuditoria es inmutable: no se permite UPDATE ni DELETE (registro %).',
        OLD.id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_bitacora_inmutable
BEFORE UPDATE OR DELETE ON bitacoraauditoria
FOR EACH ROW EXECUTE FUNCTION fn_bitacora_inmutable();

-- --- Inmutabilidad de SaldoCuentaPeriodo --------------------------------------

CREATE OR REPLACE FUNCTION fn_saldoperiodo_inmutable() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'SaldoCuentaPeriodo es inmutable: no se permite UPDATE ni DELETE (registro %).',
        OLD.id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_saldoperiodo_inmutable
BEFORE UPDATE OR DELETE ON saldocuentaperiodo
FOR EACH ROW EXECUTE FUNCTION fn_saldoperiodo_inmutable();

-- --- Solo desactivación, nunca eliminación física (usuario y cuentacontable) --

CREATE OR REPLACE FUNCTION fn_prevenir_eliminacion_fisica() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION '% no se elimina físicamente; use UPDATE ... SET activo(a) = false (registro %).',
        TG_TABLE_NAME, OLD.id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_prevenir_eliminacion_usuario
BEFORE DELETE ON usuario
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();

CREATE TRIGGER trg_prevenir_eliminacion_cuentacontable
BEFORE DELETE ON cuentacontable
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();
