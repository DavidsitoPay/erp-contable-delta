-- Datos de demostración fiscal y multimoneda aplicados manualmente a production (no los ejecuta run_migrations.sh).
-- Estructura: HELPERS (se antepone a cada bloque) + BLOQUE 1..4. Cada bloque es una sola transacción y tiene su guardia.
-- Los helpers son funciones pg_temp: viven solo en la sesión/transacción que los crea.

-- =====================================================================
-- HELPERS (anteponer a cada bloque que los use: 3 y 4)
-- =====================================================================

CREATE FUNCTION pg_temp.d_cta(p_codigo TEXT) RETURNS INT LANGUAGE sql AS $$
    SELECT id FROM cuentacontable WHERE codigo = p_codigo;
$$;

-- p_lineas: [[cuenta_id, debito, credito, centro_codigo|null], ...]. Valida partida doble y periodo Abierto.
CREATE FUNCTION pg_temp.d_asiento(p_numero TEXT, p_fecha DATE, p_usuario INT, p_tc NUMERIC, p_lineas JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_deb     NUMERIC;
    v_cred    NUMERIC;
    v_periodo INT;
    v_id      INT;
BEGIN
    SELECT SUM((e->>1)::NUMERIC), SUM((e->>2)::NUMERIC) INTO v_deb, v_cred FROM jsonb_array_elements(p_lineas) e;
    IF v_deb IS DISTINCT FROM v_cred OR COALESCE(v_deb, 0) <= 0 THEN
        RAISE EXCEPTION 'El asiento % no cuadra: débitos % créditos %', p_numero, v_deb, v_cred;
    END IF;
    SELECT id INTO v_periodo FROM periodocontable WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin AND estado = 'Abierto';
    IF v_periodo IS NULL THEN
        RAISE EXCEPTION 'No hay periodo Abierto para %', p_fecha;
    END IF;
    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id, tipo_cambio_aplicado)
    VALUES (p_numero, p_fecha, v_periodo, v_deb, 'Confirmado', p_usuario, p_tc)
    RETURNING id INTO v_id;
    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_id, (e->>0)::INT, (SELECT id FROM centrocosto WHERE codigo = e->>3), (e->>1)::NUMERIC, (e->>2)::NUMERIC
    FROM jsonb_array_elements(p_lineas) e
    WHERE (e->>1)::NUMERIC > 0 OR (e->>2)::NUMERIC > 0;
    RETURN v_id;
END $$;

-- Factura de venta (CxC) o compra (CxP) de una línea. p_monto = total con IVA incluido en la moneda del documento.
-- p_isr = TRUE aplica la retención de ISR a proveedor residente según parametroretencion vigente (escala, umbral) y la asienta en la factura.
CREATE FUNCTION pg_temp.d_factura(p_sigla TEXT, p_numero TEXT, p_tercero TEXT, p_fecha DATE, p_venc DATE, p_email TEXT,
                                  p_moneda TEXT, p_impuesto TEXT, p_cuenta_codigo TEXT, p_desc TEXT, p_monto NUMERIC,
                                  p_isr BOOLEAN DEFAULT FALSE)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_cxc     BOOLEAN := (p_sigla = 'CxC');
    v_tipo    TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Cliente' ELSE 'Proveedor' END;
    v_usuario INT;
    v_tercero INT;
    v_mon     moneda%ROWTYPE;
    v_imp     impuesto%ROWTYPE;
    v_cfg     configuracionfiscal%ROWTYPE;
    v_tc      historialtipocambio%ROWTYPE;
    v_par     parametroretencion%ROWTYPE;
    v_tasa    NUMERIC := 1;
    v_total   NUMERIC;
    v_iva     NUMERIC := 0;
    v_base    NUMERIC;
    v_ret     NUMERIC := 0;
    v_lineas  JSONB;
    v_asiento INT;
    v_doc     INT;
    v_uuid    UUID := md5(random()::TEXT || clock_timestamp()::TEXT)::UUID;
    v_dte     TEXT := regexp_replace(p_numero, '\D', '', 'g');
    v_cert    TIMESTAMPTZ := (p_fecha + TIME '10:00') AT TIME ZONE 'America/Guatemala';
    v_centro  TEXT := CASE WHEN p_sigla = 'CxC' THEN 'VTA' ELSE 'ADM' END;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT * INTO STRICT v_mon FROM moneda WHERE codigo = p_moneda;
    SELECT * INTO STRICT v_imp FROM impuesto WHERE codigo = p_impuesto;
    SELECT * INTO STRICT v_cfg FROM configuracionfiscal WHERE id = 1;
    SELECT id INTO STRICT v_tercero FROM contraparte WHERE tipo = v_tipo AND nombre = p_tercero;

    IF NOT v_mon.es_funcional THEN
        SELECT * INTO v_tc FROM historialtipocambio WHERE moneda_id = v_mon.id AND fecha = p_fecha AND activa;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'No hay tipo de cambio de % para %', p_moneda, p_fecha;
        END IF;
        v_tasa := v_tc.tasa;
    END IF;

    v_total := ROUND(p_monto * v_tasa, 2);
    IF v_imp.tipo = 'IVA_GENERAL' THEN
        v_iva := ROUND(v_total * v_imp.tasa / (100 + v_imp.tasa), 2);
    END IF;
    v_base := v_total - v_iva;

    IF p_isr THEN
        SELECT * INTO STRICT v_par FROM parametroretencion
        WHERE codigo = 'ISR_PROVEEDOR_ROS' AND activo AND vigente_desde <= p_fecha AND (vigente_hasta IS NULL OR vigente_hasta >= p_fecha);
        IF v_base < v_par.umbral_minimo THEN
            RAISE EXCEPTION 'La base % no alcanza el umbral de retención', v_base;
        END IF;
        v_ret := ROUND(LEAST(v_base, v_par.tramo_limite) * v_par.tasa_base / 100
                       + GREATEST(v_base - v_par.tramo_limite, 0) * v_par.tasa_excedente / 100, 2);
    END IF;

    IF v_cxc THEN
        v_lineas := jsonb_build_array(
            jsonb_build_array(pg_temp.d_cta('1.1.03'), v_total, 0, NULL),
            jsonb_build_array(pg_temp.d_cta(p_cuenta_codigo), 0, v_base, v_centro),
            jsonb_build_array(v_cfg.cuenta_iva_debito_id, 0, v_iva, NULL));
    ELSE
        v_lineas := jsonb_build_array(
            jsonb_build_array(pg_temp.d_cta('2.1.01'), 0, v_total - v_ret, NULL),
            jsonb_build_array(pg_temp.d_cta(p_cuenta_codigo), v_base, 0, v_centro),
            jsonb_build_array(v_cfg.cuenta_iva_credito_id, v_iva, 0, NULL),
            jsonb_build_array(v_cfg.cuenta_isr_retenido_pagar_id, 0, v_ret, NULL));
    END IF;
    v_asiento := pg_temp.d_asiento(UPPER(p_sigla) || '-' || p_numero, p_fecha, v_usuario, v_tasa, v_lineas);

    IF v_cxc THEN
        INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, monto_iva,
                                  tipo_cambio_aplicado, estado, asiento_id, dte_uuid, dte_serie, dte_numero, dte_fecha_certificacion,
                                  moneda_id, monto_total_original, tipo_cambio_id, tipo_cambio_fuente)
        VALUES (p_numero, 'Factura', v_tercero, p_fecha, p_venc, v_total, v_base, v_iva, v_tasa, 'Vigente', v_asiento,
                v_uuid, 'DEMO', v_dte, v_cert, v_mon.id, p_monto, v_tc.id, v_tc.fuente)
        RETURNING id INTO v_doc;
        INSERT INTO lineadocumentocxc (documento_id, descripcion, cantidad, precio_unitario, tasa_aplicada, impuesto_id, monto_linea,
                                       monto_base, monto_iva, tipo_bien_servicio, centro_costo_id, cuenta_contable_id, monto_linea_original)
        VALUES (v_doc, p_desc, 1, p_monto, v_imp.tasa, v_imp.id, v_total, v_base, v_iva, 'SERVICIO',
                (SELECT id FROM centrocosto WHERE codigo = v_centro), pg_temp.d_cta(p_cuenta_codigo), p_monto);
    ELSE
        INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, monto_iva,
                                  tipo_cambio_aplicado, estado, asiento_id, dte_uuid, dte_serie, dte_numero, dte_fecha_certificacion,
                                  moneda_id, monto_total_original, tipo_cambio_id, tipo_cambio_fuente)
        VALUES (p_numero, 'Factura', v_tercero, p_fecha, p_venc, v_total, v_base, v_iva, v_tasa, 'Vigente', v_asiento,
                v_uuid, 'DEMO', v_dte, v_cert, v_mon.id, p_monto, v_tc.id, v_tc.fuente)
        RETURNING id INTO v_doc;
        INSERT INTO lineadocumentocxp (documento_id, descripcion, cantidad, precio_unitario, tasa_aplicada, impuesto_id, monto_linea,
                                       monto_base, monto_iva, tipo_bien_servicio, centro_costo_id, cuenta_contable_id, monto_linea_original)
        VALUES (v_doc, p_desc, 1, p_monto, v_imp.tasa, v_imp.id, v_total, v_base, v_iva, 'SERVICIO',
                (SELECT id FROM centrocosto WHERE codigo = v_centro), pg_temp.d_cta(p_cuenta_codigo), p_monto);
        IF v_ret > 0 THEN
            INSERT INTO retencion (concepto, contraparte_id, documento_cxp_id, parametro_id, fecha_documento, fecha_operacion, cuenta_id,
                                   base, tasa, monto_calculado, monto_retenido, asiento_id, registrado_por)
            VALUES ('ISR_ROS', v_tercero, v_doc, v_par.id, p_fecha, p_fecha, v_cfg.cuenta_isr_retenido_pagar_id,
                    v_base, ROUND(v_ret / v_base * 100, 4), v_ret, v_ret, v_asiento, v_usuario);
        END IF;
    END IF;

    CALL sp_registrar_auditoria(v_usuario, 'registrar_factura_' || LOWER(p_sigla), 'documento' || LOWER(p_sigla),
        format('Factura %s (id %s) registrada para %s %s, monto %s %s', p_numero, v_doc, LOWER(v_tipo), p_tercero, p_monto, p_moneda));
    RETURN v_doc;
