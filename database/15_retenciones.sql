-- =====================================================================
-- 15_retenciones.sql
-- Fiscal I2: retenciones de ISR a proveedores, IVA retenido por clientes,
-- parámetros con vigencia, constancias y saldo neto de CxP.
-- =====================================================================


-- 1. Datos fiscales de la contraparte

ALTER TABLE contraparte DROP CONSTRAINT ck_contraparte_regimen_isr;

ALTER TABLE contraparte
    ALTER COLUMN regimen_isr SET DEFAULT 'NO_DEFINIDO',
    ADD COLUMN isr_fundamento  VARCHAR(200),
    ADD COLUMN tipo_agente_iva VARCHAR(20);

UPDATE contraparte SET regimen_isr = 'NO_DEFINIDO'
WHERE tipo = 'Proveedor' AND regimen_isr = 'UTILIDADES';

UPDATE contraparte SET tipo_agente_iva = 'AGENTE_PRIVADO'
WHERE es_agente_retencion_iva;

ALTER TABLE contraparte
    ADD CONSTRAINT ck_contraparte_regimen_isr CHECK
        (regimen_isr IN ('UTILIDADES', 'SIMPLIFICADO', 'PAGO_DIRECTO', 'EXENTO', 'NO_DEFINIDO')),
    ADD CONSTRAINT ck_contraparte_isr_fundamento CHECK
        (regimen_isr NOT IN ('EXENTO', 'PAGO_DIRECTO') OR btrim(COALESCE(isr_fundamento, '')) <> ''),
    ADD CONSTRAINT ck_contraparte_tipo_agente CHECK
        (tipo_agente_iva IS NULL OR tipo_agente_iva IN ('SECTOR_PUBLICO', 'AGENTE_PRIVADO')),
    ADD CONSTRAINT ck_contraparte_agente_iva CHECK
        (es_agente_retencion_iva = (tipo_agente_iva IS NOT NULL));


-- 2. Configuración fiscal: cuentas de retención

ALTER TABLE configuracionfiscal
    ADD COLUMN cuenta_isr_retenido_pagar_id    INT REFERENCES cuentacontable(id),
    ADD COLUMN cuenta_iva_retenido_clientes_id INT REFERENCES cuentacontable(id),
    ADD CONSTRAINT ck_configuracionfiscal_cuentas_retencion CHECK (
        (cuenta_isr_retenido_pagar_id IS NULL OR cuenta_isr_retenido_pagar_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_iva_retenido_clientes_id, -1)))
        AND (cuenta_iva_retenido_clientes_id IS NULL OR cuenta_iva_retenido_clientes_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1))));


-- 3. Parámetros de retención con vigencia

CREATE TABLE parametroretencion (
    id              SERIAL PRIMARY KEY,
    codigo          VARCHAR(40)  NOT NULL,
    concepto        VARCHAR(25)  NOT NULL,
    nombre          VARCHAR(120) NOT NULL,
    tasa_base       DECIMAL(7,4),
    tramo_limite    DECIMAL(14,2),
    tasa_excedente  DECIMAL(7,4),
    umbral_minimo   DECIMAL(14,2) NOT NULL DEFAULT 0,
    umbral_sobre    VARCHAR(5)    NOT NULL DEFAULT 'BASE',
    dia_limite      SMALLINT,
    articulo_legal  VARCHAR(200)  NOT NULL,
    nota            VARCHAR(300)  NOT NULL DEFAULT 'Referencial, validar con asesor',
    vigente_desde   DATE          NOT NULL,
    vigente_hasta   DATE,
    activo          BOOLEAN       NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_parametroretencion_codigo_desde UNIQUE (codigo, vigente_desde),
    CONSTRAINT ck_parametroretencion_concepto CHECK
        (concepto IN ('ISR_PROVEEDOR_ROS', 'IVA_RETENIDO_CLIENTE', 'PLAZO_CONSTANCIA')),
    CONSTRAINT ck_parametroretencion_umbral_sobre CHECK (umbral_sobre IN ('BASE', 'TOTAL')),
    CONSTRAINT ck_parametroretencion_forma CHECK (
        (concepto = 'PLAZO_CONSTANCIA'
            AND dia_limite IS NOT NULL AND dia_limite BETWEEN 1 AND 28
            AND tasa_base IS NULL AND tramo_limite IS NULL AND tasa_excedente IS NULL)
        OR (concepto <> 'PLAZO_CONSTANCIA'
            AND dia_limite IS NULL
            AND tasa_base IS NOT NULL AND tasa_base > 0 AND tasa_base <= 100)),
    CONSTRAINT ck_parametroretencion_tramo CHECK (
        (tramo_limite IS NULL AND tasa_excedente IS NULL)
        OR (tramo_limite IS NOT NULL AND tasa_excedente IS NOT NULL
            AND concepto = 'ISR_PROVEEDOR_ROS'
            AND tramo_limite > 0 AND tasa_excedente > 0 AND tasa_excedente <= 100)),
    CONSTRAINT ck_parametroretencion_umbral CHECK (umbral_minimo >= 0),
    CONSTRAINT ck_parametroretencion_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);


