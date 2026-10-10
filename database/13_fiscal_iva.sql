-- =====================================================================
-- 13_fiscal_iva.sql
-- Fiscal I1: parámetros fiscales, catálogo de impuestos con vigencia, IVA incluido
-- y separado en facturas CxC/CxP, datos del DTE y notas de crédito referenciadas.
-- =====================================================================


-- 1. Moneda funcional

ALTER TABLE moneda
    ADD COLUMN es_funcional BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN activa BOOLEAN NOT NULL DEFAULT TRUE;

INSERT INTO moneda (codigo, nombre)
SELECT 'GTQ', 'Quetzal guatemalteco'
WHERE NOT EXISTS (SELECT 1 FROM moneda WHERE codigo = 'GTQ');

UPDATE moneda SET es_funcional = TRUE WHERE codigo = 'GTQ';

ALTER TABLE moneda ADD CONSTRAINT uq_moneda_codigo UNIQUE (codigo);
CREATE UNIQUE INDEX ux_moneda_funcional ON moneda (es_funcional) WHERE es_funcional;

-- 2. Catálogo de impuestos con vigencia

CREATE TABLE impuesto (
    id              SERIAL PRIMARY KEY,
    codigo          VARCHAR(30)  NOT NULL,
    nombre          VARCHAR(100) NOT NULL,
    tipo            VARCHAR(25)  NOT NULL,
    tasa            DECIMAL(7,4) NOT NULL,
    aplica_a        VARCHAR(10)  NOT NULL DEFAULT 'AMBOS',
    genera_credito  BOOLEAN      NOT NULL DEFAULT FALSE,
    articulo_legal  VARCHAR(200) NOT NULL,
    nota            VARCHAR(300),
    vigente_desde   DATE         NOT NULL,
    vigente_hasta   DATE,
    activo          BOOLEAN      NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_impuesto_codigo_desde UNIQUE (codigo, vigente_desde),
    CONSTRAINT ck_impuesto_tipo CHECK (tipo IN ('IVA_GENERAL', 'EXENTO', 'NO_AFECTO', 'PEQUENO_CONTRIBUYENTE', 'LEGADO')),
    CONSTRAINT ck_impuesto_aplica CHECK (aplica_a IN ('VENTAS', 'COMPRAS', 'AMBOS')),
    CONSTRAINT ck_impuesto_tasa CHECK (
        (tipo = 'LEGADO' AND tasa >= 0)
        OR (tipo IN ('EXENTO', 'NO_AFECTO') AND tasa = 0)
        OR (tipo IN ('IVA_GENERAL', 'PEQUENO_CONTRIBUYENTE') AND tasa > 0 AND tasa <= 100)),
    CONSTRAINT ck_impuesto_credito CHECK (NOT genera_credito OR tipo = 'IVA_GENERAL'),
    CONSTRAINT ck_impuesto_pequeno CHECK (tipo <> 'PEQUENO_CONTRIBUYENTE' OR aplica_a = 'COMPRAS'),
    CONSTRAINT ck_impuesto_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);

-- 3. Triggers del catálogo de impuestos

CREATE OR REPLACE FUNCTION fn_impuesto_sin_traslape() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.vigente_hasta IS NOT NULL AND NEW.vigente_hasta < NEW.vigente_desde THEN
        RETURN NEW;
    END IF;

    IF EXISTS (
        SELECT 1 FROM impuesto i
        WHERE i.codigo = NEW.codigo
          AND i.id <> COALESCE(NEW.id, -1)
          AND daterange(i.vigente_desde, i.vigente_hasta, '[]') && daterange(NEW.vigente_desde, NEW.vigente_hasta, '[]')
    ) THEN
        RAISE EXCEPTION 'Ya existe una versión del impuesto % cuya vigencia se traslapa con la indicada.', NEW.codigo
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_impuesto_sin_traslape
BEFORE INSERT OR UPDATE ON impuesto
FOR EACH ROW EXECUTE FUNCTION fn_impuesto_sin_traslape();