END $$;

-- Nota de crédito de CxP sobre una factura (hereda moneda y tipo de cambio de la factura). Solo documentos sin IVA.
CREATE FUNCTION pg_temp.d_nc_cxp(p_numero TEXT, p_origen TEXT, p_proveedor TEXT, p_fecha DATE, p_email TEXT, p_monto NUMERIC, p_cuenta_codigo TEXT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_o       documentocxp%ROWTYPE;
    v_total   NUMERIC;
    v_asiento INT;
    v_doc     INT;
    v_uuid    UUID := md5(random()::TEXT || clock_timestamp()::TEXT)::UUID;
    v_cert    TIMESTAMPTZ := (p_fecha + TIME '10:00') AT TIME ZONE 'America/Guatemala';
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT d.* INTO STRICT v_o FROM documentocxp d JOIN contraparte c ON c.id = d.proveedor_id
    WHERE d.numero = p_origen AND c.nombre = p_proveedor;
    IF v_o.monto_iva <> 0 THEN
        RAISE EXCEPTION 'd_nc_cxp solo admite facturas sin IVA';
    END IF;
    v_total := ROUND(p_monto * v_o.tipo_cambio_aplicado, 2);
    v_asiento := pg_temp.d_asiento('CXP-' || p_numero, p_fecha, v_usuario, v_o.tipo_cambio_aplicado, jsonb_build_array(
        jsonb_build_array(pg_temp.d_cta('2.1.01'), v_total, 0, NULL),
        jsonb_build_array(pg_temp.d_cta(p_cuenta_codigo), 0, v_total, 'ADM')));
    INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, monto_iva,
                              tipo_cambio_aplicado, estado, asiento_id, dte_uuid, dte_serie, dte_numero, dte_fecha_certificacion,
                              documento_origen_id, moneda_id, monto_total_original, tipo_cambio_id, tipo_cambio_fuente)
    VALUES (p_numero, 'NotaCredito', v_o.proveedor_id, p_fecha, p_fecha, v_total, v_total, 0, v_o.tipo_cambio_aplicado, 'Vigente', v_asiento,
            v_uuid, 'DEMO', regexp_replace(p_numero, '\D', '', 'g'), v_cert, v_o.id, v_o.moneda_id, p_monto, v_o.tipo_cambio_id, v_o.tipo_cambio_fuente)
    RETURNING id INTO v_doc;
    INSERT INTO lineadocumentocxp (documento_id, descripcion, cantidad, precio_unitario, tasa_aplicada, impuesto_id, monto_linea,
                                   monto_base, monto_iva, tipo_bien_servicio, centro_costo_id, cuenta_contable_id, monto_linea_original)
    SELECT v_doc, 'Nota de crédito sobre ' || p_origen, 1, p_monto, l.tasa_aplicada, l.impuesto_id, v_total, v_total, 0, l.tipo_bien_servicio,
           l.centro_costo_id, l.cuenta_contable_id, p_monto
    FROM lineadocumentocxp l WHERE l.documento_id = v_o.id ORDER BY l.id LIMIT 1;
    CALL sp_registrar_auditoria(v_usuario, 'registrar_nota_credito_cxp', 'documentocxp',
        format('Nota de crédito %s (id %s) sobre %s, monto %s', p_numero, v_doc, p_origen, p_monto));
    RETURN v_doc;
END $$;

-- Movimiento manual (o apertura) de una cuenta bancaria; si la cuenta es en moneda extranjera usa la tasa del día.
CREATE FUNCTION pg_temp.d_mov(p_cuenta TEXT, p_fecha DATE, p_tipo TEXT, p_monto NUMERIC, p_contra INT, p_desc TEXT, p_ref TEXT,
                              p_origen TEXT, p_email TEXT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_cb      cuentabancaria%ROWTYPE;
    v_mon     moneda%ROWTYPE;
    v_tc      historialtipocambio%ROWTYPE;
    v_tasa    NUMERIC := 1;
    v_gtq     NUMERIC;
    v_asiento INT;
    v_id      INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT * INTO STRICT v_cb FROM cuentabancaria WHERE numero = p_cuenta;
    SELECT * INTO STRICT v_mon FROM moneda WHERE id = v_cb.moneda_id;
    IF NOT v_mon.es_funcional THEN
        SELECT * INTO v_tc FROM historialtipocambio WHERE moneda_id = v_mon.id AND fecha = p_fecha AND activa;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'No hay tipo de cambio de % para %', v_mon.codigo, p_fecha;
        END IF;
        v_tasa := v_tc.tasa;
    END IF;
    v_gtq := ROUND(p_monto * v_tasa, 2);
    v_asiento := pg_temp.d_asiento('TES-' || UPPER(LEFT(p_origen, 3)) || '-' || to_char(p_fecha, 'YYYYMMDD') || '-' || substr(md5(random()::TEXT || clock_timestamp()::TEXT), 1, 8),
        p_fecha, v_usuario, NULL, jsonb_build_array(
            jsonb_build_array(v_cb.cuenta_contable_id, CASE WHEN p_tipo = 'Ingreso' THEN v_gtq ELSE 0 END, CASE WHEN p_tipo = 'Ingreso' THEN 0 ELSE v_gtq END, NULL),
            jsonb_build_array(p_contra, CASE WHEN p_tipo = 'Ingreso' THEN 0 ELSE v_gtq END, CASE WHEN p_tipo = 'Ingreso' THEN v_gtq ELSE 0 END, NULL)));
    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, referencia, origen, monto_gtq, tipo_cambio, tipo_cambio_id)
    VALUES (v_cb.id, p_fecha, p_tipo, p_monto, v_asiento, p_desc, p_ref, p_origen, v_gtq, v_tasa, v_tc.id)
    RETURNING id INTO v_id;
    CALL sp_registrar_auditoria(v_usuario, 'registrar_movimiento_tesoreria', 'movimientotesoreria',
        format('Movimiento %s (%s %s) en cuenta %s', v_id, p_tipo, p_monto, p_cuenta));
    RETURN v_id;