-- 4. Retenciones registradas (instantánea; la API solo inserta y consulta)

CREATE TABLE retencion (
    id                          SERIAL PRIMARY KEY,
    concepto                    VARCHAR(15)   NOT NULL,
    contraparte_id              INT           NOT NULL REFERENCES contraparte(id),
    documento_cxp_id            INT REFERENCES documentocxp(id),
    documento_cxc_id            INT REFERENCES documentocxc(id),
    aplicacion_pago_cliente_id  INT REFERENCES aplicacionpagocliente(id),
    parametro_id                INT           NOT NULL REFERENCES parametroretencion(id),
    fecha_documento             DATE          NOT NULL,
    fecha_operacion             DATE          NOT NULL,
    cuenta_id                   INT           NOT NULL REFERENCES cuentacontable(id),
    base                        DECIMAL(14,2) NOT NULL,
    tasa                        DECIMAL(7,4)  NOT NULL,
    monto_calculado             DECIMAL(14,2) NOT NULL,
    monto_retenido              DECIMAL(14,2) NOT NULL,
    motivo_ajuste               VARCHAR(300),
    asiento_id                  INT           NOT NULL REFERENCES asientocontable(id),
    registrado_por              INT           NOT NULL REFERENCES usuario(id),
    creado_en                   TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT ck_retencion_concepto CHECK (concepto IN ('ISR_ROS', 'IVA_CLIENTE')),
    CONSTRAINT ck_retencion_origen CHECK (
        (concepto = 'ISR_ROS'
            AND documento_cxp_id IS NOT NULL
            AND documento_cxc_id IS NULL AND aplicacion_pago_cliente_id IS NULL)
        OR (concepto = 'IVA_CLIENTE'
            AND documento_cxp_id IS NULL
            AND documento_cxc_id IS NOT NULL AND aplicacion_pago_cliente_id IS NOT NULL)),
    CONSTRAINT ck_retencion_montos CHECK (base > 0 AND monto_calculado >= 0 AND monto_retenido > 0),
    CONSTRAINT ck_retencion_ajuste CHECK (
        concepto <> 'ISR_ROS'
        OR monto_retenido = monto_calculado
        OR (motivo_ajuste IS NOT NULL AND btrim(motivo_ajuste) <> ''))
);

CREATE UNIQUE INDEX ux_retencion_isr_documento ON retencion (documento_cxp_id) WHERE concepto = 'ISR_ROS';
CREATE UNIQUE INDEX ux_retencion_iva_documento ON retencion (documento_cxc_id) WHERE concepto = 'IVA_CLIENTE';
CREATE INDEX ix_retencion_fecha_concepto ON retencion (fecha_documento, concepto);
CREATE INDEX ix_retencion_contraparte ON retencion (contraparte_id);
CREATE INDEX ix_retencion_parametro ON retencion (parametro_id);
CREATE INDEX ix_retencion_asiento_cuenta ON retencion (asiento_id, cuenta_id);


-- 5. Triggers de parámetros de retención

