-- 17_retencion_no_residentes.sql
-- ISR a no residentes (Decreto 10-2012, art. 104): catalogo de tipos de renta, retencion al pago (RN-29 a RN-31)
-- y reporte. No se edita un script ya aplicado.


-- 1. Parametros: concepto nuevo y semilla del art. 104

ALTER TABLE parametroretencion DROP CONSTRAINT IF EXISTS ck_parametroretencion_concepto;
ALTER TABLE parametroretencion
    ADD CONSTRAINT ck_parametroretencion_concepto CHECK
        (concepto IN ('ISR_PROVEEDOR_ROS', 'IVA_RETENIDO_CLIENTE', 'PLAZO_CONSTANCIA', 'ISR_NO_RESIDENTE'));

-- trg_parametroretencion_sin_traslape corre antes de ON CONFLICT: el NOT EXISTS evita que reaplicar falle.
INSERT INTO parametroretencion
    (codigo, concepto, nombre, tasa_base, umbral_minimo, umbral_sobre, articulo_legal, nota, vigente_desde)
SELECT v.codigo, 'ISR_NO_RESIDENTE', v.nombre, v.tasa, 0, 'BASE', 'Decreto 10-2012, art. 104',
       'Referencial, validar con asesor', DATE '2013-01-01'
FROM (VALUES
    ('NR_TRANSPORTE_INTERNACIONAL',        'Transporte internacional',                                   5::numeric),
    ('NR_SEGUROS_REASEGUROS',              'Seguros, reaseguros, reafianzamientos y primas de fianzas',  5),
    ('NR_TELECOM_DATOS_INTERNACIONAL',     'Telecomunicaciones y transmision de datos internacional',    5),
    ('NR_ENERGIA_EXTERIOR',                'Suministro de energia desde el exterior',                    5),
    ('NR_REMESAS_EP_CASA_MATRIZ',          'Remesas de establecimiento permanente a casa matriz',        5),
    ('NR_NOTICIAS_PELICULAS_GRABACIONES',  'Noticias, peliculas, grabaciones y transmisiones',           3),
    ('NR_INTERESES',                       'Intereses',                                                  10),
    ('NR_SUELDOS_DIETAS_COMISIONES',       'Sueldos, dietas, comisiones y remuneraciones',               15),
    ('NR_ARTISTAS_DEPORTISTAS',            'Artistas y deportistas',                                     15),
    ('NR_REGALIAS',                        'Regalias y uso de propiedad intelectual',                    15),
    ('NR_HONORARIOS_ASESORIA',             'Honorarios, asesoria y servicios tecnicos',                  15),
    ('NR_OTRAS_RENTAS',                    'Otras rentas de fuente guatemalteca',                        25)
) AS v(codigo, nombre, tasa)
WHERE NOT EXISTS (SELECT 1 FROM parametroretencion p WHERE p.codigo = v.codigo)
ON CONFLICT (codigo, vigente_desde) DO NOTHING;


-- 2. Inmutabilidad de parametros: para NORES el hecho generador es la fecha del pago

CREATE OR REPLACE FUNCTION fn_parametroretencion_inmutable_si_usado() RETURNS TRIGGER AS $$
DECLARE
    v_en_uso       BOOLEAN;
    v_ultima_fecha DATE;
BEGIN
    SELECT COUNT(*) > 0,
           MAX(CASE WHEN r.concepto = 'ISR_NORES' THEN r.fecha_operacion ELSE r.fecha_documento END)
      INTO v_en_uso, v_ultima_fecha
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


-- 3. Contraparte: tipo de renta del no residente (sin FK; la API valida contra el catalogo vigente)

ALTER TABLE contraparte ADD COLUMN IF NOT EXISTS tipo_renta_nr_codigo VARCHAR(40);


-- 4. Configuracion fiscal: cuenta del ISR retenido a no residentes (y alta de la cuenta si hay catalogo)

ALTER TABLE configuracionfiscal
    ADD COLUMN IF NOT EXISTS cuenta_isr_nr_retenido_pagar_id INT REFERENCES cuentacontable(id);

ALTER TABLE configuracionfiscal DROP CONSTRAINT IF EXISTS ck_configuracionfiscal_cuentas_retencion;
ALTER TABLE configuracionfiscal
    ADD CONSTRAINT ck_configuracionfiscal_cuentas_retencion CHECK (
        (cuenta_isr_retenido_pagar_id IS NULL OR cuenta_isr_retenido_pagar_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_iva_retenido_clientes_id, -1)))
        AND (cuenta_iva_retenido_clientes_id IS NULL OR cuenta_iva_retenido_clientes_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1)))
        AND (cuenta_isr_nr_retenido_pagar_id IS NULL OR cuenta_isr_nr_retenido_pagar_id NOT IN
            (COALESCE(cuenta_iva_debito_id, -1), COALESCE(cuenta_iva_credito_id, -1),
             COALESCE(cuenta_isr_retenido_pagar_id, -1), COALESCE(cuenta_iva_retenido_clientes_id, -1),
             COALESCE(cuenta_dif_ganancia_realizada_id, -1), COALESCE(cuenta_dif_perdida_realizada_id, -1),
             COALESCE(cuenta_reval_ganancia_id, -1), COALESCE(cuenta_reval_perdida_id, -1))));

