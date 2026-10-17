-- 18_enteramiento_ajuste_feriado.sql
-- Enteramiento del ISR a no residentes (RN-32), plazo y calendario de feriados (RN-33, RN-36), ajuste por nota de credito (RN-34),
-- pais de la contraparte (RN-35) y siembra del grupo 2 del catalogo. No se edita un script ya aplicado.


-- 1. Enteramiento (monto en GTQ, inmutable salvo VIGENTE -> ANULADO)

CREATE TABLE IF NOT EXISTS enteramiento (
    id                       SERIAL PRIMARY KEY,
    concepto                 VARCHAR(15)   NOT NULL DEFAULT 'ISR_NORES',
    formulario               VARCHAR(10)   NOT NULL,
    periodo                  DATE          NOT NULL,
    fecha_pago               DATE          NOT NULL,
    boleta                   VARCHAR(40)   NOT NULL,
    monto                    DECIMAL(14,2) NOT NULL,
    cuenta_bancaria_id       INT           NOT NULL REFERENCES cuentabancaria(id),
    asiento_id               INT           NOT NULL REFERENCES asientocontable(id),
    movimiento_tesoreria_id  INT           NOT NULL REFERENCES movimientotesoreria(id),
    estado                   VARCHAR(10)   NOT NULL DEFAULT 'VIGENTE',
    motivo_anulacion         VARCHAR(300),
    anulado_por              INT REFERENCES usuario(id),
    anulado_en               TIMESTAMPTZ,
    asiento_reversa_id       INT REFERENCES asientocontable(id),
    movimiento_reversa_id    INT REFERENCES movimientotesoreria(id),
    registrado_por           INT           NOT NULL REFERENCES usuario(id),
    creado_en                TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT ck_enteramiento_concepto    CHECK (concepto IN ('ISR_NORES')),
    CONSTRAINT ck_enteramiento_formulario  CHECK (formulario IN ('SAT-1352', 'SAT-1331')),
    CONSTRAINT ck_enteramiento_periodo     CHECK (EXTRACT(DAY FROM periodo) = 1),
    CONSTRAINT ck_enteramiento_fecha_pago  CHECK (fecha_pago >= periodo),
    CONSTRAINT ck_enteramiento_boleta      CHECK (btrim(boleta) <> ''),
    CONSTRAINT ck_enteramiento_monto       CHECK (monto > 0),
    CONSTRAINT ck_enteramiento_estado      CHECK (estado IN ('VIGENTE', 'ANULADO')),
    CONSTRAINT ck_enteramiento_anulacion   CHECK ((estado = 'ANULADO') = (
        motivo_anulacion IS NOT NULL AND btrim(motivo_anulacion) <> ''
        AND anulado_por IS NOT NULL AND anulado_en IS NOT NULL
        AND asiento_reversa_id IS NOT NULL AND movimiento_reversa_id IS NOT NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_enteramiento_boleta ON enteramiento (formulario, boleta) WHERE estado = 'VIGENTE';
CREATE INDEX IF NOT EXISTS ix_enteramiento_periodo ON enteramiento (periodo, estado);

CREATE OR REPLACE FUNCTION fn_enteramiento_valida() RETURNS TRIGGER AS $$
DECLARE
    v_retenido  DECIMAL(14,2);
    v_enterado  DECIMAL(14,2);
BEGIN
    -- El lock por periodo serializa enteros simultaneos; el SELECT posterior ya ve lo confirmado (READ COMMITTED).
    PERFORM pg_advisory_xact_lock(hashtextextended('enteramiento:' || NEW.periodo::text, 0));

    IF NEW.estado <> 'VIGENTE' THEN
        RAISE EXCEPTION 'Un enteramiento nuevo debe registrarse como VIGENTE.' USING ERRCODE = '55000';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM cuentabancaria cb JOIN moneda m ON m.id = cb.moneda_id
                   WHERE cb.id = NEW.cuenta_bancaria_id AND m.es_funcional) THEN
        RAISE EXCEPTION 'El enteramiento solo se paga desde una cuenta bancaria en quetzales.' USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(SUM(monto_retenido), 0) INTO v_retenido
    FROM retencion
    WHERE concepto = 'ISR_NORES' AND date_trunc('month', fecha_operacion)::DATE = NEW.periodo;

    SELECT COALESCE(SUM(monto), 0) INTO v_enterado
    FROM enteramiento
    WHERE estado = 'VIGENTE' AND periodo = NEW.periodo;

    IF v_retenido = 0 OR NEW.monto > v_retenido - v_enterado THEN
        RAISE EXCEPTION 'El monto excede el saldo por enterar del período.' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_enteramiento_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.estado = 'VIGENTE' AND NEW.estado = 'ANULADO'
       AND (NEW.id, NEW.concepto, NEW.formulario, NEW.periodo, NEW.fecha_pago, NEW.boleta, NEW.monto,
            NEW.cuenta_bancaria_id, NEW.asiento_id, NEW.movimiento_tesoreria_id, NEW.registrado_por, NEW.creado_en)
          IS NOT DISTINCT FROM
           (OLD.id, OLD.concepto, OLD.formulario, OLD.periodo, OLD.fecha_pago, OLD.boleta, OLD.monto,
            OLD.cuenta_bancaria_id, OLD.asiento_id, OLD.movimiento_tesoreria_id, OLD.registrado_por, OLD.creado_en) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'El enteramiento % es inmutable: solo puede pasar de VIGENTE a ANULADO y no se elimina.', OLD.id
        USING ERRCODE = '55000';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_enteramiento_valida ON enteramiento;
CREATE TRIGGER trg_enteramiento_valida BEFORE INSERT ON enteramiento
FOR EACH ROW EXECUTE FUNCTION fn_enteramiento_valida();

DROP TRIGGER IF EXISTS trg_enteramiento_inmutable ON enteramiento;
CREATE TRIGGER trg_enteramiento_inmutable BEFORE UPDATE OR DELETE ON enteramiento
FOR EACH ROW EXECUTE FUNCTION fn_enteramiento_inmutable();


-- 2. Ajuste de retencion por nota de credito posterior al entero (aviso, se resuelve con motivo y referencia)

CREATE TABLE IF NOT EXISTS ajuste_retencion (
    id                     SERIAL PRIMARY KEY,
    retencion_id           INT           NOT NULL REFERENCES retencion(id),
    nota_credito_id        INT           NOT NULL REFERENCES documentocxp(id),
    monto_nc               DECIMAL(14,2) NOT NULL,
    estado                 VARCHAR(10)   NOT NULL DEFAULT 'PENDIENTE',
    creado_por             INT           NOT NULL REFERENCES usuario(id),
    creado_en              TIMESTAMPTZ   NOT NULL DEFAULT now(),
    motivo_resolucion      VARCHAR(300),
    referencia_resolucion  VARCHAR(100),
    resuelto_por           INT REFERENCES usuario(id),
    resuelto_en            TIMESTAMPTZ,
    CONSTRAINT uq_ajuste_retencion_nc      UNIQUE (retencion_id, nota_credito_id),
    CONSTRAINT ck_ajuste_retencion_monto   CHECK (monto_nc > 0),
    CONSTRAINT ck_ajuste_retencion_estado  CHECK (estado IN ('PENDIENTE', 'RESUELTO')),
    CONSTRAINT ck_ajuste_retencion_resolucion CHECK (
        (estado = 'RESUELTO'
            AND btrim(COALESCE(motivo_resolucion, '')) <> '' AND btrim(COALESCE(referencia_resolucion, '')) <> ''
            AND resuelto_por IS NOT NULL AND resuelto_en IS NOT NULL)
        OR (estado = 'PENDIENTE'
            AND motivo_resolucion IS NULL AND referencia_resolucion IS NULL
            AND resuelto_por IS NULL AND resuelto_en IS NULL))
);

CREATE INDEX IF NOT EXISTS ix_ajuste_retencion_estado ON ajuste_retencion (estado);

CREATE OR REPLACE FUNCTION fn_ajuste_retencion_valida() RETURNS TRIGGER AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM retencion r
        JOIN documentocxp nc ON nc.documento_origen_id = r.documento_cxp_id
        WHERE r.id = NEW.retencion_id AND r.concepto = 'ISR_NORES'
          AND nc.id = NEW.nota_credito_id AND nc.tipo_documento = 'NotaCredito') THEN
        RAISE EXCEPTION 'La nota de crédito % no corresponde al documento de la retención %.', NEW.nota_credito_id, NEW.retencion_id
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_ajuste_retencion_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.estado = 'PENDIENTE' AND NEW.estado = 'RESUELTO'
       AND (NEW.id, NEW.retencion_id, NEW.nota_credito_id, NEW.monto_nc, NEW.creado_por, NEW.creado_en)
          IS NOT DISTINCT FROM
           (OLD.id, OLD.retencion_id, OLD.nota_credito_id, OLD.monto_nc, OLD.creado_por, OLD.creado_en) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'El ajuste de retención % solo puede pasar de PENDIENTE a RESUELTO y no se elimina.', OLD.id
        USING ERRCODE = '55000';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_ajuste_retencion_valida ON ajuste_retencion;
CREATE TRIGGER trg_ajuste_retencion_valida BEFORE INSERT ON ajuste_retencion
FOR EACH ROW EXECUTE FUNCTION fn_ajuste_retencion_valida();

DROP TRIGGER IF EXISTS trg_ajuste_retencion_inmutable ON ajuste_retencion;
CREATE TRIGGER trg_ajuste_retencion_inmutable BEFORE UPDATE OR DELETE ON ajuste_retencion
FOR EACH ROW EXECUTE FUNCTION fn_ajuste_retencion_inmutable();


-- 3. Feriados: medio_dia FALSE = dia inhabil; medio_dia TRUE = dia habil (informativo). creado_por NULL = semilla.

CREATE TABLE IF NOT EXISTS feriado (
    fecha       DATE PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL,
    medio_dia   BOOLEAN      NOT NULL DEFAULT FALSE,
    creado_por  INT REFERENCES usuario(id),
    creado_en   TIMESTAMPTZ  NOT NULL DEFAULT now()
);

INSERT INTO feriado (fecha, nombre, medio_dia) VALUES
    (DATE '2026-01-01', 'Ano Nuevo',                                        FALSE),
    (DATE '2026-04-02', 'Jueves Santo',                                     FALSE),
    (DATE '2026-04-03', 'Viernes Santo',                                    FALSE),
    (DATE '2026-04-04', 'Sabado Santo',                                     FALSE),
    (DATE '2026-05-01', 'Dia del Trabajo',                                  FALSE),
    (DATE '2026-06-29', 'Dia del Ejercito (30 de junio trasladado)',        FALSE),
    (DATE '2026-09-15', 'Independencia',                                    FALSE),
    (DATE '2026-10-20', 'Revolucion',                                       FALSE),
    (DATE '2026-12-24', 'Nochebuena (desde las 12:00)',                     TRUE),
    (DATE '2026-12-25', 'Navidad',                                          FALSE),
    (DATE '2026-12-31', 'Fin de ano (desde las 12:00)',                     TRUE),
    (DATE '2027-01-01', 'Ano Nuevo',                                        FALSE),
    (DATE '2027-03-25', 'Jueves Santo',                                     FALSE),
    (DATE '2027-03-26', 'Viernes Santo',                                    FALSE),
    (DATE '2027-03-27', 'Sabado Santo',                                     FALSE),
    (DATE '2027-06-28', 'Dia del Ejercito (30 de junio trasladado)',        FALSE),
    (DATE '2027-09-15', 'Independencia',                                    FALSE),
    (DATE '2027-10-20', 'Revolucion',                                       FALSE),
    (DATE '2027-11-01', 'Todos los Santos',                                 FALSE),
    (DATE '2027-12-24', 'Nochebuena (desde las 12:00)',                     TRUE),
    (DATE '2027-12-31', 'Fin de ano (desde las 12:00)',                     TRUE)
ON CONFLICT (fecha) DO NOTHING;


-- 4. Contraparte: pais (ISO alfa-2; la API lo exige para no residentes)

ALTER TABLE contraparte ADD COLUMN IF NOT EXISTS pais CHAR(2);

ALTER TABLE contraparte DROP CONSTRAINT IF EXISTS ck_contraparte_pais;
ALTER TABLE contraparte
    ADD CONSTRAINT ck_contraparte_pais CHECK (pais IS NULL OR pais ~ '^[A-Z]{2}$');


-- 5. Catalogo: grupo 2 y cuenta del ISR retenido a no residentes; solo se crea lo que falta

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
VALUES ('2', 'Pasivo', 'Pasivo', 'Acreedora', NULL)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT '2.1', 'Pasivo corriente', p.tipo, p.naturaleza, p.id
FROM cuentacontable p
WHERE p.codigo = '2' AND p.tipo = 'Pasivo'
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT '2.1.03', 'ISR retenido a no residentes por pagar', p.tipo, p.naturaleza, p.id
FROM cuentacontable p
WHERE p.codigo = '2.1' AND p.tipo = 'Pasivo'
ON CONFLICT (codigo) DO NOTHING;

UPDATE configuracionfiscal
SET cuenta_isr_nr_retenido_pagar_id = (
    SELECT id FROM cuentacontable WHERE codigo = '2.1.03' AND tipo = 'Pasivo' AND activa)
WHERE id = 1 AND cuenta_isr_nr_retenido_pagar_id IS NULL;


-- 6. Vistas: saldo por enterar del periodo y reporte (enterado/saldo son del periodo de la fila, no se suman por fila)

CREATE OR REPLACE VIEW vw_saldo_enterar AS
WITH ret AS (
    SELECT date_trunc('month', r.fecha_operacion)::DATE AS periodo, SUM(r.monto_retenido) AS retenido
    FROM retencion r
    WHERE r.concepto = 'ISR_NORES'
    GROUP BY 1
), ent AS (
    SELECT e.periodo, SUM(e.monto) AS enterado
    FROM enteramiento e
    WHERE e.estado = 'VIGENTE'
    GROUP BY 1
)
SELECT periodo,
       COALESCE(ret.retenido, 0)::NUMERIC(14,2) AS retenido,
       COALESCE(ent.enterado, 0)::NUMERIC(14,2) AS enterado,
       (COALESCE(ret.retenido, 0) - COALESCE(ent.enterado, 0))::NUMERIC(14,2) AS saldo
FROM ret FULL JOIN ent USING (periodo);

CREATE OR REPLACE VIEW vw_retencion_reporte AS
SELECT
    r.id AS retencion_id,
    r.concepto,
    CASE WHEN r.concepto IN ('ISR_ROS', 'ISR_NORES') THEN 'EFECTUADA' ELSE 'RECIBIDA' END AS sentido,
    r.contraparte_id,
    c.nit,
    c.nombre,
    COALESCE(r.documento_cxp_id, r.documento_cxc_id) AS documento_id,
    COALESCE(dp.tipo_documento, dc.tipo_documento) AS documento_tipo,
    COALESCE(dp.numero, dc.numero) AS documento_numero,
    COALESCE(dp.dte_serie, dc.dte_serie) AS dte_serie,
    COALESCE(dp.dte_numero, dc.dte_numero) AS dte_numero,
    COALESCE(dp.dte_uuid, dc.dte_uuid) AS dte_uuid,
    r.fecha_documento,
    r.fecha_operacion,
    date_trunc('month', CASE WHEN r.concepto = 'ISR_NORES' THEN r.fecha_operacion ELSE r.fecha_documento END)::DATE AS periodo_fiscal,
    r.base,
    r.tasa,
    r.monto_calculado,
    r.monto_retenido,
    r.asiento_id,
    a.fecha AS fecha_asiento,
    k.id AS constancia_id,
    k.numero AS constancia_numero,
    k.fecha AS constancia_fecha,
    k.formulario,
    k.fecha_pago_formulario,
    k.periodo_declarado,
    k.estado AS constancia_estado,
    r.tipo_renta_codigo,
    CASE WHEN r.concepto = 'ISR_NORES' THEN pr.nombre END AS tipo_renta_nombre,
    mo.codigo AS moneda_codigo,
    r.monto_origen,
    r.tipo_cambio,
    r.tipo_cambio_fecha,
    r.aplicacion_pago_proveedor_id,
    COALESCE(s.enterado, 0)::NUMERIC(14,2) AS enterado_gtq,
    COALESCE(s.saldo, 0)::NUMERIC(14,2) AS saldo_por_enterar_gtq,
    c.pais,
    EXISTS (SELECT 1 FROM ajuste_retencion aj WHERE aj.retencion_id = r.id AND aj.estado = 'PENDIENTE') AS ajuste_pendiente
FROM retencion r
JOIN contraparte c ON c.id = r.contraparte_id
JOIN asientocontable a ON a.id = r.asiento_id
JOIN parametroretencion pr ON pr.id = r.parametro_id
LEFT JOIN moneda mo ON mo.id = r.moneda_id
LEFT JOIN documentocxp dp ON dp.id = r.documento_cxp_id
LEFT JOIN documentocxc dc ON dc.id = r.documento_cxc_id
LEFT JOIN vw_saldo_enterar s
       ON r.concepto = 'ISR_NORES' AND s.periodo = date_trunc('month', r.fecha_operacion)::DATE
LEFT JOIN LATERAL (
    SELECT cr.id, cr.numero, cr.fecha, cr.formulario, cr.fecha_pago_formulario, cr.periodo_declarado, cr.estado
    FROM constanciaretencion cr
    WHERE cr.retencion_id = r.id
    ORDER BY (cr.estado = 'EMITIDA') DESC, cr.id DESC
    LIMIT 1
) k ON TRUE;


-- 7. Permisos de delta_api (14 se aplica antes de estas tablas)

REVOKE EXECUTE ON FUNCTION fn_enteramiento_valida(), fn_enteramiento_inmutable(),
                           fn_ajuste_retencion_valida(), fn_ajuste_retencion_inmutable() FROM PUBLIC;

GRANT SELECT, INSERT, UPDATE ON enteramiento, ajuste_retencion TO delta_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON feriado TO delta_api;
GRANT SELECT ON vw_saldo_enterar, vw_retencion_reporte TO delta_api;
GRANT USAGE ON SEQUENCE enteramiento_id_seq, ajuste_retencion_id_seq TO delta_api;
