-- =====================================================================
-- 05_views.sql
-- Saldos calculados en tiempo real. Sustituyen por completo a las columnas
-- "saldo" y "saldo_pendiente" que existían como campos editables.
-- =====================================================================

-- Balance de saldos por cuenta contable (periodo en curso / histórico completo).
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
LEFT JOIN lineaasiento l ON l.cuenta_id = c.id
LEFT JOIN asientocontable a ON a.id = l.asiento_id AND a.estado = 'Confirmado'
GROUP BY c.id, c.codigo, c.nombre, c.naturaleza;

-- Saldo en tiempo real de cada cuenta bancaria (reemplaza cuentabancaria.saldo).
CREATE OR REPLACE VIEW vw_saldocuentabancaria AS
SELECT
    cb.id AS cuenta_bancaria_id,
    cb.banco,
    cb.numero,
    COALESCE(SUM(CASE WHEN mt.tipo = 'Ingreso' THEN mt.monto ELSE -mt.monto END), 0) AS saldo
FROM cuentabancaria cb
LEFT JOIN movimientotesoreria mt ON mt.cuenta_bancaria_id = cb.id
GROUP BY cb.id, cb.banco, cb.numero;

-- Saldo pendiente de cada documento de CxC (reemplaza documentocxc.saldo_pendiente).
CREATE OR REPLACE VIEW vw_saldodocumentocxc AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    d.monto_total - COALESCE((
        SELECT SUM(ap.monto_aplicado)
        FROM aplicacionpagocliente ap
        WHERE ap.documento_id = d.id
    ), 0) AS saldo_pendiente
FROM documentocxc d;

-- Saldo pendiente de cada documento de CxP (reemplaza documentocxp.saldo_pendiente).
CREATE OR REPLACE VIEW vw_saldodocumentocxp AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    d.monto_total - COALESCE((
        SELECT SUM(ap.monto_aplicado)
        FROM aplicacionpagoproveedor ap
        WHERE ap.documento_id = d.id
    ), 0) AS saldo_pendiente
FROM documentocxp d;