CREATE OR REPLACE FUNCTION fn_impuesto_inmutable_si_usado() RETURNS TRIGGER AS $$
DECLARE
    v_en_uso       BOOLEAN;
    v_ultima_fecha DATE;
BEGIN
    SELECT COUNT(*) > 0, MAX(u.fecha) INTO v_en_uso, v_ultima_fecha
    FROM (
        SELECT d.fecha FROM lineadocumentocxc l JOIN documentocxc d ON d.id = l.documento_id WHERE l.impuesto_id = OLD.id
        UNION ALL
        SELECT d.fecha FROM lineadocumentocxp l JOIN documentocxp d ON d.id = l.documento_id WHERE l.impuesto_id = OLD.id
    ) u;

    IF NOT v_en_uso THEN
        RETURN NEW;
    END IF;

    IF (NEW.codigo, NEW.tipo, NEW.tasa, NEW.aplica_a, NEW.genera_credito, NEW.vigente_desde)
       IS DISTINCT FROM (OLD.codigo, OLD.tipo, OLD.tasa, OLD.aplica_a, OLD.genera_credito, OLD.vigente_desde) THEN
        RAISE EXCEPTION 'El impuesto % ya fue usado en documentos; no se pueden modificar su tipo, tasa, ámbito, crédito fiscal ni fecha de inicio. Cierre su vigencia y cree una nueva versión.', OLD.codigo
            USING ERRCODE = '55000';
    END IF;

    IF NEW.vigente_hasta IS NOT NULL AND NEW.vigente_hasta < v_ultima_fecha THEN
        RAISE EXCEPTION 'No se puede cerrar la vigencia del impuesto % antes del %: existen documentos con esa fecha.',
            OLD.codigo, to_char(v_ultima_fecha, 'YYYY-MM-DD')
            USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_impuesto_inmutable_si_usado
BEFORE UPDATE ON impuesto
FOR EACH ROW EXECUTE FUNCTION fn_impuesto_inmutable_si_usado();

CREATE TRIGGER trg_prevenir_eliminacion_impuesto
BEFORE DELETE ON impuesto
FOR EACH ROW EXECUTE FUNCTION fn_prevenir_eliminacion_fisica();

-- 4. Catálogo inicial de impuestos

INSERT INTO impuesto (codigo, nombre, tipo, tasa, aplica_a, genera_credito, articulo_legal, nota, vigente_desde) VALUES
    ('IVA_GENERAL', 'IVA general 12 %', 'IVA_GENERAL', 12, 'AMBOS', TRUE,
     'Decreto 27-92, art. 10', '[VERIFICAR con asesor] vigencia referencial', DATE '2001-01-01'),
    ('IVA_GENERAL_SIN_CREDITO', 'IVA 12 % sin derecho a crédito fiscal', 'IVA_GENERAL', 12, 'COMPRAS', FALSE,
     'Decreto 27-92, arts. 15 y 16', '[VERIFICAR con asesor] vigencia referencial', DATE '2001-01-01'),
    ('EXENTO_EXPORTACION', 'Exento: exportación de servicios', 'EXENTO', 0, 'AMBOS', FALSE,
     'Decreto 27-92, art. 7 num. 2 y art. 2 num. 4', '[VERIFICAR con asesor] vigencia referencial', DATE '1992-07-01'),
    ('NO_AFECTO', 'No afecto', 'NO_AFECTO', 0, 'AMBOS', FALSE,
     'Decreto 27-92, arts. 2 y 3', '[VERIFICAR con asesor] vigencia referencial', DATE '1992-07-01'),
    ('PEQUENO_CONTRIBUYENTE', 'Pequeño contribuyente 5 %', 'PEQUENO_CONTRIBUYENTE', 5, 'COMPRAS', FALSE,
     'Decreto 27-92, arts. 45 a 50 (art. 49: sin crédito fiscal)', '[VERIFICAR con asesor] vigencia referencial', DATE '1992-07-01');

-- 5. Configuración fiscal (fila única)