END $$;

-- Cobro (CxC) o pago (CxP) con liquidación idéntica a TesoreriaService: libros, valor, diferencial realizado y retenciones.
-- p_apps: [[numero_factura, monto_aplicado_en_moneda_del_documento, iva_retenido_gtq|null], ...]
-- Retención: IVA al cliente si el tercer elemento viene; ISR a no residente (art. 104) si el proveedor es no residente (al tipo de cambio del día del pago).
CREATE FUNCTION pg_temp.d_pago(p_sigla TEXT, p_tercero TEXT, p_fecha DATE, p_metodo TEXT, p_ref TEXT, p_cuenta TEXT, p_email TEXT, p_apps JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_cobro    BOOLEAN := (p_sigla = 'CxC');
    v_tipo     TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Cliente' ELSE 'Proveedor' END;
    v_usuario  INT;
    v_ter      contraparte%ROWTYPE;
    v_cb       cuentabancaria%ROWTYPE;
    v_cb_mon   moneda%ROWTYPE;
    v_cb_tc    historialtipocambio%ROWTYPE;
    v_cfg      configuracionfiscal%ROWTYPE;
    v_control  INT;
    v_app      JSONB;
    v_calc     JSONB := '[]'::JSONB;
    v_doc_id   INT;
    v_doc_mon  moneda%ROWTYPE;
    v_doc_tc   historialtipocambio%ROWTYPE;
    v_doc_fec  DATE;
    v_doc_tasa NUMERIC;
    v_doc_iva  NUMERIC;
    v_sal_o    NUMERIC;
    v_sal_g    NUMERIC;
    v_par      parametroretencion%ROWTYPE;
    v_monto    NUMERIC;
    v_libros   NUMERIC;
    v_valor    NUMERIC;
    v_dif      NUMERIC;
    v_retg     NUMERIC;
    v_reto     NUMERIC;
    v_baseg    NUMERIC;
    v_iva_ret  NUMERIC;
    v_s_monto  NUMERIC := 0;
    v_s_libros NUMERIC := 0;
    v_s_valor  NUMERIC := 0;
    v_s_retg   NUMERIC := 0;
    v_s_reto   NUMERIC := 0;
    v_gan      NUMERIC := 0;
    v_per      NUMERIC := 0;
    v_usa      BOOLEAN;
    v_m_banco  NUMERIC;
    v_m_cuenta NUMERIC;
    v_pago     INT;
    v_apl      INT;
    v_apls     INT[] := '{}';
    v_asiento  INT;
    v_lineas   JSONB;
    v_idx      INT := 0;
    v_mon_id   INT;
    v_cuenta_ret INT;
    v_tipo_mov TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Ingreso' ELSE 'Egreso' END;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT * INTO STRICT v_ter FROM contraparte WHERE tipo = v_tipo AND nombre = p_tercero;
    SELECT * INTO STRICT v_cb FROM cuentabancaria WHERE numero = p_cuenta;
    SELECT * INTO STRICT v_cb_mon FROM moneda WHERE id = v_cb.moneda_id;
    SELECT * INTO STRICT v_cfg FROM configuracionfiscal WHERE id = 1;
    v_control := pg_temp.d_cta(CASE WHEN v_cobro THEN '1.1.03' ELSE '2.1.01' END);

    IF NOT v_cb_mon.es_funcional THEN
        SELECT * INTO v_cb_tc FROM historialtipocambio WHERE moneda_id = v_cb_mon.id AND fecha = p_fecha AND activa;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'No hay tipo de cambio de % para %', v_cb_mon.codigo, p_fecha;
        END IF;
    END IF;

    FOR v_app IN SELECT * FROM jsonb_array_elements(p_apps) LOOP
        v_monto := (v_app->>1)::NUMERIC;
        v_iva_ret := NULLIF(v_app->>2, '')::NUMERIC;
        IF v_cobro THEN
            SELECT d.id, d.moneda_id, d.fecha, d.tipo_cambio_aplicado, d.monto_iva, s.saldo_pendiente, s.saldo_pendiente_gtq
            INTO STRICT v_doc_id, v_mon_id, v_doc_fec, v_doc_tasa, v_doc_iva, v_sal_o, v_sal_g
            FROM documentocxc d JOIN vw_saldodocumentocxc s ON s.documento_id = d.id
            WHERE d.numero = v_app->>0 AND d.cliente_id = v_ter.id;
        ELSE
            SELECT d.id, d.moneda_id, d.fecha, d.tipo_cambio_aplicado, d.monto_iva, s.saldo_pendiente, s.saldo_pendiente_gtq
            INTO STRICT v_doc_id, v_mon_id, v_doc_fec, v_doc_tasa, v_doc_iva, v_sal_o, v_sal_g
            FROM documentocxp d JOIN vw_saldodocumentocxp s ON s.documento_id = d.id
            WHERE d.numero = v_app->>0 AND d.proveedor_id = v_ter.id;
        END IF;
        SELECT * INTO STRICT v_doc_mon FROM moneda WHERE id = v_mon_id;
        IF v_monto > v_sal_o THEN
            RAISE EXCEPTION 'El monto % excede el saldo % de %', v_monto, v_sal_o, v_app->>0;
        END IF;
        v_retg := 0; v_reto := 0; v_baseg := 0; v_par := NULL;

        IF v_doc_mon.es_funcional THEN
            IF NOT v_cb_mon.es_funcional THEN
                RAISE EXCEPTION 'Documento en moneda funcional con cuenta en moneda extranjera no soportado';
            END IF;
            v_libros := v_monto; v_valor := v_monto; v_dif := 0;
        ELSE
            SELECT * INTO v_doc_tc FROM historialtipocambio WHERE moneda_id = v_doc_mon.id AND fecha = p_fecha AND activa;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'No hay tipo de cambio de % para %', v_doc_mon.codigo, p_fecha;
            END IF;
            IF NOT v_cb_mon.es_funcional AND v_cb_mon.id <> v_doc_mon.id THEN
                RAISE EXCEPTION 'La cuenta bancaria debe estar en la moneda del documento o en quetzales';
            END IF;
            v_valor := ROUND(v_monto * CASE WHEN v_cb_mon.es_funcional THEN v_doc_tc.tasa ELSE v_cb_tc.tasa END, 2);
            IF v_monto = v_sal_o THEN
                v_libros := v_sal_g;
            ELSE
                v_libros := ROUND(v_monto * v_doc_tasa, 2);
                IF v_libros >= v_sal_g THEN
                    v_libros := GREATEST(v_sal_g - 0.01, 0);
                END IF;
            END IF;
            v_dif := CASE WHEN v_cobro THEN v_valor - v_libros ELSE v_libros - v_valor END;
        END IF;

        IF NOT v_cobro AND NOT v_ter.es_residente AND v_ter.regimen_isr <> 'EXENTO' THEN
            SELECT * INTO STRICT v_par FROM parametroretencion
            WHERE codigo = v_ter.tipo_renta_nr_codigo AND concepto = 'ISR_NO_RESIDENTE' AND activo
              AND vigente_desde <= p_fecha AND (vigente_hasta IS NULL OR vigente_hasta >= p_fecha);
            v_baseg := ROUND(v_monto * v_doc_tc.tasa, 2);
            v_retg := ROUND(v_baseg * v_par.tasa_base / 100, 2);
            v_reto := ROUND(v_monto * v_par.tasa_base / 100, 2);
        ELSIF v_cobro AND v_iva_ret IS NOT NULL THEN
            SELECT * INTO STRICT v_par FROM parametroretencion
            WHERE codigo = CASE WHEN v_ter.tipo_agente_iva = 'SECTOR_PUBLICO' THEN 'IVA_CLIENTE_SECTOR_PUBLICO' ELSE 'IVA_CLIENTE_AGENTE_PRIVADO' END
              AND activo AND vigente_desde <= v_doc_fec AND (vigente_hasta IS NULL OR vigente_hasta >= v_doc_fec);
            IF v_iva_ret <= 0 OR v_iva_ret > v_doc_iva OR v_iva_ret > v_monto THEN
                RAISE EXCEPTION 'IVA retenido % fuera de rango para %', v_iva_ret, v_app->>0;
            END IF;
            v_retg := v_iva_ret; v_reto := v_iva_ret; v_baseg := v_doc_iva;
        END IF;

        v_s_monto := v_s_monto + v_monto; v_s_libros := v_s_libros + v_libros; v_s_valor := v_s_valor + v_valor;
        v_s_retg := v_s_retg + v_retg; v_s_reto := v_s_reto + v_reto;
        v_gan := v_gan + GREATEST(v_dif, 0); v_per := v_per + GREATEST(-v_dif, 0);
        v_calc := v_calc || jsonb_build_array(jsonb_build_object(
            'doc', v_doc_id, 'monto', v_monto, 'libros', v_libros, 'valor', v_valor, 'dif', v_dif, 'retg', v_retg, 'reto', v_reto,
            'base', v_baseg, 'par', v_par.id, 'par_tasa', v_par.tasa_base, 'par_codigo', v_par.codigo, 'fecha_doc', v_doc_fec,
            'mon', v_doc_mon.id, 'tc_dia', v_doc_tc.tasa));
    END LOOP;

    v_mon_id := (v_calc->0->>'mon')::INT;
    v_usa := (NOT v_cb_mon.es_funcional) OR (NOT (SELECT es_funcional FROM moneda WHERE id = v_mon_id));
    IF v_usa THEN
        v_m_banco := v_s_valor - v_s_retg;
        v_m_cuenta := CASE WHEN v_cb_mon.es_funcional THEN v_m_banco ELSE v_s_monto - v_s_reto END;
    ELSE
        v_m_banco := v_s_libros - v_s_retg;
        v_m_cuenta := v_m_banco;
    END IF;
    IF v_m_banco <= 0 OR v_m_cuenta <= 0 THEN
        RAISE EXCEPTION 'El neto del pago debe ser positivo';
    END IF;

    IF v_cobro THEN
        INSERT INTO recibopagocliente (cliente_id, fecha, monto_total, metodo_pago, referencia_bancaria, cuenta_bancaria_id, moneda_id, monto_total_gtq)
        VALUES (v_ter.id, p_fecha, v_s_monto, p_metodo, p_ref, v_cb.id, v_mon_id, v_s_valor) RETURNING id INTO v_pago;
    ELSE
        INSERT INTO pagoproveedorcabecera (proveedor_id, fecha, monto_total, metodo_pago, referencia_bancaria, cuenta_bancaria_id, moneda_id, monto_total_gtq)
        VALUES (v_ter.id, p_fecha, v_s_monto, p_metodo, p_ref, v_cb.id, v_mon_id, v_s_valor) RETURNING id INTO v_pago;
    END IF;

    FOR v_app IN SELECT * FROM jsonb_array_elements(v_calc) LOOP
        IF v_cobro THEN
            INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq, tipo_cambio_pago_id)
            VALUES (v_pago, (v_app->>'doc')::INT, (v_app->>'monto')::NUMERIC, (v_app->>'libros')::NUMERIC, (v_app->>'valor')::NUMERIC,
                    (v_app->>'dif')::NUMERIC, v_cb_tc.id) RETURNING id INTO v_apl;
        ELSE
            INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado, monto_aplicado_gtq, valor_gtq, diferencial_gtq, tipo_cambio_pago_id)
            VALUES (v_pago, (v_app->>'doc')::INT, (v_app->>'monto')::NUMERIC, (v_app->>'libros')::NUMERIC, (v_app->>'valor')::NUMERIC,
                    (v_app->>'dif')::NUMERIC, v_cb_tc.id) RETURNING id INTO v_apl;
        END IF;
        v_apls := v_apls || v_apl;
    END LOOP;

    v_cuenta_ret := CASE WHEN v_cobro THEN v_cfg.cuenta_iva_retenido_clientes_id ELSE v_cfg.cuenta_isr_nr_retenido_pagar_id END;
    v_lineas := jsonb_build_array(
        jsonb_build_array(v_cb.cuenta_contable_id, CASE WHEN v_cobro THEN v_m_banco ELSE 0 END, CASE WHEN v_cobro THEN 0 ELSE v_m_banco END, NULL),
        jsonb_build_array(v_control, CASE WHEN v_cobro THEN 0 ELSE v_s_libros END, CASE WHEN v_cobro THEN v_s_libros ELSE 0 END, NULL),
        jsonb_build_array(v_cfg.cuenta_dif_ganancia_realizada_id, 0, v_gan, NULL),
        jsonb_build_array(v_cfg.cuenta_dif_perdida_realizada_id, v_per, 0, NULL),
        jsonb_build_array(v_cuenta_ret, CASE WHEN v_cobro THEN v_s_retg ELSE 0 END, CASE WHEN v_cobro THEN 0 ELSE v_s_retg END, NULL));
    v_asiento := pg_temp.d_asiento('TES-' || UPPER(p_sigla) || '-' || to_char(p_fecha, 'YYYYMMDD') || '-' || substr(md5(random()::TEXT || clock_timestamp()::TEXT), 1, 8),
        p_fecha, v_usuario, NULL, v_lineas);

    INSERT INTO movimientotesoreria (cuenta_bancaria_id, fecha, tipo, monto, asiento_id, descripcion, referencia, origen, recibo_pago_id,
                                     pago_proveedor_id, monto_gtq, tipo_cambio, tipo_cambio_id)
    VALUES (v_cb.id, p_fecha, v_tipo_mov, v_m_cuenta, v_asiento, CASE WHEN v_cobro THEN 'Cobro ' ELSE 'Pago ' END || v_pago || ' - ' || p_tercero,
            p_ref, p_sigla, CASE WHEN v_cobro THEN v_pago END, CASE WHEN v_cobro THEN NULL ELSE v_pago END,
            v_m_banco, COALESCE(v_cb_tc.tasa, 1), v_cb_tc.id);

    FOR v_app IN SELECT * FROM jsonb_array_elements(v_calc) LOOP
        v_idx := v_idx + 1;
        IF (v_app->>'retg')::NUMERIC > 0 THEN
            IF v_cobro THEN
                INSERT INTO retencion (concepto, contraparte_id, documento_cxc_id, aplicacion_pago_cliente_id, parametro_id, fecha_documento, fecha_operacion,
                                       cuenta_id, base, tasa, monto_calculado, monto_retenido, monto_retenido_original, asiento_id, registrado_por)
                VALUES ('IVA_CLIENTE', v_ter.id, (v_app->>'doc')::INT, v_apls[v_idx], (v_app->>'par')::INT, (v_app->>'fecha_doc')::DATE, p_fecha,
                        v_cuenta_ret, (v_app->>'base')::NUMERIC, (v_app->>'par_tasa')::NUMERIC,
                        ROUND((v_app->>'base')::NUMERIC * (v_app->>'par_tasa')::NUMERIC / 100, 2), (v_app->>'retg')::NUMERIC,
                        (v_app->>'retg')::NUMERIC, v_asiento, v_usuario);
            ELSE
                INSERT INTO retencion (concepto, contraparte_id, documento_cxp_id, aplicacion_pago_proveedor_id, parametro_id, fecha_documento, fecha_operacion,
                                       cuenta_id, base, tasa, monto_calculado, monto_retenido, monto_retenido_original, asiento_id, registrado_por,
                                       moneda_id, monto_origen, tipo_cambio, tipo_cambio_fecha, tipo_renta_codigo)
                VALUES ('ISR_NORES', v_ter.id, (v_app->>'doc')::INT, v_apls[v_idx], (v_app->>'par')::INT, (v_app->>'fecha_doc')::DATE, p_fecha,
                        v_cuenta_ret, (v_app->>'base')::NUMERIC, (v_app->>'par_tasa')::NUMERIC, (v_app->>'retg')::NUMERIC, (v_app->>'retg')::NUMERIC,
                        (v_app->>'reto')::NUMERIC, v_asiento, v_usuario, (v_app->>'mon')::INT, (v_app->>'monto')::NUMERIC,
                        (v_app->>'tc_dia')::NUMERIC, p_fecha, v_app->>'par_codigo');
            END IF;
        END IF;
    END LOOP;

    CALL sp_registrar_auditoria(v_usuario, CASE WHEN v_cobro THEN 'registrar_pago_cxc' ELSE 'registrar_pago_cxp' END,
        CASE WHEN v_cobro THEN 'recibopagocliente' ELSE 'pagoproveedorcabecera' END,
        format('%s %s (%s) registrado, monto %s, retenido Q%s, diferencial Q%s', CASE WHEN v_cobro THEN 'Recibo' ELSE 'Pago' END, v_pago, p_tercero,
               v_s_monto, v_s_retg, v_gan - v_per));
    RETURN v_pago;