CREATE OR REPLACE FUNCTION fn_parametroretencion_sin_traslape() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.vigente_hasta IS NOT NULL AND NEW.vigente_hasta < NEW.vigente_desde THEN
        RETURN NEW;
    END IF;

    IF EXISTS (
        SELECT 1 FROM parametroretencion p
        WHERE p.codigo = NEW.codigo
          AND p.id <> COALESCE(NEW.id, -1)
          AND daterange(p.vigente_desde, p.vigente_hasta, '[]') && daterange(NEW.vigente_desde, NEW.vigente_hasta, '[]')
    ) THEN
        RAISE EXCEPTION 'Ya existe una versión del parámetro de retención % cuya vigencia se traslapa con la indicada.', NEW.codigo
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_parametroretencion_sin_traslape
BEFORE INSERT OR UPDATE ON parametroretencion
FOR EACH ROW EXECUTE FUNCTION fn_parametroretencion_sin_traslape();

CREATE OR REPLACE FUNCTION fn_parametroretencion_inmutable_si_usado() RETURNS TRIGGER AS $$
DECLARE
    v_en_uso       BOOLEAN;
    v_ultima_fecha DATE;
BEGIN
    SELECT COUNT(*) > 0, MAX(r.fecha_documento) INTO v_en_uso, v_ultima_fecha
    FROM retencion r WHERE r.parametro_id = OLD.id;

    IF NOT v_en_uso THEN
        RETURN NEW;
    END IF;

    IF (NEW.codigo, NEW.concepto, NEW.nombre, NEW.tasa_base, NEW.tramo_limite, NEW.tasa_excedente,
        NEW.umbral_minimo, NEW.umbral_sobre, NEW.dia_limite, NEW.articulo_legal, NEW.vigente_desde)
       IS DISTINCT FROM
       (OLD.codigo, OLD.concepto, OLD.nombre, OLD.tasa_base, OLD.tramo_limite, OLD.tasa_excedente,
        OLD.umbral_minimo, OLD.umbral_sobre, OLD.dia_limite, OLD.articulo_legal, OLD.vigente_desde) THEN
        RAISE EXCEPTION 'El parámetro de retención % ya fue usado en retenciones registradas; solo se pueden modificar su nota, su estado y el cierre de su vigencia. Cree una nueva versión.', OLD.codigo
            USING ERRCODE = '55000';
    END IF;

    IF NEW.vigente_hasta IS NOT NULL AND NEW.vigente_hasta < v_ultima_fecha THEN
        RAISE EXCEPTION 'No se puede cerrar la vigencia del parámetro de retención % antes del %: existen retenciones con esa fecha.',
            OLD.codigo, to_char(v_ultima_fecha, 'YYYY-MM-DD')
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_parametroretencion_inmutable_si_usado
BEFORE UPDATE ON parametroretencion
FOR EACH ROW EXECUTE FUNCTION fn_parametroretencion_inmutable_si_usado();

REVOKE EXECUTE ON FUNCTION fn_parametroretencion_sin_traslape(), fn_parametroretencion_inmutable_si_usado() FROM PUBLIC;


-- 6. Semilla de parámetros

INSERT INTO parametroretencion
    (codigo, concepto, nombre, tasa_base, tramo_limite, tasa_excedente, umbral_minimo, umbral_sobre,
     dia_limite, articulo_legal, vigente_desde) VALUES
    ('ISR_PROVEEDOR_ROS', 'ISR_PROVEEDOR_ROS', 'ISR retenido a proveedores del régimen simplificado',
     5, 30000.00, 7, 2500.00, 'BASE', NULL,
     'Decreto 10-2012, art. 48, reformado por Decreto 19-2013, art. 15; Reglamento, art. 35', DATE '2014-01-01'),
    ('IVA_CLIENTE_SECTOR_PUBLICO', 'IVA_RETENIDO_CLIENTE', 'IVA retenido por el sector público',
     25, NULL, NULL, 30000.00, 'TOTAL', NULL,
     'Decreto 20-2006, arts. 1 a 11', DATE '2007-01-01'),
    ('IVA_CLIENTE_AGENTE_PRIVADO', 'IVA_RETENIDO_CLIENTE', 'IVA retenido por agente de retención privado',
     15, NULL, NULL, 2500.00, 'TOTAL', NULL,
     'Decreto 20-2006, arts. 1 a 11', DATE '2007-01-01'),
    ('PLAZO_CONSTANCIA_ISR', 'PLAZO_CONSTANCIA', 'Día límite del mes siguiente para la constancia de ISR',
     NULL, NULL, NULL, 0, 'BASE', 5,
     'Decreto 10-2012, art. 48', DATE '2014-01-01');


