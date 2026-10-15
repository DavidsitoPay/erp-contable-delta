-- =====================================================================
-- 16_multimoneda.sql
-- Fiscal I3a: multimoneda (GTQ/USD/EUR), tipos de cambio, diferencial
-- cambiario realizado y revaluacion de fin de mes.
-- =====================================================================


-- 1. Moneda

ALTER TABLE moneda ADD COLUMN codigo_banguat SMALLINT;

INSERT INTO moneda (codigo, nombre, es_funcional, activa)
SELECT 'USD', 'Dólar estadounidense', FALSE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM moneda WHERE codigo = 'USD');

INSERT INTO moneda (codigo, nombre, es_funcional, activa)
SELECT 'EUR', 'Euro', FALSE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM moneda WHERE codigo = 'EUR');

UPDATE moneda SET codigo_banguat = 2 WHERE codigo = 'USD';
UPDATE moneda SET codigo_banguat = 24 WHERE codigo = 'EUR';

CREATE UNIQUE INDEX ux_moneda_codigo_banguat ON moneda (codigo_banguat) WHERE codigo_banguat IS NOT NULL;
ALTER TABLE moneda ADD CONSTRAINT ck_moneda_funcional_sin_banguat CHECK (NOT es_funcional OR codigo_banguat IS NULL);

CREATE TRIGGER trg_prevenir_eliminacion_moneda
BEFORE DELETE ON moneda
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();


-- 2. Historial de tipos de cambio

ALTER TABLE historialtipocambio
    ADD COLUMN fuente             VARCHAR(10)  NOT NULL DEFAULT 'Manual',
    ADD COLUMN fecha_referencia   DATE,
    ADD COLUMN arrastrado         BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN cotizacion_venta   DECIMAL(12,6),
    ADD COLUMN cotizacion_compra  DECIMAL(12,6),
    ADD COLUMN referencia_usd     DECIMAL(12,6),
    ADD COLUMN motivo             VARCHAR(300),
    ADD COLUMN usuario_id         INT REFERENCES usuario(id),
    ADD COLUMN activa             BOOLEAN      NOT NULL DEFAULT TRUE,
    ADD COLUMN creado_en          TIMESTAMPTZ  NOT NULL DEFAULT now();

UPDATE historialtipocambio SET
    fecha_referencia = fecha,
    motivo = 'Registro anterior a la migración 16',
    usuario_id = (SELECT MIN(id) FROM usuario);

UPDATE historialtipocambio h SET activa = FALSE
WHERE h.id <> (SELECT MAX(h2.id) FROM historialtipocambio h2 WHERE h2.moneda_id = h.moneda_id AND h2.fecha = h.fecha);

ALTER TABLE historialtipocambio ALTER COLUMN fecha_referencia SET NOT NULL;

ALTER TABLE historialtipocambio
    ADD CONSTRAINT ck_historialtipocambio_tasa CHECK (tasa > 0),
    ADD CONSTRAINT ck_historialtipocambio_fuente CHECK (fuente IN ('Banguat', 'Manual')),
    ADD CONSTRAINT ck_historialtipocambio_referencia CHECK (fecha_referencia <= fecha),
    ADD CONSTRAINT ck_historialtipocambio_manual CHECK
        (fuente <> 'Manual' OR (usuario_id IS NOT NULL AND btrim(COALESCE(motivo, '')) <> '')),
    ADD CONSTRAINT ck_historialtipocambio_eur CHECK
        (cotizacion_venta IS NULL OR (referencia_usd IS NOT NULL AND cotizacion_venta > 0 AND referencia_usd > 0));

CREATE UNIQUE INDEX ux_historialtipocambio_activa ON historialtipocambio (moneda_id, fecha) WHERE activa;
CREATE INDEX ix_historialtipocambio_moneda_fecha ON historialtipocambio (moneda_id, fecha DESC);


-- 3. Configuración fiscal: cuentas de diferencial cambiario

ALTER TABLE configuracionfiscal
    ADD COLUMN cuenta_dif_ganancia_realizada_id INT REFERENCES cuentacontable(id),
    ADD COLUMN cuenta_dif_perdida_realizada_id  INT REFERENCES cuentacontable(id),
    ADD COLUMN cuenta_reval_ganancia_id         INT REFERENCES cuentacontable(id),
    ADD COLUMN cuenta_reval_perdida_id          INT REFERENCES cuentacontable(id);

ALTER TABLE configuracionfiscal
    ADD CONSTRAINT ck_configuracionfiscal_cuentas_cambiarias CHECK (
        (cuenta_dif_ganancia_realizada_id IS NULL OR cuenta_dif_ganancia_realizada_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_isr_retenido_pagar_id, -1), COALESCE(cuenta_iva_retenido_clientes_id, -1),
             COALESCE(cuenta_dif_perdida_realizada_id, -1), COALESCE(cuenta_reval_ganancia_id, -1),
             COALESCE(cuenta_reval_perdida_id, -1)))
        AND (cuenta_dif_perdida_realizada_id IS NULL OR cuenta_dif_perdida_realizada_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_isr_retenido_pagar_id, -1), COALESCE(cuenta_iva_retenido_clientes_id, -1),
             COALESCE(cuenta_dif_ganancia_realizada_id, -1), COALESCE(cuenta_reval_ganancia_id, -1),
             COALESCE(cuenta_reval_perdida_id, -1)))
        AND (cuenta_reval_ganancia_id IS NULL OR cuenta_reval_ganancia_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_isr_retenido_pagar_id, -1), COALESCE(cuenta_iva_retenido_clientes_id, -1),
             COALESCE(cuenta_dif_ganancia_realizada_id, -1), COALESCE(cuenta_dif_perdida_realizada_id, -1),
             COALESCE(cuenta_reval_perdida_id, -1)))
        AND (cuenta_reval_perdida_id IS NULL OR cuenta_reval_perdida_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_isr_retenido_pagar_id, -1), COALESCE(cuenta_iva_retenido_clientes_id, -1),
             COALESCE(cuenta_dif_ganancia_realizada_id, -1), COALESCE(cuenta_dif_perdida_realizada_id, -1),
             COALESCE(cuenta_reval_ganancia_id, -1))));