END $$;


-- =====================================================================
-- BLOQUE 1: tipos de cambio USD y EUR (28-ago a 17-oct 2026, diarios). No usa helpers.
-- =====================================================================
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM historialtipocambio) THEN
        RAISE EXCEPTION 'Los tipos de cambio demo ya fueron cargados.';
    END IF;
END $$;

INSERT INTO historialtipocambio (moneda_id, fecha, tasa, fuente, fecha_referencia, arrastrado, motivo, usuario_id, activa)
SELECT m.id, d::DATE,
       CASE m.codigo
            WHEN 'USD' THEN ROUND((7.7000 + 0.0006 * x.n + 0.0120 * sin(x.n * 0.9))::NUMERIC, 4)
            ELSE ROUND(((7.7000 + 0.0006 * x.n + 0.0120 * sin(x.n * 0.9)) * 1.165 + 0.0060 * cos(x.n * 0.7))::NUMERIC, 4)
       END,
       'Manual', d::DATE, FALSE, 'Tipo de cambio de referencia para datos de demostración',
       (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt'), TRUE
FROM generate_series(DATE '2026-08-28', DATE '2026-10-17', INTERVAL '1 day') AS d
CROSS JOIN moneda m
CROSS JOIN LATERAL (SELECT (d::DATE - DATE '2026-08-28') AS n) x
WHERE m.codigo IN ('USD', 'EUR');

-- VERIFICAR: SELECT m.codigo, COUNT(*), MIN(h.tasa), MAX(h.tasa) FROM historialtipocambio h JOIN moneda m ON m.id = h.moneda_id GROUP BY 1;  -- 51 filas por moneda
COMMIT;


-- =====================================================================
-- BLOQUE 2: contrapartes nuevas (2 proveedores no residentes, 2 proveedores ISR simplificado, 1 cliente agente de retención de IVA, 1 cliente extranjero)
-- =====================================================================
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM contraparte WHERE nombre = 'Cloudware Solutions, Inc.') THEN
        RAISE EXCEPTION 'Las contrapartes demo del lote 3 ya fueron cargadas.';
    END IF;