-- 7. Constancias de retención

CREATE TABLE constanciaretencion (
    id                        SERIAL PRIMARY KEY,
    retencion_id              INT          NOT NULL REFERENCES retencion(id),
    tipo                      VARCHAR(3)   NOT NULL,
    retenedor_contraparte_id  INT REFERENCES contraparte(id),
    numero                    VARCHAR(30)  NOT NULL,
    fecha                     DATE         NOT NULL,
    formulario                VARCHAR(10),
    fecha_pago_formulario     DATE,
    periodo_declarado         DATE,
    estado                    VARCHAR(10)  NOT NULL DEFAULT 'EMITIDA',
    motivo_anulacion          VARCHAR(300),
    creado_en                 TIMESTAMPTZ  NOT NULL DEFAULT now(),
    creado_por                INT          NOT NULL REFERENCES usuario(id),
    actualizado_en            TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_por           INT REFERENCES usuario(id),
    CONSTRAINT ck_constancia_tipo CHECK (tipo IN ('ISR', 'IVA')),
    CONSTRAINT ck_constancia_numero CHECK (btrim(numero) <> ''),
    CONSTRAINT ck_constancia_formulario CHECK
        (formulario IS NULL OR formulario IN ('SAT-1331', 'SAT-1352', 'SAT-2340')),
    CONSTRAINT ck_constancia_periodo CHECK
        (periodo_declarado IS NULL OR EXTRACT(DAY FROM periodo_declarado) = 1),
    CONSTRAINT ck_constancia_estado CHECK (estado IN ('EMITIDA', 'ANULADA')),
    CONSTRAINT ck_constancia_anulacion CHECK
        ((estado = 'ANULADA') = (motivo_anulacion IS NOT NULL))
);

CREATE UNIQUE INDEX ux_constancia_retencion ON constanciaretencion (retencion_id) WHERE estado = 'EMITIDA';
CREATE UNIQUE INDEX ux_constancia_numero
    ON constanciaretencion (tipo, COALESCE(retenedor_contraparte_id, 0), numero) WHERE estado = 'EMITIDA';
CREATE INDEX ix_constancia_retencion_id ON constanciaretencion (retencion_id);


-- 8. Documentos CxP: revisión de retención

ALTER TABLE documentocxp
    ADD COLUMN revision_retencion VARCHAR(10) NOT NULL DEFAULT 'NINGUNA',
    ADD COLUMN revision_motivo    VARCHAR(300),
    ADD CONSTRAINT ck_documentocxp_revision CHECK (revision_retencion IN ('NINGUNA', 'PENDIENTE', 'RESUELTA')),
    ADD CONSTRAINT ck_documentocxp_revision_motivo CHECK (revision_retencion <> 'RESUELTA' OR revision_motivo IS NOT NULL);


-- 9. Saldo neto de CxP: restan la retención de ISR

CREATE OR REPLACE FUNCTION fn_nota_credito_valida_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_origen      documentocxp%ROWTYPE;
    v_pagado      DECIMAL(14,2);
    v_notas       DECIMAL(14,2);
    v_retencion   DECIMAL(14,2);
    v_saldo       DECIMAL(14,2);
