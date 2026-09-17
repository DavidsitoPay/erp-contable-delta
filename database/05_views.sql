-- =====================================================================
-- 05_views.sql — Saldos calculados en tiempo real (nunca almacenados como columna mutable)
-- =====================================================================

-- Saldo de cada cuenta contable, derivado de lineaasiento (no se guarda en cuentacontable).
CREATE OR REPLACE VIEW vw_saldo_cuenta_contable AS
SELECT
    c.id AS cuenta_id,
    c.codigo,
    c.nombre,
    COALESCE(SUM(l.debito), 0) AS total_debito,
    COALESCE(SUM(l.credito), 0) AS total_credito,
    COALESCE(SUM(l.debito), 0) - COALESCE(SUM(l.credito), 0) AS saldo
FROM cuentacontable c
LEFT JOIN lineaasiento l ON l.cuenta_id = c.id
GROUP BY c.id, c.codigo, c.nombre;

-- Saldo en tiempo real de cada cuenta bancaria, derivado de movimientotesoreria
-- (complementa el saldo consolidado e inmutable de saldocuentaperiodo).
CREATE OR REPLACE VIEW vw_saldo_cuenta_bancaria AS
SELECT
    cb.id AS cuenta_bancaria_id,
    cb.banco,
    cb.numero,
    COALESCE(SUM(CASE WHEN mt.tipo = 'Ingreso' THEN mt.monto ELSE 0 END), 0) AS total_ingresos,
    COALESCE(SUM(CASE WHEN mt.tipo = 'Egreso' THEN mt.monto ELSE 0 END), 0) AS total_egresos,
    COALESCE(SUM(CASE WHEN mt.tipo = 'Ingreso' THEN mt.monto ELSE -mt.monto END), 0) AS saldo_actual
FROM cuentabancaria cb
LEFT JOIN movimientotesoreria mt ON mt.cuenta_bancaria_id = cb.id
GROUP BY cb.id, cb.banco, cb.numero;
