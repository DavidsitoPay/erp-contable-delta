-- =====================================================================
-- 03_triggers.sql — Implementa RN-12: saldo_pendiente derivado, no editable
-- =====================================================================

-- --- CxC ---------------------------------------------------------------

CREATE OR REPLACE FUNCTION fn_recalcular_saldo_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_documento_id INT;
BEGIN
    v_documento_id := COALESCE(NEW.documento_id, OLD.documento_id);

    UPDATE documentocxc
    SET saldo_pendiente = monto_total - COALESCE((
        SELECT SUM(monto_aplicado)
        FROM aplicacionpagocliente
        WHERE documento_id = v_documento_id
    ), 0)
    WHERE id = v_documento_id;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_recalcular_saldo_cxc
AFTER INSERT OR UPDATE OR DELETE ON aplicacionpagocliente
FOR EACH ROW EXECUTE FUNCTION fn_recalcular_saldo_cxc();

-- --- CxP ---------------------------------------------------------------

CREATE OR REPLACE FUNCTION fn_recalcular_saldo_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_documento_id INT;
BEGIN
    v_documento_id := COALESCE(NEW.documento_id, OLD.documento_id);

    UPDATE documentocxp
    SET saldo_pendiente = monto_total - COALESCE((
        SELECT SUM(monto_aplicado)
        FROM aplicacionpagoproveedor
        WHERE documento_id = v_documento_id
    ), 0)
    WHERE id = v_documento_id;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_recalcular_saldo_cxp
AFTER INSERT OR UPDATE OR DELETE ON aplicacionpagoproveedor
FOR EACH ROW EXECUTE FUNCTION fn_recalcular_saldo_cxp();

-- NOTA: no existe (ni debe crearse) un trigger que escriba en bitacoraauditoria.
-- RN-08 es responsabilidad exclusiva de la API (ver docs/architecture.md).