CREATE TABLE configuracionfiscal (
    id                      INT PRIMARY KEY DEFAULT 1,
    nit_empresa             VARCHAR(30),
    nombre_legal            VARCHAR(200),
    moneda_funcional_id     INT NOT NULL REFERENCES moneda(id),
    regimen_isr             VARCHAR(20) NOT NULL DEFAULT 'UTILIDADES',
    agente_retencion_iva    BOOLEAN NOT NULL DEFAULT FALSE,
    tipo_agente_iva         VARCHAR(30),
    cuenta_iva_debito_id    INT REFERENCES cuentacontable(id),
    cuenta_iva_credito_id   INT REFERENCES cuentacontable(id),
    actualizado_en          TIMESTAMPTZ NOT NULL DEFAULT now(),
    actualizado_por         INT REFERENCES usuario(id),
    CONSTRAINT ck_configuracionfiscal_unica CHECK (id = 1),
    CONSTRAINT ck_configuracionfiscal_regimen_isr CHECK (regimen_isr IN ('UTILIDADES', 'SIMPLIFICADO')),
    CONSTRAINT ck_configuracionfiscal_tipo_agente CHECK (
        tipo_agente_iva IS NULL
        OR tipo_agente_iva IN ('EXPORTADOR_HABITUAL', 'SECTOR_PUBLICO', 'TARJETA_CREDITO', 'COMBUSTIBLE', 'CONTRIBUYENTE_ESPECIAL')),
    CONSTRAINT ck_configuracionfiscal_agente CHECK (agente_retencion_iva = (tipo_agente_iva IS NOT NULL)),
    CONSTRAINT ck_configuracionfiscal_cuentas CHECK (
        cuenta_iva_debito_id IS NULL OR cuenta_iva_credito_id IS NULL OR cuenta_iva_debito_id <> cuenta_iva_credito_id)
);

CREATE OR REPLACE FUNCTION fn_configuracionfiscal_no_eliminable() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'La configuración fiscal no se elimina; solo se actualiza.' USING ERRCODE = '55000';
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_configuracionfiscal_no_eliminable
BEFORE DELETE ON configuracionfiscal
FOR EACH ROW EXECUTE FUNCTION fn_configuracionfiscal_no_eliminable();

INSERT INTO configuracionfiscal (id, moneda_funcional_id)
VALUES (1, (SELECT id FROM moneda WHERE es_funcional));

-- 6. Datos fiscales de la contraparte

ALTER TABLE contraparte
    ADD COLUMN regimen_iva VARCHAR(25) NOT NULL DEFAULT 'GENERAL',
    ADD COLUMN regimen_isr VARCHAR(20) NOT NULL DEFAULT 'UTILIDADES',
    ADD COLUMN es_residente BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN es_agente_retencion_iva BOOLEAN NOT NULL DEFAULT FALSE,
    ADD CONSTRAINT ck_contraparte_regimen_iva CHECK (regimen_iva IN ('GENERAL', 'PEQUENO_CONTRIBUYENTE', 'EXENTO')),
    ADD CONSTRAINT ck_contraparte_regimen_isr CHECK (regimen_isr IN ('UTILIDADES', 'SIMPLIFICADO'));

-- 7. Documentos y líneas: columnas nuevas (CxC)

ALTER TABLE lineadocumentocxc RENAME COLUMN porcentaje_impuesto TO tasa_aplicada;
ALTER TABLE lineadocumentocxc ALTER COLUMN tasa_aplicada TYPE DECIMAL(7,4);

ALTER TABLE lineadocumentocxc
    ADD COLUMN impuesto_id INT REFERENCES impuesto(id),
    ADD COLUMN monto_linea DECIMAL(14,2),
    ADD COLUMN monto_base DECIMAL(14,2),
    ADD COLUMN monto_iva DECIMAL(14,2),
    ADD COLUMN tipo_bien_servicio VARCHAR(10);