-- 4. Documentos CxC y CxP: moneda, importe original y tipo de cambio

ALTER TABLE documentocxc
    ADD COLUMN moneda_id              INT REFERENCES moneda(id),
    ADD COLUMN monto_total_original   DECIMAL(14,2),
    ADD COLUMN tipo_cambio_id         INT REFERENCES historialtipocambio(id),
    ADD COLUMN tipo_cambio_fuente     VARCHAR(10),
    ADD COLUMN tipo_cambio_arrastrado BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE lineadocumentocxc ADD COLUMN monto_linea_original DECIMAL(14,2);

ALTER TABLE documentocxp
    ADD COLUMN moneda_id              INT REFERENCES moneda(id),
    ADD COLUMN monto_total_original   DECIMAL(14,2),
    ADD COLUMN tipo_cambio_id         INT REFERENCES historialtipocambio(id),
    ADD COLUMN tipo_cambio_fuente     VARCHAR(10),
    ADD COLUMN tipo_cambio_arrastrado BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE lineadocumentocxp ADD COLUMN monto_linea_original DECIMAL(14,2);

UPDATE documentocxc SET
    moneda_id = (SELECT id FROM moneda WHERE es_funcional),
    monto_total_original = monto_total,
    tipo_cambio_aplicado = 1;
UPDATE lineadocumentocxc SET monto_linea_original = monto_linea;

UPDATE documentocxp SET
    moneda_id = (SELECT id FROM moneda WHERE es_funcional),
    monto_total_original = monto_total,
    tipo_cambio_aplicado = 1;
UPDATE lineadocumentocxp SET monto_linea_original = monto_linea;

ALTER TABLE documentocxc
    ALTER COLUMN moneda_id SET NOT NULL,
    ALTER COLUMN monto_total_original SET NOT NULL,
    ALTER COLUMN tipo_cambio_aplicado SET DEFAULT 1,
    ALTER COLUMN tipo_cambio_aplicado SET NOT NULL,
    ADD CONSTRAINT ck_documentocxc_tipo_cambio CHECK (tipo_cambio_aplicado > 0),
    ADD CONSTRAINT ck_documentocxc_fuente CHECK (tipo_cambio_fuente IS NULL OR tipo_cambio_fuente IN ('Banguat', 'Manual')),
    ADD CONSTRAINT ck_documentocxc_original CHECK (monto_total_original >= 0);
ALTER TABLE lineadocumentocxc ALTER COLUMN monto_linea_original SET NOT NULL;

ALTER TABLE documentocxp
    ALTER COLUMN moneda_id SET NOT NULL,
    ALTER COLUMN monto_total_original SET NOT NULL,
    ALTER COLUMN tipo_cambio_aplicado SET DEFAULT 1,
    ALTER COLUMN tipo_cambio_aplicado SET NOT NULL,
    ADD CONSTRAINT ck_documentocxp_tipo_cambio CHECK (tipo_cambio_aplicado > 0),
    ADD CONSTRAINT ck_documentocxp_fuente CHECK (tipo_cambio_fuente IS NULL OR tipo_cambio_fuente IN ('Banguat', 'Manual')),
    ADD CONSTRAINT ck_documentocxp_original CHECK (monto_total_original >= 0);
ALTER TABLE lineadocumentocxp ALTER COLUMN monto_linea_original SET NOT NULL;

CREATE INDEX ix_documentocxc_moneda ON documentocxc (moneda_id);
CREATE INDEX ix_documentocxc_tipo_cambio ON documentocxc (tipo_cambio_id) WHERE tipo_cambio_id IS NOT NULL;
CREATE INDEX ix_documentocxp_moneda ON documentocxp (moneda_id);
CREATE INDEX ix_documentocxp_tipo_cambio ON documentocxp (tipo_cambio_id) WHERE tipo_cambio_id IS NOT NULL;


-- 5. Recibos, pagos y aplicaciones

ALTER TABLE recibopagocliente
    ADD COLUMN moneda_id        INT REFERENCES moneda(id),
    ADD COLUMN monto_total_gtq  DECIMAL(14,2);
ALTER TABLE aplicacionpagocliente
    ADD COLUMN monto_aplicado_gtq  DECIMAL(14,2),
    ADD COLUMN valor_gtq           DECIMAL(14,2),
    ADD COLUMN diferencial_gtq     DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN tipo_cambio_pago_id INT REFERENCES historialtipocambio(id);

ALTER TABLE pagoproveedorcabecera
    ADD COLUMN moneda_id        INT REFERENCES moneda(id),
    ADD COLUMN monto_total_gtq  DECIMAL(14,2);
ALTER TABLE aplicacionpagoproveedor
    ADD COLUMN monto_aplicado_gtq  DECIMAL(14,2),
    ADD COLUMN valor_gtq           DECIMAL(14,2),
    ADD COLUMN diferencial_gtq     DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN tipo_cambio_pago_id INT REFERENCES historialtipocambio(id);

ALTER TABLE aplicacionpagocliente DISABLE TRIGGER trg_limite_pago_cxc;
ALTER TABLE aplicacionpagoproveedor DISABLE TRIGGER trg_limite_pago_cxp;