END $$;

INSERT INTO contraparte (tipo, nombre, nit, direccion, regimen_iva, regimen_isr, es_residente, es_agente_retencion_iva, tipo_agente_iva, tipo_renta_nr_codigo, pais) VALUES
    ('Proveedor', 'Cloudware Solutions, Inc.',               NULL,        '500 Howard Street, San Francisco, CA 94105, Estados Unidos', 'GENERAL', 'NO_DEFINIDO', FALSE, FALSE, NULL, 'NR_HONORARIOS_ASESORIA', 'US'),
    ('Proveedor', 'Iberia Software Licencias, S.L.',         NULL,        'Calle de Alcalá 120, 28009 Madrid, España',                  'GENERAL', 'NO_DEFINIDO', FALSE, FALSE, NULL, 'NR_REGALIAS',            'ES'),
    ('Proveedor', 'Transportes Rápidos del Sur, S.A.',       '5918274-1', 'Km 28 Carretera a Palín, Escuintla',                         'GENERAL', 'SIMPLIFICADO', TRUE,  FALSE, NULL, NULL, NULL),
    ('Proveedor', 'Consultores Asociados Delta Norte, S.A.', '3047159-1', '10a. Avenida 15-20, Zona 10, Ciudad de Guatemala',           'GENERAL', 'SIMPLIFICADO', TRUE,  FALSE, NULL, NULL, NULL),
    ('Cliente',   'Corporación Industrial Quetzal, S.A.',    '8120463-9', 'Boulevard Los Próceres 24-69, Zona 10, Ciudad de Guatemala', 'GENERAL', 'UTILIDADES',   TRUE,  TRUE,  'AGENTE_PRIVADO', NULL, NULL),
    ('Cliente',   'Pacific Trade Partners, LLC',             NULL,        '1200 Brickell Avenue, Miami, FL 33131, Estados Unidos',     'GENERAL', 'NO_DEFINIDO',  FALSE, FALSE, NULL, NULL, 'US');

-- VERIFICAR: SELECT COUNT(*) FROM contraparte;  -- 18
COMMIT;


-- =====================================================================
-- BLOQUE 3: septiembre 2026 reabierto -> facturas USD, pago con ISR a no residente, revaluación del 30-sep (+ reversa 1-oct) y cierre de septiembre
-- Requiere helpers + bloques 1 y 2. Un solo BEGIN..COMMIT: si algo falla, septiembre queda como estaba (cerrado).
-- =====================================================================
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM documentocxp WHERE numero = 'P-3001') OR EXISTS (SELECT 1 FROM revaluacion) THEN
        RAISE EXCEPTION 'El bloque 3 ya fue aplicado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM historialtipocambio) OR NOT EXISTS (SELECT 1 FROM contraparte WHERE nombre = 'Cloudware Solutions, Inc.') THEN
        RAISE EXCEPTION 'Faltan los bloques 1 y 2.';
    END IF;
    IF (SELECT estado FROM periodocontable WHERE nombre = 'Septiembre 2026') <> 'Cerrado'
       OR (SELECT estado FROM periodocontable WHERE nombre = 'Octubre 2026') <> 'Abierto' THEN
        RAISE EXCEPTION 'Se esperaba Septiembre cerrado y Octubre abierto.';
    END IF;