-- Las migraciones no siembran plan de cuentas: solo se crea y asigna si el catalogo ya tiene 2.1.
INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT '2.1.03', 'ISR retenido a no residentes por pagar', p.tipo, p.naturaleza, p.id
FROM cuentacontable p
WHERE p.codigo = '2.1' AND p.tipo = 'Pasivo'
ON CONFLICT (codigo) DO NOTHING;

UPDATE configuracionfiscal
SET cuenta_isr_nr_retenido_pagar_id = (
    SELECT id FROM cuentacontable
    WHERE codigo = '2.1.03' AND nombre = 'ISR retenido a no residentes por pagar' AND tipo = 'Pasivo' AND activa)
WHERE id = 1 AND cuenta_isr_nr_retenido_pagar_id IS NULL;


-- 5. Retencion: ISR a no residentes (retencion al pago, por aplicacion)

ALTER TABLE retencion
    ADD COLUMN IF NOT EXISTS aplicacion_pago_proveedor_id INT REFERENCES aplicacionpagoproveedor(id),
    ADD COLUMN IF NOT EXISTS moneda_id                    INT REFERENCES moneda(id),
    ADD COLUMN IF NOT EXISTS monto_origen                 DECIMAL(14,2),
    ADD COLUMN IF NOT EXISTS tipo_cambio                  DECIMAL(12,6),
    ADD COLUMN IF NOT EXISTS tipo_cambio_fecha            DATE,
    ADD COLUMN IF NOT EXISTS tipo_renta_codigo            VARCHAR(40);

ALTER TABLE retencion DROP CONSTRAINT IF EXISTS ck_retencion_concepto;
ALTER TABLE retencion DROP CONSTRAINT IF EXISTS ck_retencion_origen;
ALTER TABLE retencion DROP CONSTRAINT IF EXISTS ck_retencion_nores_valores;
ALTER TABLE retencion
    ADD CONSTRAINT ck_retencion_concepto CHECK (concepto IN ('ISR_ROS', 'IVA_CLIENTE', 'ISR_NORES')),
    ADD CONSTRAINT ck_retencion_origen CHECK (
        (concepto = 'ISR_ROS'
            AND documento_cxp_id IS NOT NULL
            AND documento_cxc_id IS NULL AND aplicacion_pago_cliente_id IS NULL
            AND aplicacion_pago_proveedor_id IS NULL)
        OR (concepto = 'IVA_CLIENTE'
            AND documento_cxp_id IS NULL AND aplicacion_pago_proveedor_id IS NULL
            AND documento_cxc_id IS NOT NULL AND aplicacion_pago_cliente_id IS NOT NULL)
        OR (concepto = 'ISR_NORES'
            AND documento_cxp_id IS NOT NULL AND aplicacion_pago_proveedor_id IS NOT NULL
            AND documento_cxc_id IS NULL AND aplicacion_pago_cliente_id IS NULL
            AND moneda_id IS NOT NULL AND monto_origen IS NOT NULL
            AND tipo_cambio IS NOT NULL AND tipo_cambio_fecha IS NOT NULL
            AND tipo_renta_codigo IS NOT NULL)),
    ADD CONSTRAINT ck_retencion_nores_valores CHECK (
        (monto_origen IS NULL OR monto_origen > 0) AND (tipo_cambio IS NULL OR tipo_cambio > 0));

CREATE UNIQUE INDEX IF NOT EXISTS ux_retencion_nores_aplicacion
    ON retencion (aplicacion_pago_proveedor_id) WHERE concepto = 'ISR_NORES';


-- 6. Vista del reporte: columnas existentes en el mismo orden, nuevas al final

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
    0::NUMERIC(14,2) AS enterado_gtq,
    CASE WHEN r.concepto = 'IVA_CLIENTE' THEN 0 ELSE r.monto_retenido END::NUMERIC(14,2) AS saldo_por_enterar_gtq
FROM retencion r
JOIN contraparte c ON c.id = r.contraparte_id
JOIN asientocontable a ON a.id = r.asiento_id
JOIN parametroretencion pr ON pr.id = r.parametro_id
LEFT JOIN moneda mo ON mo.id = r.moneda_id
LEFT JOIN documentocxp dp ON dp.id = r.documento_cxp_id
LEFT JOIN documentocxc dc ON dc.id = r.documento_cxc_id
LEFT JOIN LATERAL (
    SELECT cr.id, cr.numero, cr.fecha, cr.formulario, cr.fecha_pago_formulario, cr.periodo_declarado, cr.estado
    FROM constanciaretencion cr
    WHERE cr.retencion_id = r.id
    ORDER BY (cr.estado = 'EMITIDA') DESC, cr.id DESC
    LIMIT 1
) k ON TRUE;