UPDATE recibopagocliente SET moneda_id = (SELECT id FROM moneda WHERE es_funcional), monto_total_gtq = monto_total;
UPDATE aplicacionpagocliente SET monto_aplicado_gtq = monto_aplicado, valor_gtq = monto_aplicado;
UPDATE pagoproveedorcabecera SET moneda_id = (SELECT id FROM moneda WHERE es_funcional), monto_total_gtq = monto_total;
UPDATE aplicacionpagoproveedor SET monto_aplicado_gtq = monto_aplicado, valor_gtq = monto_aplicado;

ALTER TABLE aplicacionpagocliente ENABLE TRIGGER trg_limite_pago_cxc;
ALTER TABLE aplicacionpagoproveedor ENABLE TRIGGER trg_limite_pago_cxp;

ALTER TABLE recibopagocliente
    ALTER COLUMN moneda_id SET NOT NULL,
    ALTER COLUMN monto_total_gtq SET NOT NULL;
ALTER TABLE aplicacionpagocliente
    ALTER COLUMN monto_aplicado_gtq SET NOT NULL,
    ALTER COLUMN valor_gtq SET NOT NULL,
    ADD CONSTRAINT ck_aplicacionpagocliente_diferencial CHECK (diferencial_gtq = valor_gtq - monto_aplicado_gtq);

ALTER TABLE pagoproveedorcabecera
    ALTER COLUMN moneda_id SET NOT NULL,
    ALTER COLUMN monto_total_gtq SET NOT NULL;
ALTER TABLE aplicacionpagoproveedor
    ALTER COLUMN monto_aplicado_gtq SET NOT NULL,
    ALTER COLUMN valor_gtq SET NOT NULL,
    ADD CONSTRAINT ck_aplicacionpagoproveedor_diferencial CHECK (diferencial_gtq = monto_aplicado_gtq - valor_gtq);

CREATE INDEX ix_aplicacionpagocliente_tc ON aplicacionpagocliente (tipo_cambio_pago_id) WHERE tipo_cambio_pago_id IS NOT NULL;
CREATE INDEX ix_aplicacionpagoproveedor_tc ON aplicacionpagoproveedor (tipo_cambio_pago_id) WHERE tipo_cambio_pago_id IS NOT NULL;


-- 6. Retención: importe en la moneda del documento

ALTER TABLE retencion ADD COLUMN monto_retenido_original DECIMAL(14,2);
UPDATE retencion SET monto_retenido_original = monto_retenido;
ALTER TABLE retencion
    ALTER COLUMN monto_retenido_original SET NOT NULL,
    ADD CONSTRAINT ck_retencion_monto_original CHECK (monto_retenido_original > 0);


-- 7. Cuentas bancarias y movimientos de tesorería

ALTER TABLE cuentabancaria ADD COLUMN moneda_id INT REFERENCES moneda(id);
UPDATE cuentabancaria SET moneda_id = (SELECT id FROM moneda WHERE es_funcional);
ALTER TABLE cuentabancaria ALTER COLUMN moneda_id SET NOT NULL;
CREATE INDEX ix_cuentabancaria_moneda ON cuentabancaria (moneda_id);

ALTER TABLE movimientotesoreria
    ADD COLUMN monto_gtq       DECIMAL(14,2),
    ADD COLUMN tipo_cambio     DECIMAL(12,6),
    ADD COLUMN tipo_cambio_id  INT REFERENCES historialtipocambio(id);

ALTER TABLE movimientotesoreria DISABLE TRIGGER trg_movimiento_inmutable;
UPDATE movimientotesoreria SET monto_gtq = monto, tipo_cambio = 1;
ALTER TABLE movimientotesoreria ENABLE TRIGGER trg_movimiento_inmutable;

ALTER TABLE movimientotesoreria
    ALTER COLUMN monto_gtq SET NOT NULL,
    ALTER COLUMN tipo_cambio SET NOT NULL,
    ADD CONSTRAINT ck_movimiento_monto_gtq CHECK (monto_gtq > 0),
    ADD CONSTRAINT ck_movimiento_tipo_cambio CHECK (tipo_cambio > 0);
CREATE INDEX ix_movimiento_tipo_cambio ON movimientotesoreria (tipo_cambio_id) WHERE tipo_cambio_id IS NOT NULL;


-- 8. Revaluación de fin de mes

CREATE TABLE revaluacion (
    id                            SERIAL PRIMARY KEY,
    moneda_id                     INT           NOT NULL REFERENCES moneda(id),
    anio                          SMALLINT      NOT NULL,
    mes                           SMALLINT      NOT NULL,
    fecha_cierre                  DATE          NOT NULL,
    tipo_cambio_id                INT           NOT NULL REFERENCES historialtipocambio(id),
    tasa                          DECIMAL(12,6) NOT NULL,
    total_ganancia                DECIMAL(14,2) NOT NULL,
    total_perdida                 DECIMAL(14,2) NOT NULL,
    estado                        VARCHAR(10)   NOT NULL DEFAULT 'Vigente',
    asiento_id                    INT           NOT NULL REFERENCES asientocontable(id),
    asiento_reversa_id            INT           NOT NULL REFERENCES asientocontable(id),
    motivo_anulacion              VARCHAR(300),
    anulada_por                   INT REFERENCES usuario(id),
    anulada_en                    TIMESTAMPTZ,
    asiento_anulacion_id          INT REFERENCES asientocontable(id),
    asiento_anulacion_reversa_id  INT REFERENCES asientocontable(id),
    creado_por                    INT           NOT NULL REFERENCES usuario(id),
    creado_en                     TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT ck_revaluacion_mes CHECK (mes BETWEEN 1 AND 12 AND anio BETWEEN 1 AND 9999),
    CONSTRAINT ck_revaluacion_estado CHECK (estado IN ('Vigente', 'Anulada')),
    CONSTRAINT ck_revaluacion_totales CHECK
        (total_ganancia >= 0 AND total_perdida >= 0 AND total_ganancia + total_perdida > 0),
    CONSTRAINT ck_revaluacion_anulacion CHECK
        ((estado = 'Anulada') = (motivo_anulacion IS NOT NULL AND asiento_anulacion_id IS NOT NULL
                                 AND asiento_anulacion_reversa_id IS NOT NULL AND anulada_por IS NOT NULL))
);

