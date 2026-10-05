-- =====================================================================
-- 10_balance_jerarquico.sql
-- Balance de saldos jerárquico: los saldos de una cuenta de mayor acumulan
-- los de sus subcuentas; una cuenta con saldo propio no admite subcuentas.
-- =====================================================================

-- 1. Balance de saldos acumulado por jerarquía

CREATE OR REPLACE VIEW vw_balance_saldos AS
WITH RECURSIVE
propio AS (
    SELECT l.cuenta_id, SUM(l.debito) AS debito, SUM(l.credito) AS credito
    FROM lineaasiento l
    JOIN asientocontable a ON a.id = l.asiento_id AND a.estado IN ('Confirmado', 'Anulado')
    GROUP BY l.cuenta_id
),
arbol AS (
    SELECT c.id AS ancestro_id, c.id AS cuenta_id
    FROM cuentacontable c
    UNION ALL
    SELECT t.ancestro_id, h.id
    FROM arbol t
    JOIN cuentacontable h ON h.cuenta_padre_id = t.cuenta_id
) CYCLE cuenta_id SET es_ciclo USING ruta,
acumulado AS (
    SELECT t.ancestro_id AS cuenta_id, SUM(p.debito) AS debito, SUM(p.credito) AS credito
    FROM arbol t
    JOIN propio p ON p.cuenta_id = t.cuenta_id
    WHERE NOT t.es_ciclo
    GROUP BY t.ancestro_id
),
profundidad AS (
    SELECT t.cuenta_id, COUNT(*)::INT AS nivel
    FROM arbol t
    WHERE NOT t.es_ciclo
    GROUP BY t.cuenta_id
)
SELECT
    c.id AS cuenta_id,
    c.codigo,
    c.nombre,
    c.naturaleza,
    COALESCE(ac.debito, 0) AS total_debito,
    COALESCE(ac.credito, 0) AS total_credito,
    CASE
        WHEN c.naturaleza = 'Deudora' THEN COALESCE(ac.debito, 0) - COALESCE(ac.credito, 0)
        ELSE COALESCE(ac.credito, 0) - COALESCE(ac.debito, 0)
    END AS saldo,
    pf.nivel,
    c.cuenta_padre_id,
    NOT EXISTS (SELECT 1 FROM cuentacontable h WHERE h.cuenta_padre_id = c.id) AS es_hoja,
    c.activa,
    COALESCE(pr.debito, 0) AS debito_propio,
    COALESCE(pr.credito, 0) AS credito_propio
FROM cuentacontable c
JOIN profundidad pf ON pf.cuenta_id = c.id
LEFT JOIN acumulado ac ON ac.cuenta_id = c.id
LEFT JOIN propio pr ON pr.cuenta_id = c.id;

-- 2. Una cuenta con saldo propio o asientos en borrador no admite subcuentas

CREATE OR REPLACE FUNCTION fn_validar_padre_sin_saldo() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.cuenta_padre_id IS NULL THEN
        RETURN NEW;
    END IF;
    IF TG_OP = 'UPDATE' AND NEW.cuenta_padre_id IS NOT DISTINCT FROM OLD.cuenta_padre_id THEN
        RETURN NEW;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM lineaasiento l
        JOIN asientocontable a ON a.id = l.asiento_id
        WHERE l.cuenta_id = NEW.cuenta_padre_id AND a.estado = 'Borrador'
    ) OR (
        SELECT COALESCE(SUM(l.debito - l.credito), 0)
        FROM lineaasiento l
        JOIN asientocontable a ON a.id = l.asiento_id
        WHERE l.cuenta_id = NEW.cuenta_padre_id AND a.estado IN ('Confirmado', 'Anulado')
    ) <> 0 THEN
        RAISE EXCEPTION 'La cuenta % tiene saldo propio o asientos en borrador; reclasifique su saldo antes de agregarle subcuentas.',
            (SELECT codigo FROM cuentacontable WHERE id = NEW.cuenta_padre_id)
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validar_padre_sin_saldo ON cuentacontable;

CREATE TRIGGER trg_validar_padre_sin_saldo
BEFORE INSERT OR UPDATE OF cuenta_padre_id ON cuentacontable
FOR EACH ROW EXECUTE FUNCTION fn_validar_padre_sin_saldo();