ALTER TABLE documentocxc
    ADD COLUMN monto_base DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN monto_iva DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN calculo_legado BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN dte_uuid UUID,
    ADD COLUMN dte_serie VARCHAR(20),
    ADD COLUMN dte_numero VARCHAR(20),
    ADD COLUMN dte_fecha_certificacion TIMESTAMPTZ,
    ADD COLUMN documento_origen_id INT REFERENCES documentocxc(id);

ALTER TABLE documentocxc ALTER COLUMN calculo_legado SET DEFAULT FALSE;

-- 8. Documentos y líneas: columnas nuevas (CxP)

ALTER TABLE lineadocumentocxp RENAME COLUMN porcentaje_impuesto TO tasa_aplicada;
ALTER TABLE lineadocumentocxp ALTER COLUMN tasa_aplicada TYPE DECIMAL(7,4);

ALTER TABLE lineadocumentocxp
    ADD COLUMN impuesto_id INT REFERENCES impuesto(id),
    ADD COLUMN monto_linea DECIMAL(14,2),
    ADD COLUMN monto_base DECIMAL(14,2),
    ADD COLUMN monto_iva DECIMAL(14,2),
    ADD COLUMN tipo_bien_servicio VARCHAR(10);

ALTER TABLE documentocxp
    ADD COLUMN monto_base DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN monto_iva DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN calculo_legado BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN dte_uuid UUID,
    ADD COLUMN dte_serie VARCHAR(20),
    ADD COLUMN dte_numero VARCHAR(20),
    ADD COLUMN dte_fecha_certificacion TIMESTAMPTZ,
    ADD COLUMN documento_origen_id INT REFERENCES documentocxp(id);

ALTER TABLE documentocxp ALTER COLUMN calculo_legado SET DEFAULT FALSE;

-- 9. Impuestos históricos para tasas previas distintas de 0 y 12

INSERT INTO impuesto (codigo, nombre, tipo, tasa, aplica_a, genera_credito, articulo_legal, vigente_desde, activo)
SELECT 'LEGADO_' || replace(d.tasa::TEXT, '.', '_'),
       'Impuesto histórico ' || d.tasa || ' %',
       'LEGADO', d.tasa, 'AMBOS', FALSE,
       'Sin referencia legal (registro anterior al catálogo fiscal)',
       DATE '1900-01-01', FALSE
FROM (
    SELECT DISTINCT COALESCE(u.tasa_aplicada, 0)::NUMERIC(7,4) AS tasa
    FROM (
        SELECT tasa_aplicada FROM lineadocumentocxc
        UNION ALL
        SELECT tasa_aplicada FROM lineadocumentocxp
    ) u
) d
WHERE d.tasa NOT IN (0, 12);

-- 10. Relleno de líneas existentes

UPDATE lineadocumentocxc l SET
    tasa_aplicada = COALESCE(l.tasa_aplicada, 0),
    impuesto_id = (
        SELECT i.id FROM impuesto i
        WHERE i.codigo = CASE COALESCE(l.tasa_aplicada, 0)
            WHEN 12 THEN 'IVA_GENERAL'
            WHEN 0 THEN 'NO_AFECTO'
            ELSE 'LEGADO_' || replace(l.tasa_aplicada::NUMERIC(7,4)::TEXT, '.', '_')
        END),
    monto_linea = ROUND(l.cantidad * l.precio_unitario * (1 + COALESCE(l.tasa_aplicada, 0) / 100), 2),
    monto_base = ROUND(l.cantidad * l.precio_unitario, 2),
    tipo_bien_servicio = 'SERVICIO';

UPDATE lineadocumentocxc SET monto_iva = monto_linea - monto_base;

UPDATE lineadocumentocxp l SET
    tasa_aplicada = COALESCE(l.tasa_aplicada, 0),
    impuesto_id = (
        SELECT i.id FROM impuesto i
        WHERE i.codigo = CASE COALESCE(l.tasa_aplicada, 0)
            WHEN 12 THEN 'IVA_GENERAL'
            WHEN 0 THEN 'NO_AFECTO'
            ELSE 'LEGADO_' || replace(l.tasa_aplicada::NUMERIC(7,4)::TEXT, '.', '_')
        END),
    monto_linea = ROUND(l.cantidad * l.precio_unitario * (1 + COALESCE(l.tasa_aplicada, 0) / 100), 2),
    monto_base = ROUND(l.cantidad * l.precio_unitario, 2),
    tipo_bien_servicio = 'SERVICIO';