CREATE UNIQUE INDEX ux_revaluacion_vigente ON revaluacion (moneda_id, anio, mes) WHERE estado = 'Vigente';
CREATE INDEX ix_revaluacion_tipo_cambio ON revaluacion (tipo_cambio_id);

CREATE TABLE revaluacionpartida (
    id                  SERIAL PRIMARY KEY,
    revaluacion_id      INT           NOT NULL REFERENCES revaluacion(id),
    tipo                VARCHAR(5)    NOT NULL,
    documento_cxc_id    INT REFERENCES documentocxc(id),
    documento_cxp_id    INT REFERENCES documentocxp(id),
    cuenta_bancaria_id  INT REFERENCES cuentabancaria(id),
    cuenta_id           INT           NOT NULL REFERENCES cuentacontable(id),
    saldo_original      DECIMAL(14,2) NOT NULL,
    valor_libros_gtq    DECIMAL(14,2) NOT NULL,
    valor_cierre_gtq    DECIMAL(14,2) NOT NULL,
    ajuste_gtq          DECIMAL(14,2) NOT NULL,
    CONSTRAINT ck_revaluacionpartida_tipo CHECK (tipo IN ('CxC', 'CxP', 'Banco')),
    CONSTRAINT ck_revaluacionpartida_origen CHECK
        ((tipo = 'CxC'   AND documento_cxc_id IS NOT NULL AND documento_cxp_id IS NULL     AND cuenta_bancaria_id IS NULL)
      OR (tipo = 'CxP'   AND documento_cxp_id IS NOT NULL AND documento_cxc_id IS NULL     AND cuenta_bancaria_id IS NULL)
      OR (tipo = 'Banco' AND cuenta_bancaria_id IS NOT NULL AND documento_cxc_id IS NULL   AND documento_cxp_id IS NULL)),
    CONSTRAINT ck_revaluacionpartida_ajuste CHECK (ajuste_gtq = valor_cierre_gtq - valor_libros_gtq)
);

CREATE INDEX ix_revaluacionpartida_revaluacion ON revaluacionpartida (revaluacion_id);

CREATE OR REPLACE FUNCTION fn_revaluacion_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF OLD.estado = 'Anulada'
       OR (NEW.id, NEW.moneda_id, NEW.anio, NEW.mes, NEW.fecha_cierre, NEW.tipo_cambio_id, NEW.tasa,
           NEW.total_ganancia, NEW.total_perdida, NEW.asiento_id, NEW.asiento_reversa_id, NEW.creado_por, NEW.creado_en)
          IS DISTINCT FROM
          (OLD.id, OLD.moneda_id, OLD.anio, OLD.mes, OLD.fecha_cierre, OLD.tipo_cambio_id, OLD.tasa,
           OLD.total_ganancia, OLD.total_perdida, OLD.asiento_id, OLD.asiento_reversa_id, OLD.creado_por, OLD.creado_en) THEN
        RAISE EXCEPTION 'La revaluación % solo admite el paso de Vigente a Anulada.', OLD.id
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_revaluacion_inmutable
BEFORE UPDATE ON revaluacion
FOR EACH ROW EXECUTE FUNCTION fn_revaluacion_inmutable();

CREATE TRIGGER trg_prevenir_eliminacion_revaluacion
BEFORE DELETE ON revaluacion
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();


-- 9. Completar columnas nuevas en INSERT que no las mencionan

CREATE OR REPLACE FUNCTION fn_multimoneda_completar() RETURNS TRIGGER AS $$
BEGIN
    CASE TG_TABLE_NAME
        WHEN 'documentocxc', 'documentocxp' THEN
            NEW.moneda_id := COALESCE(NEW.moneda_id, (SELECT id FROM moneda WHERE es_funcional));
            NEW.monto_total_original := COALESCE(NEW.monto_total_original, NEW.monto_total);
            NEW.tipo_cambio_aplicado := COALESCE(NEW.tipo_cambio_aplicado, 1);
        WHEN 'lineadocumentocxc', 'lineadocumentocxp' THEN
            NEW.monto_linea_original := COALESCE(NEW.monto_linea_original, NEW.monto_linea);
        WHEN 'recibopagocliente', 'pagoproveedorcabecera' THEN
            NEW.moneda_id := COALESCE(NEW.moneda_id, (SELECT id FROM moneda WHERE es_funcional));
            NEW.monto_total_gtq := COALESCE(NEW.monto_total_gtq, NEW.monto_total);
        WHEN 'aplicacionpagocliente', 'aplicacionpagoproveedor' THEN
            NEW.monto_aplicado_gtq := COALESCE(NEW.monto_aplicado_gtq, NEW.monto_aplicado);
            NEW.valor_gtq := COALESCE(NEW.valor_gtq, NEW.monto_aplicado_gtq);
        WHEN 'cuentabancaria' THEN
            NEW.moneda_id := COALESCE(NEW.moneda_id, (SELECT id FROM moneda WHERE es_funcional));
        WHEN 'movimientotesoreria' THEN
            NEW.monto_gtq := COALESCE(NEW.monto_gtq, NEW.monto);
            NEW.tipo_cambio := COALESCE(NEW.tipo_cambio, 1);
        WHEN 'retencion' THEN
            NEW.monto_retenido_original := COALESCE(NEW.monto_retenido_original, NEW.monto_retenido);
        ELSE
            NULL;
    END CASE;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON documentocxc
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON documentocxp
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON lineadocumentocxc
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON lineadocumentocxp
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON recibopagocliente
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON pagoproveedorcabecera
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON aplicacionpagocliente
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON aplicacionpagoproveedor
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON cuentabancaria
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON movimientotesoreria
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();
CREATE TRIGGER trg_00_multimoneda_completar BEFORE INSERT ON retencion
FOR EACH ROW EXECUTE FUNCTION fn_multimoneda_completar();


