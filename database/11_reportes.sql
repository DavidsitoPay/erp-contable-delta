-- =====================================================================
-- 11_reportes.sql
-- Saldos jerárquicos por cuenta para balance general y estado de resultados:
-- un periodo Cerrado lee su cierre vigente; los demás, los asientos en vivo.
-- =====================================================================

-- 1. Saldos por periodo (p_acumulado = false) o hasta su fecha de fin (true)

CREATE OR REPLACE FUNCTION fn_reporte_saldos(p_periodo_id INT, p_acumulado BOOLEAN)
RETURNS TABLE (
    cuenta_id        INT,
    codigo           VARCHAR,
    nombre           VARCHAR,
    tipo             VARCHAR,
    naturaleza       VARCHAR,
    cuenta_padre_id  INT,
    nivel            INT,
    es_hoja          BOOLEAN,
    total_debito     NUMERIC,
    total_credito    NUMERIC,
    saldo            NUMERIC
)
LANGUAGE sql STABLE
AS $$
    WITH RECURSIVE
    incluidos AS (
        SELECT p.id, (p.estado = 'Cerrado' AND p.cierres > 0) AS usa_cierre
        FROM periodocontable p
        JOIN periodocontable ref ON ref.id = p_periodo_id
        WHERE p.id = ref.id OR (p_acumulado AND p.fecha_fin <= ref.fecha_fin)
    ),
    movimientos AS (
        SELECT s.cuenta_id, s.total_debito AS debito, s.total_credito AS credito
        FROM vw_saldocuentaperiodo_vigente s
        JOIN incluidos i ON i.id = s.periodo_id AND i.usa_cierre
        UNION ALL
        SELECT l.cuenta_id, l.debito, l.credito
        FROM lineaasiento l
        JOIN asientocontable a ON a.id = l.asiento_id AND a.estado IN ('Confirmado', 'Anulado')
        JOIN incluidos i ON i.id = a.periodo_id AND NOT i.usa_cierre
    ),
    propio AS (
        SELECT m.cuenta_id, SUM(m.debito) AS debito, SUM(m.credito) AS credito
        FROM movimientos m
        GROUP BY m.cuenta_id
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
        c.id,
        c.codigo,
        c.nombre,
        c.tipo,
        c.naturaleza,
        c.cuenta_padre_id,
        pf.nivel,
        NOT EXISTS (SELECT 1 FROM cuentacontable h WHERE h.cuenta_padre_id = c.id),
        COALESCE(ac.debito, 0),
        COALESCE(ac.credito, 0),
        CASE
            WHEN c.naturaleza = 'Deudora' THEN COALESCE(ac.debito, 0) - COALESCE(ac.credito, 0)
            ELSE COALESCE(ac.credito, 0) - COALESCE(ac.debito, 0)
        END
    FROM cuentacontable c
    JOIN profundidad pf ON pf.cuenta_id = c.id
    LEFT JOIN acumulado ac ON ac.cuenta_id = c.id
    WHERE EXISTS (SELECT 1 FROM periodocontable x WHERE x.id = p_periodo_id)
    ORDER BY c.codigo;
$$;