END $$;

DO $$
DECLARE
    v_p INT := (SELECT id FROM periodocontable WHERE nombre = 'Septiembre 2026');
    v_u INT := (SELECT id FROM usuario WHERE email = 'admin@delta.com.gt');
BEGIN
    CALL sp_reabrir_periodo(v_p, v_u);
END $$;

SELECT pg_temp.d_factura('CxP', 'P-3001', 'Cloudware Solutions, Inc.', DATE '2026-09-03', DATE '2026-10-03', 'contador@delta.com.gt',
    'USD', 'NO_AFECTO', '5.1.02', 'Licencias de plataforma en la nube y soporte', 2000);
SELECT pg_temp.d_factura('CxC', 'F-1102', 'Pacific Trade Partners, LLC', DATE '2026-09-10', DATE '2026-10-10', 'vendedor@delta.com.gt',
    'USD', 'EXENTO_EXPORTACION', '4.1.02', 'Exportación de servicios de consultoría', 4000);
SELECT pg_temp.d_factura('CxP', 'P-3002', 'Iberia Software Licencias, S.L.', DATE '2026-09-16', DATE '2026-10-16', 'contador@delta.com.gt',
    'USD', 'NO_AFECTO', '5.1.02', 'Regalías por licencia de software', 1500);

SELECT pg_temp.d_pago('CxP', 'Cloudware Solutions, Inc.', DATE '2026-09-22', 'Transferencia', 'TRF-NR-0922', '40-123456-7', 'contador@delta.com.gt',
    '[["P-3001", 1500]]');