-- 10. Triggers del historial de tipos de cambio

CREATE OR REPLACE FUNCTION fn_historialtipocambio_valida() RETURNS TRIGGER AS $$
DECLARE
    v_funcional BOOLEAN;
    v_activa    BOOLEAN;
BEGIN
    SELECT es_funcional, activa INTO v_funcional, v_activa FROM moneda WHERE id = NEW.moneda_id;

    IF NOT FOUND OR v_funcional OR NOT v_activa THEN
        RAISE EXCEPTION 'La moneda % no admite tipo de cambio (inexistente, inactiva o funcional).', NEW.moneda_id
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_historialtipocambio_valida
BEFORE INSERT ON historialtipocambio
FOR EACH ROW EXECUTE FUNCTION fn_historialtipocambio_valida();

CREATE OR REPLACE FUNCTION fn_historialtipocambio_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF (NEW.id, NEW.moneda_id, NEW.fecha, NEW.tasa, NEW.fuente, NEW.fecha_referencia, NEW.arrastrado,
        NEW.cotizacion_venta, NEW.cotizacion_compra, NEW.referencia_usd, NEW.motivo, NEW.usuario_id, NEW.creado_en)
       IS DISTINCT FROM
       (OLD.id, OLD.moneda_id, OLD.fecha, OLD.tasa, OLD.fuente, OLD.fecha_referencia, OLD.arrastrado,
        OLD.cotizacion_venta, OLD.cotizacion_compra, OLD.referencia_usd, OLD.motivo, OLD.usuario_id, OLD.creado_en)
       OR (NEW.activa AND NOT OLD.activa) THEN
        RAISE EXCEPTION 'La tasa de cambio % solo puede darse de baja.', OLD.id
            USING ERRCODE = '55000';
    END IF;

    IF OLD.activa AND NOT NEW.activa AND (
        EXISTS (SELECT 1 FROM documentocxc WHERE tipo_cambio_id = OLD.id)
        OR EXISTS (SELECT 1 FROM documentocxp WHERE tipo_cambio_id = OLD.id)
        OR EXISTS (SELECT 1 FROM aplicacionpagocliente WHERE tipo_cambio_pago_id = OLD.id)
        OR EXISTS (SELECT 1 FROM aplicacionpagoproveedor WHERE tipo_cambio_pago_id = OLD.id)
        OR EXISTS (SELECT 1 FROM movimientotesoreria WHERE tipo_cambio_id = OLD.id)
        OR EXISTS (SELECT 1 FROM revaluacion WHERE tipo_cambio_id = OLD.id)
    ) THEN
        RAISE EXCEPTION 'La tasa de cambio % ya fue usada en documentos, cobros, pagos, movimientos o revaluaciones; no puede darse de baja.', OLD.id
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_historialtipocambio_inmutable
BEFORE UPDATE ON historialtipocambio
FOR EACH ROW EXECUTE FUNCTION fn_historialtipocambio_inmutable();

CREATE TRIGGER trg_prevenir_eliminacion_historialtipocambio
BEFORE DELETE ON historialtipocambio
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();


-- 11. Triggers de moneda en documentos

CREATE OR REPLACE FUNCTION fn_documento_moneda_valida() RETURNS TRIGGER AS $$
DECLARE
    v_funcional BOOLEAN;
    v_activa    BOOLEAN;
    v_tasa      historialtipocambio%ROWTYPE;
BEGIN
    SELECT es_funcional, activa INTO v_funcional, v_activa FROM moneda WHERE id = NEW.moneda_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'La moneda % del documento no existe.', NEW.moneda_id USING ERRCODE = '23514';
    END IF;

    IF v_funcional THEN
        IF NEW.tipo_cambio_id IS NOT NULL
           OR NEW.tipo_cambio_aplicado <> 1
           OR NEW.monto_total_original <> NEW.monto_total THEN
            RAISE EXCEPTION 'Un documento en moneda funcional usa tipo de cambio 1, sin tasa registrada y con importe original igual al total.'
                USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END IF;

    IF NOT v_activa OR NEW.tipo_cambio_id IS NULL THEN
        RAISE EXCEPTION 'Un documento en moneda extranjera exige una moneda activa y un tipo de cambio registrado.'
            USING ERRCODE = '23514';
    END IF;

    SELECT * INTO v_tasa FROM historialtipocambio WHERE id = NEW.tipo_cambio_id;

    IF NOT FOUND
       OR NOT v_tasa.activa
       OR v_tasa.moneda_id <> NEW.moneda_id
       OR v_tasa.tasa <> NEW.tipo_cambio_aplicado
       OR (NEW.documento_origen_id IS NULL AND v_tasa.fecha <> NEW.fecha) THEN
        RAISE EXCEPTION 'El tipo de cambio % no corresponde a la moneda, la tasa o la fecha del documento.', NEW.tipo_cambio_id
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_documento_moneda_valida_cxc
BEFORE INSERT ON documentocxc
FOR EACH ROW EXECUTE FUNCTION fn_documento_moneda_valida();

CREATE TRIGGER trg_documento_moneda_valida_cxp
BEFORE INSERT ON documentocxp
FOR EACH ROW EXECUTE FUNCTION fn_documento_moneda_valida();

CREATE OR REPLACE FUNCTION fn_documento_moneda_inmutable() RETURNS TRIGGER AS $$
DECLARE
    v_con_efectos BOOLEAN;