UPDATE lineadocumentocxp SET monto_iva = monto_linea - monto_base;

-- 11. Relleno de cabeceras existentes

UPDATE documentocxc d SET monto_iva = s.iva
FROM (SELECT documento_id, SUM(monto_iva) AS iva FROM lineadocumentocxc GROUP BY documento_id) s
WHERE s.documento_id = d.id;

UPDATE documentocxc SET monto_base = monto_total - monto_iva;

UPDATE documentocxp d SET monto_iva = s.iva
FROM (SELECT documento_id, SUM(monto_iva) AS iva FROM lineadocumentocxp GROUP BY documento_id) s
WHERE s.documento_id = d.id;

UPDATE documentocxp SET monto_base = monto_total - monto_iva;

-- 12. Restricciones e índices (CxC)

ALTER TABLE lineadocumentocxc
    ALTER COLUMN tasa_aplicada SET NOT NULL,
    ALTER COLUMN impuesto_id SET NOT NULL,
    ALTER COLUMN monto_linea SET NOT NULL,
    ALTER COLUMN monto_base SET NOT NULL,
    ALTER COLUMN monto_iva SET NOT NULL,
    ALTER COLUMN tipo_bien_servicio SET NOT NULL,
    ADD CONSTRAINT ck_lineadocumentocxc_montos CHECK (monto_linea = monto_base + monto_iva),
    ADD CONSTRAINT ck_lineadocumentocxc_tipobs CHECK (tipo_bien_servicio IN ('BIEN', 'SERVICIO'));

ALTER TABLE documentocxc
    ADD CONSTRAINT ck_documentocxc_montos CHECK (monto_total = monto_base + monto_iva),
    ADD CONSTRAINT ck_documentocxc_dte CHECK (
        calculo_legado
        OR (dte_uuid IS NOT NULL AND dte_serie IS NOT NULL AND dte_numero IS NOT NULL AND dte_fecha_certificacion IS NOT NULL)),
    ADD CONSTRAINT ck_documentocxc_origen CHECK (documento_origen_id IS NULL OR tipo_documento = 'NotaCredito'),
    ADD CONSTRAINT ck_documentocxc_nc_origen CHECK (tipo_documento <> 'NotaCredito' OR documento_origen_id IS NOT NULL OR calculo_legado);

CREATE UNIQUE INDEX ux_documentocxc_dte_uuid ON documentocxc (dte_uuid) WHERE dte_uuid IS NOT NULL;
CREATE UNIQUE INDEX ux_documentocxc_dte_serie_numero ON documentocxc (dte_serie, dte_numero) WHERE dte_serie IS NOT NULL;
CREATE INDEX ix_documentocxc_origen ON documentocxc (documento_origen_id) WHERE documento_origen_id IS NOT NULL;
CREATE INDEX ix_lineadocumentocxc_impuesto ON lineadocumentocxc (impuesto_id);

-- 13. Restricciones e índices (CxP)

ALTER TABLE lineadocumentocxp
    ALTER COLUMN tasa_aplicada SET NOT NULL,
    ALTER COLUMN impuesto_id SET NOT NULL,
    ALTER COLUMN monto_linea SET NOT NULL,
    ALTER COLUMN monto_base SET NOT NULL,
    ALTER COLUMN monto_iva SET NOT NULL,
    ALTER COLUMN tipo_bien_servicio SET NOT NULL,
    ADD CONSTRAINT ck_lineadocumentocxp_montos CHECK (monto_linea = monto_base + monto_iva),
    ADD CONSTRAINT ck_lineadocumentocxp_tipobs CHECK (tipo_bien_servicio IN ('BIEN', 'SERVICIO'));