DO $$
DECLARE
    v_user   INT := (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt');
    v_usd    INT := (SELECT id FROM moneda WHERE codigo = 'USD');
    v_cfg    configuracionfiscal%ROWTYPE;
    v_tc     historialtipocambio%ROWTYPE;
    v_c_cxc  INT := pg_temp.d_cta('1.1.03');
    v_c_cxp  INT := pg_temp.d_cta('2.1.01');
    r        RECORD;
    v_cierre NUMERIC;
    v_aj     NUMERIC;
    v_gan    NUMERIC := 0;
    v_per    NUMERIC := 0;
    v_e_cxc  NUMERIC := 0;
    v_e_cxp  NUMERIC := 0;
    v_parts  JSONB := '[]'::JSONB;
    v_lineas JSONB;
    v_inv    JSONB;
    v_a      INT;
    v_r      INT;
    v_rev    INT;
BEGIN
    SELECT * INTO STRICT v_cfg FROM configuracionfiscal WHERE id = 1;
    SELECT * INTO STRICT v_tc FROM historialtipocambio WHERE moneda_id = v_usd AND fecha = DATE '2026-09-30' AND activa;

    FOR r IN
        SELECT 'CxC' AS tipo, d.id AS doc, v.saldo_pendiente AS so, v.saldo_pendiente_gtq AS libros
        FROM documentocxc d JOIN vw_saldodocumentocxc v ON v.documento_id = d.id
        WHERE d.moneda_id = v_usd AND d.fecha <= DATE '2026-09-30' AND d.estado = 'Vigente' AND d.tipo_documento = 'Factura' AND v.saldo_pendiente > 0
        UNION ALL
        SELECT 'CxP', d.id, v.saldo_pendiente, v.saldo_pendiente_gtq
        FROM documentocxp d JOIN vw_saldodocumentocxp v ON v.documento_id = d.id
        WHERE d.moneda_id = v_usd AND d.fecha <= DATE '2026-09-30' AND d.estado = 'Vigente' AND d.tipo_documento = 'Factura' AND v.saldo_pendiente > 0
        ORDER BY 1, 2
    LOOP
        v_cierre := ROUND(r.so * v_tc.tasa, 2);
        v_aj := v_cierre - r.libros;
        IF v_aj <> 0 THEN
            v_parts := v_parts || jsonb_build_array(jsonb_build_object('tipo', r.tipo, 'doc', r.doc, 'so', r.so, 'libros', r.libros, 'cierre', v_cierre, 'aj', v_aj));
            IF r.tipo = 'CxC' THEN
                v_e_cxc := v_e_cxc + v_aj;
                IF v_aj > 0 THEN v_gan := v_gan + v_aj; ELSE v_per := v_per - v_aj; END IF;
            ELSE
                v_e_cxp := v_e_cxp - v_aj;
                IF v_aj < 0 THEN v_gan := v_gan - v_aj; ELSE v_per := v_per + v_aj; END IF;
            END IF;
        END IF;
    END LOOP;
    IF v_gan + v_per <= 0 THEN
        RAISE EXCEPTION 'La revaluación no tiene diferencias que asentar';
    END IF;

    v_lineas := jsonb_build_array(
        jsonb_build_array(v_c_cxc, GREATEST(v_e_cxc, 0), GREATEST(-v_e_cxc, 0), NULL),
        jsonb_build_array(v_c_cxp, GREATEST(v_e_cxp, 0), GREATEST(-v_e_cxp, 0), NULL),
        jsonb_build_array(v_cfg.cuenta_reval_ganancia_id, 0, v_gan, NULL),
        jsonb_build_array(v_cfg.cuenta_reval_perdida_id, v_per, 0, NULL));
    v_inv := jsonb_build_array(
        jsonb_build_array(v_c_cxc, GREATEST(-v_e_cxc, 0), GREATEST(v_e_cxc, 0), NULL),
        jsonb_build_array(v_c_cxp, GREATEST(-v_e_cxp, 0), GREATEST(v_e_cxp, 0), NULL),
        jsonb_build_array(v_cfg.cuenta_reval_ganancia_id, v_gan, 0, NULL),
        jsonb_build_array(v_cfg.cuenta_reval_perdida_id, 0, v_per, NULL));

    v_a := pg_temp.d_asiento('REVAL-USD-202609', DATE '2026-09-30', v_user, v_tc.tasa, v_lineas);
    v_r := pg_temp.d_asiento('REVAL-USD-202609-R', DATE '2026-10-01', v_user, v_tc.tasa, v_inv);

    INSERT INTO revaluacion (moneda_id, anio, mes, fecha_cierre, tipo_cambio_id, tasa, total_ganancia, total_perdida, asiento_id, asiento_reversa_id, creado_por)
    VALUES (v_usd, 2026, 9, DATE '2026-09-30', v_tc.id, v_tc.tasa, v_gan, v_per, v_a, v_r, v_user)
    RETURNING id INTO v_rev;

    INSERT INTO revaluacionpartida (revaluacion_id, tipo, documento_cxc_id, documento_cxp_id, cuenta_id, saldo_original, valor_libros_gtq, valor_cierre_gtq, ajuste_gtq)
    SELECT v_rev, p->>'tipo', CASE WHEN p->>'tipo' = 'CxC' THEN (p->>'doc')::INT END, CASE WHEN p->>'tipo' = 'CxP' THEN (p->>'doc')::INT END,
           CASE WHEN p->>'tipo' = 'CxC' THEN v_c_cxc ELSE v_c_cxp END,
           (p->>'so')::NUMERIC, (p->>'libros')::NUMERIC, (p->>'cierre')::NUMERIC, (p->>'aj')::NUMERIC
    FROM jsonb_array_elements(v_parts) p;

    CALL sp_registrar_auditoria(v_user, 'ejecutar_revaluacion', 'revaluacion',
        format('Revaluación USD 09/2026 a %s: ganancia %s, pérdida %s, asientos %s y %s', v_tc.tasa, v_gan, v_per, v_a, v_r));
END $$;

DO $$
DECLARE
    v_p INT := (SELECT id FROM periodocontable WHERE nombre = 'Septiembre 2026');
    v_u INT := (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt');
BEGIN
    CALL sp_cerrar_periodo(v_p, v_u);
END $$;

-- VERIFICAR: SELECT nombre, estado, cierres FROM periodocontable ORDER BY id;  -- Septiembre Cerrado, cierres = 3; Octubre Abierto
-- VERIFICAR: SELECT * FROM revaluacion; SELECT * FROM revaluacionpartida;  -- 1 revaluación vigente; 3 partidas (P-3001, F-1102, P-3002)
-- VERIFICAR: SELECT a.numero, a.fecha, SUM(l.debito) d, SUM(l.credito) c FROM asientocontable a JOIN lineaasiento l ON l.asiento_id = a.id WHERE a.numero LIKE 'REVAL-USD%' GROUP BY 1, 2;  -- d = c
-- VERIFICAR: SELECT * FROM retencion WHERE concepto = 'ISR_NORES';  -- 1 fila, 15% de round(1500*TC 22-sep)
-- VERIFICAR: SELECT * FROM vw_saldo_enterar;  -- periodo 2026-09-01 con retenido > 0 y saldo = retenido
COMMIT;


-- =====================================================================
-- BLOQUE 4: octubre 2026 -> cuenta bancaria en USD, facturas con retención de ISR (residente) e IVA (cliente agente), cobro/pago en USD con
-- diferencial cambiario, ISR a no residentes, enteramiento de septiembre, nota de crédito con ajuste de retención pendiente y conciliación USD.
-- Requiere helpers + bloques 1, 2 y 3.
-- =====================================================================
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM documentocxp WHERE numero = 'P-2101') OR EXISTS (SELECT 1 FROM cuentabancaria WHERE numero = '40-654321-0') THEN
        RAISE EXCEPTION 'El bloque 4 ya fue aplicado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM revaluacion) THEN
        RAISE EXCEPTION 'Falta el bloque 3.';
    END IF;
END $$;

-- 4.1 Cuenta bancaria en USD con saldo de apertura (1-oct)
-- Hermana de Bancos (1.1.02): Bancos tiene saldo propio y el catálogo no admite subcuentas nuevas bajo una cuenta con saldo.
INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT '1.1.07', 'Bancos - BAC Dólares', b.tipo, b.naturaleza, b.cuenta_padre_id FROM cuentacontable b WHERE b.codigo = '1.1.02';

INSERT INTO cuentabancaria (banco, numero, tipo, cuenta_contable_id, saldo_apertura, fecha_apertura, moneda_id)
VALUES ('BAC', '40-654321-0', 'Monetaria', pg_temp.d_cta('1.1.07'), 15000, DATE '2026-10-01', (SELECT id FROM moneda WHERE codigo = 'USD'));

SELECT pg_temp.d_mov('40-654321-0', DATE '2026-10-01', 'Ingreso', 15000, pg_temp.d_cta('3.1.01'), 'Saldo de apertura', NULL, 'Apertura', 'contador@delta.com.gt');

-- 4.2 Proveedores ISR simplificado: retención de ISR en la factura (escala 5% hasta Q30,000 y 7% el excedente)
SELECT pg_temp.d_factura('CxP', 'P-2101', 'Transportes Rápidos del Sur, S.A.', DATE '2026-10-05', DATE '2026-11-04', 'contador@delta.com.gt',
    'GTQ', 'IVA_GENERAL', '5.1.02', 'Servicio de transporte y logística de octubre', 44800, TRUE);
SELECT pg_temp.d_factura('CxP', 'P-2102', 'Consultores Asociados Delta Norte, S.A.', DATE '2026-10-09', DATE '2026-11-08', 'contador@delta.com.gt',
    'GTQ', 'IVA_GENERAL', '5.1.02', 'Asesoría contable y fiscal de octubre', 13440, TRUE);
-- P-2101: base Q40,000.00 -> ISR Q2,200.00 (saldo neto Q42,600.00). P-2102: base Q12,000.00 -> ISR Q600.00 (queda abierta).
SELECT pg_temp.d_pago('CxP', 'Transportes Rápidos del Sur, S.A.', DATE '2026-10-12', 'Transferencia', 'TRF-0412', '40-123456-7', 'contador@delta.com.gt',
    '[["P-2101", 42600]]');

INSERT INTO constanciaretencion (retencion_id, tipo, numero, fecha, estado, creado_por)
SELECT r.id, 'ISR', 'ISR-2026-0001', r.fecha_documento, 'EMITIDA', (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt')
FROM retencion r JOIN documentocxp d ON d.id = r.documento_cxp_id WHERE d.numero = 'P-2101' AND r.concepto = 'ISR_ROS';

-- 4.3 Cliente agente de retención de IVA: factura de Q50,000 (IVA Q5,357.14), cobro con IVA retenido 15% = Q803.57
SELECT pg_temp.d_factura('CxC', 'F-1101', 'Corporación Industrial Quetzal, S.A.', DATE '2026-10-06', DATE '2026-11-05', 'vendedor@delta.com.gt',
    'GTQ', 'IVA_GENERAL', '4.1.02', 'Servicios de implementación y soporte técnico', 50000);
SELECT pg_temp.d_pago('CxC', 'Corporación Industrial Quetzal, S.A.', DATE '2026-10-13', 'Transferencia', 'TRF-IN-0913', '40-123456-7', 'contador@delta.com.gt',
    '[["F-1101", 50000, 803.57]]');

-- 4.4 Exportación en USD con cobro parcial en la cuenta USD (diferencial cambiario realizado)
SELECT pg_temp.d_factura('CxC', 'F-1103', 'Pacific Trade Partners, LLC', DATE '2026-10-07', DATE '2026-11-06', 'vendedor@delta.com.gt',
    'USD', 'EXENTO_EXPORTACION', '4.1.02', 'Exportación de servicios de desarrollo', 2500);
SELECT pg_temp.d_pago('CxC', 'Pacific Trade Partners, LLC', DATE '2026-10-15', 'Transferencia', 'WIRE-8841', '40-654321-0', 'contador@delta.com.gt',
    '[["F-1103", 1500]]');
-- Cobro parcial de la exportación de septiembre (F-1102) desde la misma cuenta USD: libros al TC de la factura, valor al TC del día
SELECT pg_temp.d_pago('CxC', 'Pacific Trade Partners, LLC', DATE '2026-10-16', 'Transferencia', 'WIRE-8860', '40-654321-0', 'contador@delta.com.gt',
    '[["F-1102", 2000]]');

-- 4.5 Proveedores no residentes: ISR art. 104 al tipo de cambio del día del pago
SELECT pg_temp.d_pago('CxP', 'Iberia Software Licencias, S.L.', DATE '2026-10-15', 'Transferencia', 'WIRE-OUT-3302', '40-654321-0', 'contador@delta.com.gt',
    '[["P-3002", 1000]]');
SELECT pg_temp.d_factura('CxP', 'P-3003', 'Cloudware Solutions, Inc.', DATE '2026-10-08', DATE '2026-11-07', 'contador@delta.com.gt',
    'USD', 'NO_AFECTO', '5.1.02', 'Soporte técnico gestionado de octubre', 1200);
SELECT pg_temp.d_pago('CxP', 'Cloudware Solutions, Inc.', DATE '2026-10-16', 'Transferencia', 'TRF-NR-1016', '40-123456-7', 'contador@delta.com.gt',
    '[["P-3003", 1200]]');

-- 4.6 Enteramiento del ISR retenido a no residentes de septiembre (pagado el 14-oct, décimo día hábil); octubre queda con saldo por enterar
DO $$
DECLARE
    v_user  INT := (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt');
    v_cfg   configuracionfiscal%ROWTYPE;
    v_monto NUMERIC;
    v_mov   INT;
    v_ase   INT;
    v_cb    INT;
    v_id    INT;
BEGIN
    SELECT * INTO STRICT v_cfg FROM configuracionfiscal WHERE id = 1;
    SELECT saldo INTO STRICT v_monto FROM vw_saldo_enterar WHERE periodo = DATE '2026-09-01';
    IF v_monto <= 0 THEN
        RAISE EXCEPTION 'No hay ISR de septiembre por enterar';
    END IF;
    v_mov := pg_temp.d_mov('40-123456-7', DATE '2026-10-14', 'Egreso', v_monto, v_cfg.cuenta_isr_nr_retenido_pagar_id,
        'Enteramiento SAT-1352 boleta BOL-1352-092026-0148 período 2026-09', 'BOL-1352-092026-0148', 'Manual', 'contador@delta.com.gt');
    SELECT asiento_id, cuenta_bancaria_id INTO STRICT v_ase, v_cb FROM movimientotesoreria WHERE id = v_mov;
    INSERT INTO enteramiento (formulario, periodo, fecha_pago, boleta, monto, cuenta_bancaria_id, asiento_id, movimiento_tesoreria_id, registrado_por)
    VALUES ('SAT-1352', DATE '2026-09-01', DATE '2026-10-14', 'BOL-1352-092026-0148', v_monto, v_cb, v_ase, v_mov, v_user)
    RETURNING id INTO v_id;
    CALL sp_registrar_auditoria(v_user, 'registrar_enteramiento', 'enteramiento',
        format('Enteramiento %s: SAT-1352 período 2026-09, monto Q%s, asiento %s', v_id, v_monto, v_ase));
END $$;

-- 4.7 Nota de crédito (USD 200) sobre P-3001 posterior al enteramiento de septiembre -> ajuste de retención PENDIENTE
SELECT pg_temp.d_nc_cxp('NC-4001', 'P-3001', 'Cloudware Solutions, Inc.', DATE '2026-10-16', 'contador@delta.com.gt', 200, '5.1.02');

INSERT INTO ajuste_retencion (retencion_id, nota_credito_id, monto_nc, creado_por)
SELECT r.id, nc.id, nc.monto_total, (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt')
FROM retencion r
JOIN documentocxp f ON f.id = r.documento_cxp_id AND f.numero = 'P-3001'
JOIN documentocxp nc ON nc.documento_origen_id = f.id AND nc.numero = 'NC-4001'
WHERE r.concepto = 'ISR_NORES';

-- 4.8 Conciliación de la cuenta USD (movimientos hasta el 16-oct) con diferencia cero y finalizada por el procedimiento oficial
DO $$
DECLARE
    v_user   INT := (SELECT id FROM usuario WHERE email = 'contador@delta.com.gt');
    v_cb     INT := (SELECT id FROM cuentabancaria WHERE numero = '40-654321-0');
    v_extr   NUMERIC;
    v_conc   INT;
BEGIN
    SELECT cb.saldo_apertura + COALESCE(SUM(CASE WHEN m.tipo = 'Ingreso' THEN m.monto ELSE -m.monto END), 0) INTO STRICT v_extr
    FROM cuentabancaria cb LEFT JOIN movimientotesoreria m ON m.cuenta_bancaria_id = cb.id AND m.origen <> 'Apertura' AND m.fecha <= DATE '2026-10-16'
    WHERE cb.id = v_cb GROUP BY cb.saldo_apertura;
    INSERT INTO conciliacionbancaria (cuenta_bancaria_id, periodo_id, fecha, estado, saldo_extracto)
    VALUES (v_cb, (SELECT id FROM periodocontable WHERE nombre = 'Octubre 2026'), DATE '2026-10-16', 'Pendiente', v_extr)
    RETURNING id INTO v_conc;
    INSERT INTO detalleconciliacion (conciliacion_id, movimiento_id)
    SELECT v_conc, m.id FROM movimientotesoreria m
    WHERE m.cuenta_bancaria_id = v_cb AND m.origen <> 'Apertura' AND m.fecha <= DATE '2026-10-16' ORDER BY m.fecha, m.id;
    CALL sp_finalizar_conciliacion(v_conc, v_user);
END $$;

-- VERIFICAR (cuadre): SELECT a.id, a.numero FROM asientocontable a JOIN lineaasiento l ON l.asiento_id = a.id GROUP BY a.id, a.numero HAVING SUM(l.debito) <> SUM(l.credito);  -- 0 filas
-- VERIFICAR (documentos): SELECT d.numero, d.moneda_id, d.monto_total_original, d.monto_total, s.saldo_pendiente, s.saldo_pendiente_gtq FROM documentocxc d JOIN vw_saldodocumentocxc s ON s.documento_id = d.id WHERE d.numero IN ('F-1101','F-1102','F-1103');  -- F-1101 0; F-1102 2000; F-1103 1000
-- VERIFICAR (documentos CxP): SELECT d.numero, s.saldo_pendiente, s.saldo_pendiente_gtq FROM documentocxp d JOIN vw_saldodocumentocxp s ON s.documento_id = d.id WHERE d.numero IN ('P-2101','P-2102','P-3001','P-3002','P-3003','NC-4001');  -- sin saldos negativos; P-3003 = 0; P-3001 = 300 USD
-- VERIFICAR (retenciones): SELECT concepto, COUNT(*), SUM(monto_retenido) FROM retencion GROUP BY 1;  -- ISR_ROS 2 (2800.00), IVA_CLIENTE 1 (803.57), ISR_NORES 3
-- VERIFICAR (reporte): SELECT retencion_id, concepto, pais, periodo_fiscal, monto_retenido, enterado_gtq, saldo_por_enterar_gtq, ajuste_pendiente, constancia_numero FROM vw_retencion_reporte ORDER BY retencion_id;
-- VERIFICAR (enterar): SELECT * FROM vw_saldo_enterar;  -- septiembre saldo 0; octubre retenido > 0, saldo = retenido
-- VERIFICAR (bancos): SELECT * FROM vw_saldocuentabancaria; SELECT * FROM vw_conciliacion_resumen WHERE cuenta_bancaria_id = (SELECT id FROM cuentabancaria WHERE numero = '40-654321-0');  -- saldo USD de la cuenta 40-654321-0 = 15000 + 1500 + 2000 - 850 = 17650; conciliación con diferencia 0 y estado Conciliado
-- VERIFICAR (diferencial): SELECT ap.documento_id, ap.monto_aplicado, ap.monto_aplicado_gtq, ap.valor_gtq, ap.diferencial_gtq FROM aplicacionpagocliente ap WHERE ap.diferencial_gtq <> 0;
-- VERIFICAR (balance): SELECT SUM(l.debito) - SUM(l.credito) FROM lineaasiento l JOIN asientocontable a ON a.id = l.asiento_id WHERE a.estado IN ('Confirmado','Anulado');  -- 0
-- VERIFICAR (periodos): SELECT nombre, estado FROM periodocontable ORDER BY id;  -- Ago/Sep Cerrado, Oct Abierto
COMMIT;