BEGIN
    IF (NEW.moneda_id, NEW.tipo_cambio_aplicado, NEW.tipo_cambio_id, NEW.monto_total_original)
       IS NOT DISTINCT FROM
       (OLD.moneda_id, OLD.tipo_cambio_aplicado, OLD.tipo_cambio_id, OLD.monto_total_original) THEN
        RETURN NEW;
    END IF;

    IF TG_TABLE_NAME = 'documentocxc' THEN
        v_con_efectos := EXISTS (SELECT 1 FROM aplicacionpagocliente WHERE documento_id = OLD.id)
                      OR EXISTS (SELECT 1 FROM documentocxc WHERE documento_origen_id = OLD.id);
    ELSE
        v_con_efectos := EXISTS (SELECT 1 FROM aplicacionpagoproveedor WHERE documento_id = OLD.id)
                      OR EXISTS (SELECT 1 FROM documentocxp WHERE documento_origen_id = OLD.id);
    END IF;

    IF v_con_efectos THEN
        RAISE EXCEPTION 'El documento % tiene aplicaciones o notas de crédito; no se pueden modificar su moneda, tipo de cambio ni importe original.', OLD.id
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_documento_moneda_inmutable_cxc
BEFORE UPDATE ON documentocxc
FOR EACH ROW EXECUTE FUNCTION fn_documento_moneda_inmutable();

CREATE TRIGGER trg_documento_moneda_inmutable_cxp
BEFORE UPDATE ON documentocxp
FOR EACH ROW EXECUTE FUNCTION fn_documento_moneda_inmutable();


-- 12. Nota de crédito: hereda moneda y tipo de cambio; límite en moneda original

CREATE OR REPLACE FUNCTION fn_nota_credito_valida_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_origen   documentocxc%ROWTYPE;
    v_cobrado  DECIMAL(14,2);
    v_notas    DECIMAL(14,2);
    v_saldo    DECIMAL(14,2);
    v_simbolo  TEXT;
BEGIN
    IF NEW.documento_origen_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT * INTO v_origen FROM documentocxc WHERE id = NEW.documento_origen_id FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El documento de origen no existe.' USING ERRCODE = '55000';
    END IF;

    IF v_origen.cliente_id <> NEW.cliente_id THEN
        RAISE EXCEPTION 'El documento de origen pertenece a otro cliente.' USING ERRCODE = '55000';
    END IF;

    IF v_origen.tipo_documento <> 'Factura' OR v_origen.estado <> 'Vigente' THEN
        RAISE EXCEPTION 'El documento de origen debe ser una factura vigente.' USING ERRCODE = '55000';
    END IF;

    IF NEW.moneda_id <> v_origen.moneda_id
       OR NEW.tipo_cambio_aplicado <> v_origen.tipo_cambio_aplicado
       OR NEW.tipo_cambio_id IS DISTINCT FROM v_origen.tipo_cambio_id THEN
        RAISE EXCEPTION 'La nota de crédito debe usar la moneda y el tipo de cambio de su factura.' USING ERRCODE = '55000';
    END IF;

    IF NEW.estado = 'Vigente' THEN
        SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_cobrado
        FROM aplicacionpagocliente WHERE documento_id = v_origen.id;

        SELECT COALESCE(SUM(monto_total_original), 0) INTO v_notas
        FROM documentocxc WHERE documento_origen_id = v_origen.id AND estado = 'Vigente';

        v_saldo := v_origen.monto_total_original - v_cobrado - v_notas;

        IF NEW.monto_total_original > v_saldo THEN
            SELECT CASE WHEN es_funcional THEN 'Q' ELSE codigo || ' ' END INTO v_simbolo
            FROM moneda WHERE id = v_origen.moneda_id;

            RAISE EXCEPTION 'El monto de la nota de crédito (%) excede el saldo pendiente del documento de origen (%).',
                v_simbolo || NEW.monto_total_original::TEXT, v_simbolo || v_saldo::TEXT
                USING ERRCODE = '55000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_nota_credito_valida_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_origen      documentocxp%ROWTYPE;
    v_pagado      DECIMAL(14,2);
    v_notas       DECIMAL(14,2);
    v_retencion   DECIMAL(14,2);
    v_saldo       DECIMAL(14,2);
    v_simbolo     TEXT;
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

    IF NEW.moneda_id <> v_origen.moneda_id
       OR NEW.tipo_cambio_aplicado <> v_origen.tipo_cambio_aplicado
       OR NEW.tipo_cambio_id IS DISTINCT FROM v_origen.tipo_cambio_id THEN
        RAISE EXCEPTION 'La nota de crédito debe usar la moneda y el tipo de cambio de su factura.' USING ERRCODE = '55000';
    END IF;

    IF NEW.estado = 'Vigente' THEN
        SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_pagado
        FROM aplicacionpagoproveedor WHERE documento_id = v_origen.id;

        SELECT COALESCE(SUM(monto_total_original), 0) INTO v_notas
        FROM documentocxp WHERE documento_origen_id = v_origen.id AND estado = 'Vigente';

        SELECT COALESCE(SUM(monto_retenido_original), 0) INTO v_retencion
        FROM retencion WHERE documento_cxp_id = v_origen.id AND concepto = 'ISR_ROS';

        v_saldo := v_origen.monto_total_original - v_pagado - v_notas - v_retencion;

        IF NEW.monto_total_original > v_saldo THEN
            SELECT CASE WHEN es_funcional THEN 'Q' ELSE codigo || ' ' END INTO v_simbolo
            FROM moneda WHERE id = v_origen.moneda_id;

            RAISE EXCEPTION 'El monto de la nota de crédito (%) excede el saldo pendiente del documento de origen (%).',
                v_simbolo || NEW.monto_total_original::TEXT, v_simbolo || v_saldo::TEXT
                USING ERRCODE = '55000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


-- 13. RN-05 en moneda original y cierre del saldo en quetzales (RN-26)