ALTER TABLE documentocxp
    ADD CONSTRAINT ck_documentocxp_montos CHECK (monto_total = monto_base + monto_iva),
    ADD CONSTRAINT ck_documentocxp_dte_completo CHECK (
        (dte_uuid IS NULL) = (dte_serie IS NULL) AND (dte_serie IS NULL) = (dte_numero IS NULL)),
    ADD CONSTRAINT ck_documentocxp_origen CHECK (documento_origen_id IS NULL OR tipo_documento = 'NotaCredito'),
    ADD CONSTRAINT ck_documentocxp_nc_origen CHECK (tipo_documento <> 'NotaCredito' OR documento_origen_id IS NOT NULL OR calculo_legado);

CREATE UNIQUE INDEX ux_documentocxp_dte_uuid ON documentocxp (dte_uuid) WHERE dte_uuid IS NOT NULL;
CREATE UNIQUE INDEX ux_documentocxp_proveedor_dte_serie_numero ON documentocxp (proveedor_id, dte_serie, dte_numero) WHERE dte_serie IS NOT NULL;
CREATE INDEX ix_documentocxp_origen ON documentocxp (documento_origen_id) WHERE documento_origen_id IS NOT NULL;
CREATE INDEX ix_lineadocumentocxp_impuesto ON lineadocumentocxp (impuesto_id);

-- 14. Validación de la línea contra el catálogo (documentos no legados)

CREATE OR REPLACE FUNCTION fn_linea_impuesto_valido() RETURNS TRIGGER AS $$
DECLARE
    v_ambito  VARCHAR(10);
    v_legado  BOOLEAN;
    v_fecha   DATE;
    v_impuesto impuesto%ROWTYPE;
BEGIN
    IF TG_OP = 'UPDATE'
       AND NEW.documento_id = OLD.documento_id
       AND NEW.impuesto_id = OLD.impuesto_id
       AND NEW.tasa_aplicada = OLD.tasa_aplicada THEN
        RETURN NEW;
    END IF;

    IF TG_TABLE_NAME = 'lineadocumentocxc' THEN
        v_ambito := 'VENTAS';
        SELECT calculo_legado, fecha INTO v_legado, v_fecha FROM documentocxc WHERE id = NEW.documento_id;
    ELSE
        v_ambito := 'COMPRAS';
        SELECT calculo_legado, fecha INTO v_legado, v_fecha FROM documentocxp WHERE id = NEW.documento_id;
    END IF;

    IF NOT FOUND OR v_legado THEN
        RETURN NEW;
    END IF;

    SELECT * INTO v_impuesto FROM impuesto WHERE id = NEW.impuesto_id;

    IF NOT FOUND
       OR NOT v_impuesto.activo
       OR v_impuesto.tipo = 'LEGADO'
       OR v_fecha < v_impuesto.vigente_desde
       OR v_fecha > COALESCE(v_impuesto.vigente_hasta, DATE 'infinity')
       OR v_impuesto.aplica_a NOT IN ('AMBOS', v_ambito)
       OR NEW.tasa_aplicada <> v_impuesto.tasa THEN
        RAISE EXCEPTION 'El impuesto % no es aplicable a la línea del documento % (inactivo, fuera de vigencia, de otro ámbito o con tasa distinta).',
            NEW.impuesto_id, NEW.documento_id
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_linea_impuesto_valido_cxc
BEFORE INSERT OR UPDATE ON lineadocumentocxc
FOR EACH ROW EXECUTE FUNCTION fn_linea_impuesto_valido();

CREATE TRIGGER trg_linea_impuesto_valido_cxp
BEFORE INSERT OR UPDATE ON lineadocumentocxp
FOR EACH ROW EXECUTE FUNCTION fn_linea_impuesto_valido();

-- 15. Nota de crédito referenciada