BEGIN
    IF NEW.documento_origen_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT * INTO v_origen FROM documentocxp WHERE id = NEW.documento_origen_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El documento de origen no existe.' USING ERRCODE = '55000';
    END IF;

    IF v_origen.proveedor_id <> NEW.proveedor_id THEN
        RAISE EXCEPTION 'El documento de origen pertenece a otro proveedor.' USING ERRCODE = '55000';
    END IF;

    IF v_origen.tipo_documento <> 'Factura' OR v_origen.estado <> 'Vigente' THEN
        RAISE EXCEPTION 'El documento de origen debe ser una factura vigente.' USING ERRCODE = '55000';
    END IF;

    IF NEW.estado = 'Vigente' THEN
        SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_pagado
        FROM aplicacionpagoproveedor WHERE documento_id = v_origen.id;

        SELECT COALESCE(SUM(monto_total), 0) INTO v_notas
        FROM documentocxp WHERE documento_origen_id = v_origen.id AND estado = 'Vigente';

        SELECT COALESCE(SUM(monto_retenido), 0) INTO v_retencion
        FROM retencion WHERE documento_cxp_id = v_origen.id AND concepto = 'ISR_ROS';

        v_saldo := v_origen.monto_total - v_pagado - v_notas - v_retencion;

        IF NEW.monto_total > v_saldo THEN
            RAISE EXCEPTION 'El monto de la nota de crédito (Q%) excede el saldo pendiente del documento de origen (Q%).',
                NEW.monto_total, v_saldo
                USING ERRCODE = '55000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_limite_pago_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_monto_total     DECIMAL(14,2);
    v_tipo_documento  VARCHAR(20);
    v_notas_credito   DECIMAL(14,2);
    v_retencion       DECIMAL(14,2);
    v_ya_aplicado     DECIMAL(14,2);
BEGIN
    SELECT monto_total, tipo_documento INTO v_monto_total, v_tipo_documento
    FROM documentocxp WHERE id = NEW.documento_id;

    IF v_tipo_documento = 'NotaCredito' THEN
        RAISE EXCEPTION 'RN-05: una nota de crédito no admite pagos ni cobros (documento %)', NEW.documento_id
            USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(SUM(monto_total), 0) INTO v_notas_credito
    FROM documentocxp WHERE documento_origen_id = NEW.documento_id AND estado = 'Vigente';

    SELECT COALESCE(SUM(monto_retenido), 0) INTO v_retencion
    FROM retencion WHERE documento_cxp_id = NEW.documento_id AND concepto = 'ISR_ROS';

    v_monto_total := v_monto_total - v_notas_credito - v_retencion;

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

CREATE OR REPLACE VIEW vw_saldodocumentocxp AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total
         - COALESCE((
             SELECT SUM(ap.monto_aplicado)
             FROM aplicacionpagoproveedor ap
             WHERE ap.documento_id = d.id
         ), 0)
         - COALESCE((
             SELECT SUM(nc.monto_total)
             FROM documentocxp nc
             WHERE nc.documento_origen_id = d.id AND nc.estado = 'Vigente'
         ), 0)
         - COALESCE((
             SELECT SUM(r.monto_retenido)
             FROM retencion r
             WHERE r.documento_cxp_id = d.id AND r.concepto = 'ISR_ROS'
         ), 0)
    END AS saldo_pendiente
FROM documentocxp d;


-- 10. Vista del reporte de retenciones

CREATE OR REPLACE VIEW vw_retencion_reporte AS
SELECT
    r.id AS retencion_id,
    r.concepto,
    CASE WHEN r.concepto = 'ISR_ROS' THEN 'EFECTUADA' ELSE 'RECIBIDA' END AS sentido,
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
    date_trunc('month', r.fecha_documento)::DATE AS periodo_fiscal,
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
    k.estado AS constancia_estado
FROM retencion r
JOIN contraparte c ON c.id = r.contraparte_id
JOIN asientocontable a ON a.id = r.asiento_id
LEFT JOIN documentocxp dp ON dp.id = r.documento_cxp_id
LEFT JOIN documentocxc dc ON dc.id = r.documento_cxc_id
LEFT JOIN LATERAL (
    SELECT cr.id, cr.numero, cr.fecha, cr.formulario, cr.fecha_pago_formulario, cr.periodo_declarado, cr.estado
    FROM constanciaretencion cr
    WHERE cr.retencion_id = r.id
    ORDER BY (cr.estado = 'EMITIDA') DESC, cr.id DESC
    LIMIT 1
) k ON TRUE;


-- 11. Permisos de delta_api

GRANT SELECT, INSERT, UPDATE ON parametroretencion, constanciaretencion TO delta_api;
GRANT SELECT, INSERT ON retencion TO delta_api;
GRANT SELECT ON vw_retencion_reporte TO delta_api;
GRANT USAGE ON SEQUENCE parametroretencion_id_seq, retencion_id_seq, constanciaretencion_id_seq TO delta_api;