CREATE OR REPLACE FUNCTION fn_limite_pago_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_total_orig      DECIMAL(14,2);
    v_total_gtq       DECIMAL(14,2);
    v_tipo_documento  VARCHAR(20);
    v_nc_orig         DECIMAL(14,2);
    v_nc_gtq          DECIMAL(14,2);
    v_prev_orig       DECIMAL(14,2);
    v_prev_gtq        DECIMAL(14,2);
    v_saldo_gtq       DECIMAL(14,2);
BEGIN
    SELECT monto_total_original, monto_total, tipo_documento INTO v_total_orig, v_total_gtq, v_tipo_documento
    FROM documentocxc WHERE id = NEW.documento_id;

    IF v_tipo_documento = 'NotaCredito' THEN
        RAISE EXCEPTION 'RN-05: una nota de crédito no admite pagos ni cobros (documento %)', NEW.documento_id
            USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(SUM(monto_total_original), 0), COALESCE(SUM(monto_total), 0) INTO v_nc_orig, v_nc_gtq
    FROM documentocxc WHERE documento_origen_id = NEW.documento_id AND estado = 'Vigente';

    v_total_orig := v_total_orig - v_nc_orig;
    v_total_gtq := v_total_gtq - v_nc_gtq;

    SELECT COALESCE(SUM(monto_aplicado), 0), COALESCE(SUM(monto_aplicado_gtq), 0) INTO v_prev_orig, v_prev_gtq
    FROM aplicacionpagocliente
    WHERE documento_id = NEW.documento_id AND id <> COALESCE(NEW.id, -1);

    IF (v_prev_orig + NEW.monto_aplicado) > v_total_orig THEN
        RAISE EXCEPTION 'RN-05: el pago aplicado (%) excede el saldo pendiente del documento % (saldo: %)',
            NEW.monto_aplicado, NEW.documento_id, (v_total_orig - v_prev_orig);
    END IF;

    v_saldo_gtq := v_total_gtq - v_prev_gtq - NEW.monto_aplicado_gtq;

    IF v_saldo_gtq < 0
       OR ((v_total_orig - v_prev_orig - NEW.monto_aplicado = 0) <> (v_saldo_gtq = 0)) THEN
        RAISE EXCEPTION 'RN-26: el saldo en quetzales del documento % debe cerrar en cero junto con su saldo en moneda original (saldo en quetzales: %)',
            NEW.documento_id, v_saldo_gtq USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_limite_pago_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_total_orig      DECIMAL(14,2);
    v_total_gtq       DECIMAL(14,2);
    v_tipo_documento  VARCHAR(20);
    v_nc_orig         DECIMAL(14,2);
    v_nc_gtq          DECIMAL(14,2);
    v_ret_orig        DECIMAL(14,2);
    v_ret_gtq         DECIMAL(14,2);
    v_prev_orig       DECIMAL(14,2);
    v_prev_gtq        DECIMAL(14,2);
    v_saldo_gtq       DECIMAL(14,2);
BEGIN
    SELECT monto_total_original, monto_total, tipo_documento INTO v_total_orig, v_total_gtq, v_tipo_documento
    FROM documentocxp WHERE id = NEW.documento_id;

    IF v_tipo_documento = 'NotaCredito' THEN
        RAISE EXCEPTION 'RN-05: una nota de crédito no admite pagos ni cobros (documento %)', NEW.documento_id
            USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(SUM(monto_total_original), 0), COALESCE(SUM(monto_total), 0) INTO v_nc_orig, v_nc_gtq
    FROM documentocxp WHERE documento_origen_id = NEW.documento_id AND estado = 'Vigente';

    SELECT COALESCE(SUM(monto_retenido_original), 0), COALESCE(SUM(monto_retenido), 0) INTO v_ret_orig, v_ret_gtq
    FROM retencion WHERE documento_cxp_id = NEW.documento_id AND concepto = 'ISR_ROS';

    v_total_orig := v_total_orig - v_nc_orig - v_ret_orig;
    v_total_gtq := v_total_gtq - v_nc_gtq - v_ret_gtq;

    SELECT COALESCE(SUM(monto_aplicado), 0), COALESCE(SUM(monto_aplicado_gtq), 0) INTO v_prev_orig, v_prev_gtq
    FROM aplicacionpagoproveedor
    WHERE documento_id = NEW.documento_id AND id <> COALESCE(NEW.id, -1);

    IF (v_prev_orig + NEW.monto_aplicado) > v_total_orig THEN
        RAISE EXCEPTION 'RN-05: el pago aplicado (%) excede el saldo pendiente del documento % (saldo: %)',
            NEW.monto_aplicado, NEW.documento_id, (v_total_orig - v_prev_orig);
    END IF;

    v_saldo_gtq := v_total_gtq - v_prev_gtq - NEW.monto_aplicado_gtq;

    IF v_saldo_gtq < 0
       OR ((v_total_orig - v_prev_orig - NEW.monto_aplicado = 0) <> (v_saldo_gtq = 0)) THEN
        RAISE EXCEPTION 'RN-26: el saldo en quetzales del documento % debe cerrar en cero junto con su saldo en moneda original (saldo en quetzales: %)',
            NEW.documento_id, v_saldo_gtq USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


-- 14. Triggers de moneda en tesorería

CREATE OR REPLACE FUNCTION fn_cuentabancaria_moneda_inmutable() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.moneda_id IS DISTINCT FROM OLD.moneda_id
       AND EXISTS (SELECT 1 FROM movimientotesoreria WHERE cuenta_bancaria_id = OLD.id) THEN
        RAISE EXCEPTION 'La cuenta bancaria % tiene movimientos; su moneda no puede cambiarse.', OLD.id
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_cuentabancaria_moneda_inmutable
BEFORE UPDATE ON cuentabancaria
FOR EACH ROW EXECUTE FUNCTION fn_cuentabancaria_moneda_inmutable();