CREATE OR REPLACE FUNCTION fn_nota_credito_valida_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_origen   documentocxc%ROWTYPE;
    v_cobrado  DECIMAL(14,2);
    v_notas    DECIMAL(14,2);
    v_saldo    DECIMAL(14,2);
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

    IF NEW.estado = 'Vigente' THEN
        SELECT COALESCE(SUM(monto_aplicado), 0) INTO v_cobrado
        FROM aplicacionpagocliente WHERE documento_id = v_origen.id;

        SELECT COALESCE(SUM(monto_total), 0) INTO v_notas
        FROM documentocxc WHERE documento_origen_id = v_origen.id AND estado = 'Vigente';

        v_saldo := v_origen.monto_total - v_cobrado - v_notas;

        IF NEW.monto_total > v_saldo THEN
            RAISE EXCEPTION 'El monto de la nota de crédito (Q%) excede el saldo pendiente del documento de origen (Q%).',
                NEW.monto_total, v_saldo
                USING ERRCODE = '55000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_nota_credito_valida_cxc
BEFORE INSERT ON documentocxc
FOR EACH ROW EXECUTE FUNCTION fn_nota_credito_valida_cxc();

CREATE OR REPLACE FUNCTION fn_nota_credito_valida_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_origen   documentocxp%ROWTYPE;
    v_pagado   DECIMAL(14,2);
    v_notas    DECIMAL(14,2);
    v_saldo    DECIMAL(14,2);
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

        v_saldo := v_origen.monto_total - v_pagado - v_notas;

        IF NEW.monto_total > v_saldo THEN
            RAISE EXCEPTION 'El monto de la nota de crédito (Q%) excede el saldo pendiente del documento de origen (Q%).',
                NEW.monto_total, v_saldo
                USING ERRCODE = '55000';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_nota_credito_valida_cxp
BEFORE INSERT ON documentocxp
FOR EACH ROW EXECUTE FUNCTION fn_nota_credito_valida_cxp();

-- 16. RN-05 con notas de crédito: el límite descuenta las notas vigentes del origen

CREATE OR REPLACE FUNCTION fn_limite_pago_cxc() RETURNS TRIGGER AS $$
DECLARE
    v_monto_total     DECIMAL(14,2);
    v_tipo_documento  VARCHAR(20);
    v_notas_credito   DECIMAL(14,2);
    v_ya_aplicado     DECIMAL(14,2);
BEGIN
    SELECT monto_total, tipo_documento INTO v_monto_total, v_tipo_documento
    FROM documentocxc WHERE id = NEW.documento_id;

    IF v_tipo_documento = 'NotaCredito' THEN
        RAISE EXCEPTION 'RN-05: una nota de crédito no admite pagos ni cobros (documento %)', NEW.documento_id
            USING ERRCODE = '55000';
    END IF;

    SELECT COALESCE(SUM(monto_total), 0) INTO v_notas_credito
    FROM documentocxc WHERE documento_origen_id = NEW.documento_id AND estado = 'Vigente';
    v_monto_total := v_monto_total - v_notas_credito;

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

CREATE OR REPLACE FUNCTION fn_limite_pago_cxp() RETURNS TRIGGER AS $$
DECLARE
    v_monto_total     DECIMAL(14,2);
    v_tipo_documento  VARCHAR(20);
    v_notas_credito   DECIMAL(14,2);
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
    v_monto_total := v_monto_total - v_notas_credito;

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

-- 17. Vistas de saldo: restan las notas de crédito vigentes; la nota de crédito no tiene saldo propio

CREATE OR REPLACE VIEW vw_saldodocumentocxc AS
SELECT
    d.id AS documento_id,
    d.monto_total,
    CASE WHEN d.tipo_documento = 'NotaCredito' THEN 0
    ELSE d.monto_total
         - COALESCE((
             SELECT SUM(ap.monto_aplicado)
             FROM aplicacionpagocliente ap
             WHERE ap.documento_id = d.id
         ), 0)
         - COALESCE((
             SELECT SUM(nc.monto_total)
             FROM documentocxc nc
             WHERE nc.documento_origen_id = d.id AND nc.estado = 'Vigente'
         ), 0)
    END AS saldo_pendiente
FROM documentocxc d;

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
    END AS saldo_pendiente
FROM documentocxp d;