CREATE OR REPLACE FUNCTION fn_movimiento_moneda_valida() RETURNS TRIGGER AS $$
DECLARE
    v_cuenta_moneda_id INT;
    v_funcional        BOOLEAN;
    v_tasa             historialtipocambio%ROWTYPE;
BEGIN
    SELECT cb.moneda_id, m.es_funcional INTO v_cuenta_moneda_id, v_funcional
    FROM cuentabancaria cb JOIN moneda m ON m.id = cb.moneda_id
    WHERE cb.id = NEW.cuenta_bancaria_id;

    IF NOT FOUND THEN
        RETURN NEW;
    END IF;

    IF v_funcional THEN
        IF NEW.monto_gtq <> NEW.monto OR NEW.tipo_cambio <> 1 OR NEW.tipo_cambio_id IS NOT NULL THEN
            RAISE EXCEPTION 'Un movimiento en una cuenta en moneda funcional usa tipo de cambio 1, sin tasa registrada y monto en quetzales igual al monto.'
                USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.tipo_cambio_id IS NULL OR NEW.monto_gtq <= 0 THEN
        RAISE EXCEPTION 'Un movimiento en una cuenta en moneda extranjera exige un tipo de cambio registrado y un monto en quetzales positivo.'
            USING ERRCODE = '23514';
    END IF;

    SELECT * INTO v_tasa FROM historialtipocambio WHERE id = NEW.tipo_cambio_id;

    IF NOT FOUND
       OR NOT v_tasa.activa
       OR v_tasa.moneda_id <> v_cuenta_moneda_id
       OR v_tasa.tasa <> NEW.tipo_cambio THEN
        RAISE EXCEPTION 'El tipo de cambio % no corresponde a la moneda o a la tasa del movimiento.', NEW.tipo_cambio_id
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_movimiento_moneda_valida
BEFORE INSERT ON movimientotesoreria
FOR EACH ROW EXECUTE FUNCTION fn_movimiento_moneda_valida();


-- 15. Vistas de saldo (solo se agregan columnas al final)

CREATE OR REPLACE VIEW vw_saldodocumentocxc AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total_original
         - COALESCE((
             SELECT SUM(ap.monto_aplicado)
             FROM aplicacionpagocliente ap
             WHERE ap.documento_id = d.id
         ), 0)
         - COALESCE((
             SELECT SUM(nc.monto_total_original)
             FROM documentocxc nc
             WHERE nc.documento_origen_id = d.id AND nc.estado = 'Vigente'
         ), 0)
    END AS saldo_pendiente,
    d.moneda_id,
    d.monto_total_original,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total
         - COALESCE((
             SELECT SUM(ap.monto_aplicado_gtq)
             FROM aplicacionpagocliente ap
             WHERE ap.documento_id = d.id
         ), 0)
         - COALESCE((
             SELECT SUM(nc.monto_total)
             FROM documentocxc nc
             WHERE nc.documento_origen_id = d.id AND nc.estado = 'Vigente'
         ), 0)
    END AS saldo_pendiente_gtq
FROM documentocxc d;

CREATE OR REPLACE VIEW vw_saldodocumentocxp AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total_original
         - COALESCE((
             SELECT SUM(ap.monto_aplicado)
             FROM aplicacionpagoproveedor ap
             WHERE ap.documento_id = d.id
         ), 0)
         - COALESCE((
             SELECT SUM(nc.monto_total_original)
             FROM documentocxp nc
             WHERE nc.documento_origen_id = d.id AND nc.estado = 'Vigente'
         ), 0)
         - COALESCE((
             SELECT SUM(r.monto_retenido_original)
             FROM retencion r
             WHERE r.documento_cxp_id = d.id AND r.concepto = 'ISR_ROS'
         ), 0)
    END AS saldo_pendiente,
    d.moneda_id,
    d.monto_total_original,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total
         - COALESCE((
             SELECT SUM(ap.monto_aplicado_gtq)
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
    END AS saldo_pendiente_gtq
FROM documentocxp d;

CREATE OR REPLACE VIEW vw_saldocuentabancaria AS
SELECT
    cb.id AS cuenta_bancaria_id,
    cb.banco,
    cb.numero,
    COALESCE(SUM(CASE WHEN mt.tipo = 'Ingreso' THEN mt.monto ELSE -mt.monto END), 0) AS saldo,
    cb.moneda_id
FROM cuentabancaria cb
LEFT JOIN movimientotesoreria mt ON mt.cuenta_bancaria_id = cb.id
GROUP BY cb.id, cb.banco, cb.numero, cb.moneda_id;


-- 16. Reversa de asientos: los de revaluación solo se anulan desde la revaluación

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

    IF EXISTS (SELECT 1 FROM revaluacion WHERE p_asiento_id IN (asiento_id, asiento_reversa_id)) THEN
        RAISE EXCEPTION 'El asiento % pertenece a una revaluación de fin de mes; anule la revaluación desde su pantalla.', p_asiento_id
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


-- 17. Permisos

REVOKE EXECUTE ON FUNCTION
    fn_multimoneda_completar(),
    fn_historialtipocambio_valida(),
    fn_historialtipocambio_inmutable(),
    fn_documento_moneda_valida(),
    fn_documento_moneda_inmutable(),
    fn_cuentabancaria_moneda_inmutable(),
    fn_movimiento_moneda_valida(),
    fn_revaluacion_inmutable()
FROM PUBLIC;

GRANT SELECT, INSERT, UPDATE ON historialtipocambio, revaluacion TO delta_api;
GRANT SELECT, INSERT ON revaluacionpartida TO delta_api;
GRANT USAGE ON SEQUENCE historialtipocambio_id_seq, revaluacion_id_seq, revaluacionpartida_id_seq TO delta_api;
