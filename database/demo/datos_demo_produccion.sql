-- Datos de demostración aplicados manualmente a production (no los ejecuta run_migrations.sh).

-- 0. Guardia contra doble ejecución ------------------------------------------

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM cuentacontable WHERE codigo = '1')
       OR EXISTS (SELECT 1 FROM periodocontable WHERE nombre IN ('Agosto 2026', 'Octubre 2026')) THEN
        RAISE EXCEPTION 'Los datos demo ya fueron aplicados.';
    END IF;
END $$;

-- 1. Usuarios demo (reutilizan el hash del usuario semilla del mismo perfil) -----

INSERT INTO usuario (nombre, email, password_hash, perfil_id, activo)
SELECT d.nombre, d.email, s.password_hash, s.perfil_id, TRUE
FROM (VALUES
    ('Demo Administrador 1', 'demo.admin1@delta.com.gt',    'admin@delta.com.gt'),
    ('Demo Administrador 2', 'demo.admin2@delta.com.gt',    'admin@delta.com.gt'),
    ('Demo Contador 1',      'demo.contador1@delta.com.gt', 'contador@delta.com.gt'),
    ('Demo Contador 2',      'demo.contador2@delta.com.gt', 'contador@delta.com.gt'),
    ('Demo Vendedor 1',      'demo.vendedor1@delta.com.gt', 'vendedor@delta.com.gt'),
    ('Demo Vendedor 2',      'demo.vendedor2@delta.com.gt', 'vendedor@delta.com.gt'),
    ('Demo Técnico 1',       'demo.tecnico1@delta.com.gt',  'tecnico@delta.com.gt'),
    ('Demo Técnico 2',       'demo.tecnico2@delta.com.gt',  'tecnico@delta.com.gt')
) AS d(nombre, email, email_semilla)
JOIN usuario s ON s.email = d.email_semilla;

-- 2. Catálogo: cuentas contables --------------------------------------------------

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id) VALUES
    ('1', 'Activo',   'Activo',  'Deudora',   NULL),
    ('2', 'Pasivo',   'Pasivo',  'Acreedora', NULL),
    ('3', 'Capital',  'Capital', 'Acreedora', NULL),
    ('4', 'Ingresos', 'Ingreso', 'Acreedora', NULL),
    ('5', 'Gastos',   'Gasto',   'Deudora',   NULL);

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT v.codigo, v.nombre, p.tipo, p.naturaleza, p.id
FROM (VALUES
    ('1.1', 'Activo corriente',      '1'),
    ('1.2', 'Activo no corriente',   '1'),
    ('2.1', 'Pasivo corriente',      '2'),
    ('3.1', 'Capital contable',      '3'),
    ('4.1', 'Ingresos de operación', '4'),
    ('5.1', 'Gastos de operación',   '5')
) AS v(codigo, nombre, padre)
JOIN cuentacontable p ON p.codigo = v.padre;

INSERT INTO cuentacontable (codigo, nombre, tipo, naturaleza, cuenta_padre_id)
SELECT v.codigo, v.nombre, p.tipo, p.naturaleza, p.id
FROM (VALUES
    ('1.1.01', 'Caja',                               '1.1'),
    ('1.1.02', 'Bancos',                             '1.1'),
    ('1.1.03', 'Clientes (cuentas por cobrar)',      '1.1'),
    ('1.1.04', 'Inventario de mercaderías',          '1.1'),
    ('1.2.01', 'Mobiliario y equipo',                '1.2'),
    ('2.1.01', 'Proveedores (cuentas por pagar)',    '2.1'),
    ('2.1.02', 'IVA por pagar',                      '2.1'),
    ('3.1.01', 'Capital social',                     '3.1'),
    ('3.1.02', 'Utilidades retenidas',               '3.1'),
    ('4.1.01', 'Ventas de mercadería',               '4.1'),
    ('4.1.02', 'Ingresos por servicios',             '4.1'),
    ('5.1.01', 'Sueldos y salarios',                 '5.1'),
    ('5.1.02', 'Servicios (luz, internet, seguros)', '5.1'),
    ('5.1.03', 'Alquiler de oficina',                '5.1'),
    ('5.1.04', 'Papelería y útiles',                 '5.1')
) AS v(codigo, nombre, padre)
JOIN cuentacontable p ON p.codigo = v.padre;

-- 3. Catálogo: centros de costo, periodos y contrapartes --------------------------

INSERT INTO centrocosto (codigo, nombre) VALUES
    ('ADM', 'Administración'),
    ('VTA', 'Ventas'),
    ('OPS', 'Operaciones'),
    ('TI',  'TI'),
    ('FIN', 'Finanzas');

INSERT INTO periodocontable (nombre, fecha_inicio, fecha_fin, estado) VALUES
    ('Agosto 2026',  '2026-08-01', '2026-08-31', 'Abierto'),
    ('Octubre 2026', '2026-10-01', '2026-10-31', 'Abierto');

INSERT INTO contraparte (tipo, nombre, nit, direccion) VALUES
    ('Cliente',   'Distribuidora Los Pinos, S.A.',                '4829175-3', '6a. Avenida 12-45, Zona 1, Ciudad de Guatemala'),
    ('Cliente',   'Ferretería El Constructor, S.A.',              '6120384-K', '3a. Calle 7-21, Zona 4, Mixco'),
    ('Cliente',   'Colegio Santa Lucía, S.A.',                    '2917446-9', '12 Avenida 18-30, Zona 11, Ciudad de Guatemala'),
    ('Cliente',   'Hotel Casa Antigua, S.A.',                     '7305518-2', '5a. Avenida Norte 14, Antigua Guatemala, Sacatepéquez'),
    ('Cliente',   'Agroindustrias del Pacífico, S.A.',            '3584962-7', 'Km 92.5 Carretera al Pacífico, Escuintla'),
    ('Cliente',   'Clínica Médica San Rafael, S.A.',              '5471093-1', '9a. Calle 3-50, Zona 1, Quetzaltenango'),
    ('Proveedor', 'Papelería y Suministros Guatemala, S.A.',      '1938475-6', '8a. Avenida 9-12, Zona 9, Ciudad de Guatemala'),
    ('Proveedor', 'Distribuidora Eléctrica Centroamericana, S.A.', '8264017-4', 'Calzada Aguilar Batres 41-30, Zona 12, Ciudad de Guatemala'),
    ('Proveedor', 'Servicios Informáticos Nova, S.A.',            '4150628-8', '15 Calle 1-45, Zona 10, Ciudad de Guatemala'),
    ('Proveedor', 'Inmobiliaria Zona 10, S.A.',                   '2706359-5', 'Avenida Reforma 8-60, Zona 10, Ciudad de Guatemala'),
    ('Proveedor', 'Comercial Agrícola Maya, S.A.',                '6693821-0', '2a. Calle 4-15, Zona 3, Cobán, Alta Verapaz'),
    ('Proveedor', 'Seguros y Fianzas Chapín, S.A.',               '3017746-K', '7a. Avenida 11-19, Zona 9, Ciudad de Guatemala');

-- 4. Funciones auxiliares temporales (se descartan al final) ----------------------

CREATE FUNCTION pg_temp.demo_asiento(p_numero TEXT, p_fecha DATE, p_email TEXT, p_estado TEXT, p_lineas JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_usuario INT;
    v_periodo INT;
    v_id      INT;
    v_monto   NUMERIC;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id INTO STRICT v_periodo FROM periodocontable WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin;
    SELECT SUM((e->>2)::NUMERIC) INTO v_monto FROM jsonb_array_elements(p_lineas) e;

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    VALUES (p_numero, p_fecha, v_periodo, v_monto, p_estado, v_usuario)
    RETURNING id INTO v_id;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_id,
           (SELECT id FROM cuentacontable WHERE codigo = e->>0),
           (SELECT id FROM centrocosto WHERE codigo = e->>1),
           (e->>2)::NUMERIC,
           (e->>3)::NUMERIC
    FROM jsonb_array_elements(p_lineas) e;

    IF p_estado = 'Confirmado' THEN
        CALL sp_registrar_auditoria(v_usuario, 'registrar_asiento', 'asientocontable',
            format('Asiento %s (id %s) registrado en periodo %s', p_numero, v_id, v_periodo));
    END IF;
    RETURN v_id;
END $$;

-- p_lineas: [descripcion, cantidad, precio_unitario, porcentaje_impuesto, centro, cuenta]
CREATE FUNCTION pg_temp.demo_factura(p_sigla TEXT, p_numero TEXT, p_tercero TEXT, p_fecha DATE, p_venc DATE,
                                     p_email TEXT, p_control TEXT, p_lineas JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_es_cxc    BOOLEAN := (p_sigla = 'CxC');
    v_tipo      TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Cliente' ELSE 'Proveedor' END;
    v_usuario   INT;
    v_periodo   INT;
    v_tercero   INT;
    v_monto     NUMERIC;
    v_asiento   INT;
    v_documento INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id INTO STRICT v_periodo FROM periodocontable WHERE p_fecha BETWEEN fecha_inicio AND fecha_fin;
    SELECT id INTO STRICT v_tercero FROM contraparte WHERE tipo = v_tipo AND nombre = p_tercero;
    SELECT SUM(ROUND((e->>1)::NUMERIC * (e->>2)::NUMERIC * (1 + (e->>3)::NUMERIC / 100), 2))
    INTO v_monto FROM jsonb_array_elements(p_lineas) e;

    INSERT INTO asientocontable (numero, fecha, periodo_id, monto, estado, usuario_id)
    VALUES (UPPER(p_sigla) || '-' || p_numero, p_fecha, v_periodo, v_monto, 'Confirmado', v_usuario)
    RETURNING id INTO v_asiento;

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    VALUES (v_asiento, (SELECT id FROM cuentacontable WHERE codigo = p_control), NULL,
            CASE WHEN v_es_cxc THEN v_monto ELSE 0 END,
            CASE WHEN v_es_cxc THEN 0 ELSE v_monto END);

    INSERT INTO lineaasiento (asiento_id, cuenta_id, centro_costo_id, debito, credito)
    SELECT v_asiento,
           (SELECT id FROM cuentacontable WHERE codigo = e->>5),
           (SELECT id FROM centrocosto WHERE codigo = e->>4),
           CASE WHEN v_es_cxc THEN 0 ELSE ROUND((e->>1)::NUMERIC * (e->>2)::NUMERIC * (1 + (e->>3)::NUMERIC / 100), 2) END,
           CASE WHEN v_es_cxc THEN ROUND((e->>1)::NUMERIC * (e->>2)::NUMERIC * (1 + (e->>3)::NUMERIC / 100), 2) ELSE 0 END
    FROM jsonb_array_elements(p_lineas) e;

    IF v_es_cxc THEN
        INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, estado, asiento_id)
        VALUES (p_numero, 'Factura', v_tercero, p_fecha, p_venc, v_monto, 'Vigente', v_asiento)
        RETURNING id INTO v_documento;

        INSERT INTO lineadocumentocxc (documento_id, descripcion, cantidad, precio_unitario, porcentaje_impuesto, centro_costo_id, cuenta_contable_id)
        SELECT v_documento, e->>0, (e->>1)::NUMERIC, (e->>2)::NUMERIC, (e->>3)::NUMERIC,
               (SELECT id FROM centrocosto WHERE codigo = e->>4),
               (SELECT id FROM cuentacontable WHERE codigo = e->>5)
        FROM jsonb_array_elements(p_lineas) e;
    ELSE
        INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, estado, asiento_id)
        VALUES (p_numero, 'Factura', v_tercero, p_fecha, p_venc, v_monto, 'Vigente', v_asiento)
        RETURNING id INTO v_documento;

        INSERT INTO lineadocumentocxp (documento_id, descripcion, cantidad, precio_unitario, porcentaje_impuesto, centro_costo_id, cuenta_contable_id)
        SELECT v_documento, e->>0, (e->>1)::NUMERIC, (e->>2)::NUMERIC, (e->>3)::NUMERIC,
               (SELECT id FROM centrocosto WHERE codigo = e->>4),
               (SELECT id FROM cuentacontable WHERE codigo = e->>5)
        FROM jsonb_array_elements(p_lineas) e;
    END IF;

    CALL sp_registrar_auditoria(v_usuario, 'registrar_factura_' || LOWER(p_sigla), 'documento' || LOWER(p_sigla),
        format('Factura %s (id %s) registrada para %s %s, monto %s', p_numero, v_documento, LOWER(v_tipo), p_tercero, v_monto));
    RETURN v_documento;
END $$;

-- p_aplicaciones: [numero_factura, monto_aplicado]
CREATE FUNCTION pg_temp.demo_pago(p_sigla TEXT, p_tercero TEXT, p_fecha DATE, p_metodo TEXT, p_referencia TEXT,
                                  p_email TEXT, p_aplicaciones JSONB)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE
    v_es_cxc  BOOLEAN := (p_sigla = 'CxC');
    v_tipo    TEXT := CASE WHEN p_sigla = 'CxC' THEN 'Cliente' ELSE 'Proveedor' END;
    v_usuario INT;
    v_tercero INT;
    v_monto   NUMERIC;
    v_pago    INT;
BEGIN
    SELECT id INTO STRICT v_usuario FROM usuario WHERE email = p_email;
    SELECT id INTO STRICT v_tercero FROM contraparte WHERE tipo = v_tipo AND nombre = p_tercero;
    SELECT SUM((e->>1)::NUMERIC) INTO v_monto FROM jsonb_array_elements(p_aplicaciones) e;

    IF v_es_cxc THEN
        INSERT INTO recibopagocliente (cliente_id, fecha, monto_total, metodo_pago, referencia_bancaria)
        VALUES (v_tercero, p_fecha, v_monto, p_metodo, p_referencia)
        RETURNING id INTO v_pago;

        INSERT INTO aplicacionpagocliente (recibo_pago_id, documento_id, monto_aplicado)
        SELECT v_pago,
               (SELECT id FROM documentocxc WHERE numero = e->>0 AND cliente_id = v_tercero),
               (e->>1)::NUMERIC
        FROM jsonb_array_elements(p_aplicaciones) e;

        CALL sp_registrar_auditoria(v_usuario, 'registrar_pago_cxc', 'recibopagocliente',
            format('Recibo %s (cliente %s) registrado, monto %s', v_pago, p_tercero, v_monto));
    ELSE
        INSERT INTO pagoproveedorcabecera (proveedor_id, fecha, monto_total, metodo_pago, referencia_bancaria)
        VALUES (v_tercero, p_fecha, v_monto, p_metodo, p_referencia)
        RETURNING id INTO v_pago;

        INSERT INTO aplicacionpagoproveedor (pago_cabecera_id, documento_id, monto_aplicado)
        SELECT v_pago,
               (SELECT id FROM documentocxp WHERE numero = e->>0 AND proveedor_id = v_tercero),
               (e->>1)::NUMERIC
        FROM jsonb_array_elements(p_aplicaciones) e;

        CALL sp_registrar_auditoria(v_usuario, 'registrar_pago_cxp', 'pagoproveedorcabecera',
            format('Pago %s (proveedor %s) registrado, monto %s', v_pago, p_tercero, v_monto));
    END IF;
    RETURN v_pago;
END $$;

-- 5. Asientos manuales: Agosto 2026 -------------------------------------------------

SELECT pg_temp.demo_asiento('AS-2608-01', '2026-08-01', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,250000,0],["3.1.01",null,0,250000]]');
SELECT pg_temp.demo_asiento('AS-2608-02', '2026-08-02', 'contador@delta.com.gt', 'Confirmado',
    '[["1.2.01",null,18500,0],["1.1.02",null,0,18500]]');
SELECT pg_temp.demo_asiento('AS-2608-03', '2026-08-03', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.01",null,11200,0],["4.1.01",null,0,10000],["2.1.02",null,0,1200]]');
SELECT pg_temp.demo_asiento('AS-2608-04', '2026-08-05', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.03","ADM",8000,0],["1.1.02",null,0,8000]]');
SELECT pg_temp.demo_asiento('AS-2608-05', '2026-08-07', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,9000,0],["1.1.01",null,0,9000]]');
SELECT pg_temp.demo_asiento('AS-2608-06', '2026-08-10', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.04",null,15000,0],["1.1.02",null,0,15000]]');
SELECT pg_temp.demo_asiento('AS-2608-07', '2026-08-15', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.02","ADM",1350,0],["5.1.02","OPS",1000,0],["1.1.02",null,0,2350]]');
SELECT pg_temp.demo_asiento('AS-2608-08', '2026-08-18', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.01",null,5600,0],["4.1.02",null,0,5000],["2.1.02",null,0,600]]');
SELECT pg_temp.demo_asiento('AS-2608-09', '2026-08-20', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.04","ADM",400,0],["5.1.04","FIN",240,0],["1.1.01",null,0,640]]');
SELECT pg_temp.demo_asiento('AS-2608-10', '2026-08-25', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.01","ADM",10000,0],["5.1.01","VTA",10000,0],["5.1.01","OPS",6000,0],["5.1.01","TI",4000,0],["5.1.01","FIN",2000,0],["1.1.02",null,0,32000]]');
SELECT pg_temp.demo_asiento('AS-2608-11', '2026-08-28', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,5000,0],["1.1.01",null,0,5000]]');
SELECT pg_temp.demo_asiento('AS-2608-12', '2026-08-30', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.02","TI",1800,0],["1.1.01",null,0,1800]]');

-- 6. Asientos manuales: Octubre 2026 ------------------------------------------------

SELECT pg_temp.demo_asiento('AS-2610-01', '2026-10-01', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.03","ADM",8000,0],["1.1.02",null,0,8000]]');
SELECT pg_temp.demo_asiento('AS-2610-02', '2026-10-02', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.01",null,8960,0],["4.1.01",null,0,8000],["2.1.02",null,0,960]]');
SELECT pg_temp.demo_asiento('AS-2610-03', '2026-10-03', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,8000,0],["1.1.01",null,0,8000]]');
SELECT pg_temp.demo_asiento('AS-2610-04', '2026-10-05', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.02","ADM",1480,0],["5.1.02","TI",1000,0],["1.1.02",null,0,2480]]');
SELECT pg_temp.demo_asiento('AS-2610-05', '2026-10-07', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.04","FIN",520,0],["1.1.01",null,0,520]]');
SELECT pg_temp.demo_asiento('AS-2610-06', '2026-10-09', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.04",null,22000,0],["1.1.02",null,0,22000]]');
SELECT pg_temp.demo_asiento('AS-2610-07', '2026-10-12', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,6720,0],["4.1.02",null,0,6000],["2.1.02",null,0,720]]');
SELECT pg_temp.demo_asiento('AS-2610-08', '2026-10-15', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.01","ADM",10000,0],["5.1.01","VTA",10000,0],["5.1.01","OPS",6000,0],["5.1.01","TI",4000,0],["5.1.01","FIN",2000,0],["1.1.02",null,0,32000]]');
SELECT pg_temp.demo_asiento('AS-2610-09', '2026-10-16', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.01",null,5600,0],["4.1.01",null,0,5000],["2.1.02",null,0,600]]');
SELECT pg_temp.demo_asiento('AS-2610-10', '2026-10-20', 'contador@delta.com.gt', 'Confirmado',
    '[["1.1.02",null,5000,0],["1.1.01",null,0,5000]]');
SELECT pg_temp.demo_asiento('AS-2610-11', '2026-10-22', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.02","OPS",3000,0],["1.1.02",null,0,3000]]');
SELECT pg_temp.demo_asiento('AS-2610-12', '2026-10-28', 'contador@delta.com.gt', 'Confirmado',
    '[["5.1.02","TI",1800,0],["1.1.02",null,0,1800]]');
SELECT pg_temp.demo_asiento('AS-2610-13', '2026-10-29', 'contador@delta.com.gt', 'Borrador',
    '[["5.1.04","ADM",300,0],["1.1.01",null,0,300]]');

DO $$
DECLARE
    v_asiento INT;
    v_admin   INT;
BEGIN
    SELECT id INTO STRICT v_asiento FROM asientocontable WHERE numero = 'AS-2610-11';
    SELECT id INTO STRICT v_admin FROM usuario WHERE email = 'admin@delta.com.gt';
    CALL sp_reversar_asiento(v_asiento, v_admin, 'Reversa de demostración');
END $$;

-- 7. Facturas de cuentas por cobrar -------------------------------------------------

SELECT pg_temp.demo_factura('CxC', 'F-1001', 'Distribuidora Los Pinos, S.A.', '2026-08-04', '2026-09-03', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote A",40,250,12,"VTA","4.1.01"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1002', 'Ferretería El Constructor, S.A.', '2026-08-12', '2026-09-11', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote B",25,480,12,"VTA","4.1.01"],["Instalación y puesta en marcha",1,1500,12,"VTA","4.1.02"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1003', 'Colegio Santa Lucía, S.A.', '2026-08-19', '2026-09-18', 'contador@delta.com.gt', '1.1.03',
    '[["Consultoría de sistemas (horas)",10,600,12,"VTA","4.1.02"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1004', 'Hotel Casa Antigua, S.A.', '2026-08-26', '2026-09-25', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote C",60,125,12,"VTA","4.1.01"],["Capacitación de personal",2,900,12,"VTA","4.1.02"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1005', 'Agroindustrias del Pacífico, S.A.', '2026-10-02', '2026-11-01', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote D",80,150,12,"VTA","4.1.01"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1006', 'Clínica Médica San Rafael, S.A.', '2026-10-06', '2026-11-05', 'contador@delta.com.gt', '1.1.03',
    '[["Mantenimiento mensual de equipo",1,4500,12,"VTA","4.1.02"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1007', 'Distribuidora Los Pinos, S.A.', '2026-10-14', '2026-11-13', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote E",30,250,12,"VTA","4.1.01"]]');
SELECT pg_temp.demo_factura('CxC', 'F-1008', 'Hotel Casa Antigua, S.A.', '2026-10-21', '2026-11-20', 'vendedor@delta.com.gt', '1.1.03',
    '[["Mercadería lote F",24,300,12,"VTA","4.1.01"],["Soporte técnico",1,2000,12,"VTA","4.1.02"]]');

-- 8. Facturas de cuentas por pagar --------------------------------------------------

SELECT pg_temp.demo_factura('CxP', 'P-2001', 'Papelería y Suministros Guatemala, S.A.', '2026-08-06', '2026-09-05', 'contador@delta.com.gt', '2.1.01',
    '[["Resmas de papel bond",100,32,12,"ADM","5.1.04"]]');
SELECT pg_temp.demo_factura('CxP', 'P-2002', 'Distribuidora Eléctrica Centroamericana, S.A.', '2026-08-14', '2026-09-13', 'contador@delta.com.gt', '2.1.01',
    '[["Material eléctrico para reventa",20,450,12,"OPS","1.1.04"]]');
SELECT pg_temp.demo_factura('CxP', 'P-2003', 'Servicios Informáticos Nova, S.A.', '2026-08-22', '2026-09-21', 'contador@delta.com.gt', '2.1.01',
    '[["Licencias de software",5,800,12,"TI","5.1.02"],["Soporte técnico anual",1,1200,12,"TI","5.1.02"]]');
SELECT pg_temp.demo_factura('CxP', 'P-2004', 'Comercial Agrícola Maya, S.A.', '2026-10-03', '2026-11-02', 'contador@delta.com.gt', '2.1.01',
    '[["Insumos agrícolas para reventa",50,120,12,"OPS","1.1.04"]]');
SELECT pg_temp.demo_factura('CxP', 'P-2005', 'Seguros y Fianzas Chapín, S.A.', '2026-10-10', '2026-11-09', 'contador@delta.com.gt', '2.1.01',
    '[["Póliza anual de seguro de local",1,5000,12,"FIN","5.1.02"]]');
SELECT pg_temp.demo_factura('CxP', 'P-2006', 'Papelería y Suministros Guatemala, S.A.', '2026-10-17', '2026-11-16', 'contador@delta.com.gt', '2.1.01',
    '[["Útiles de oficina",60,38,12,"ADM","5.1.04"],["Escritorios ejecutivos",2,1500,12,"ADM","1.2.01"]]');

-- 9. Pagos (completos, parciales y facturas sin pago) ---------------------------------

SELECT pg_temp.demo_pago('CxC', 'Distribuidora Los Pinos, S.A.', '2026-08-20', 'Transferencia', 'TRF-880145', 'contador@delta.com.gt',
    '[["F-1001",11200]]');
SELECT pg_temp.demo_pago('CxC', 'Ferretería El Constructor, S.A.', '2026-08-28', 'Cheque', 'CHQ-004512', 'contador@delta.com.gt',
    '[["F-1002",8000]]');
SELECT pg_temp.demo_pago('CxC', 'Hotel Casa Antigua, S.A.', '2026-09-10', 'Transferencia', 'TRF-902233', 'contador@delta.com.gt',
    '[["F-1004",10416]]');
SELECT pg_temp.demo_pago('CxC', 'Agroindustrias del Pacífico, S.A.', '2026-10-04', 'Cheque', 'CHQ-004987', 'contador@delta.com.gt',
    '[["F-1005",6000]]');
SELECT pg_temp.demo_pago('CxC', 'Distribuidora Los Pinos, S.A.', '2026-10-18', 'Efectivo', NULL, 'vendedor@delta.com.gt',
    '[["F-1007",8400]]');

SELECT pg_temp.demo_pago('CxP', 'Papelería y Suministros Guatemala, S.A.', '2026-08-30', 'Transferencia', 'TRF-771020', 'contador@delta.com.gt',
    '[["P-2001",3584]]');
SELECT pg_temp.demo_pago('CxP', 'Distribuidora Eléctrica Centroamericana, S.A.', '2026-08-31', 'Cheque', 'CHQ-005101', 'contador@delta.com.gt',
    '[["P-2002",5000]]');
SELECT pg_temp.demo_pago('CxP', 'Comercial Agrícola Maya, S.A.', '2026-10-04', 'Transferencia', 'TRF-771544', 'contador@delta.com.gt',
    '[["P-2004",6720]]');
SELECT pg_temp.demo_pago('CxP', 'Seguros y Fianzas Chapín, S.A.', '2026-10-11', 'Cheque', 'CHQ-005230', 'contador@delta.com.gt',
    '[["P-2005",2000]]');

-- 10. Cierre de Agosto 2026 (último: todos sus datos ya están insertados) ---------------

DO $$
DECLARE
    v_periodo  INT;
    v_contador INT;
BEGIN
    SELECT id INTO STRICT v_periodo FROM periodocontable WHERE nombre = 'Agosto 2026';
    SELECT id INTO STRICT v_contador FROM usuario WHERE email = 'contador@delta.com.gt';
    CALL sp_cerrar_periodo(v_periodo, v_contador);
END $$;

DROP FUNCTION pg_temp.demo_pago(TEXT, TEXT, DATE, TEXT, TEXT, TEXT, JSONB);
DROP FUNCTION pg_temp.demo_factura(TEXT, TEXT, TEXT, DATE, DATE, TEXT, TEXT, JSONB);
DROP FUNCTION pg_temp.demo_asiento(TEXT, DATE, TEXT, TEXT, JSONB);

-- 11. Verificación -------------------------------------------------------------------

SELECT 'usuario' AS tabla, COUNT(*) AS filas FROM usuario
UNION ALL SELECT 'cuentacontable', COUNT(*) FROM cuentacontable
UNION ALL SELECT 'centrocosto', COUNT(*) FROM centrocosto
UNION ALL SELECT 'periodocontable', COUNT(*) FROM periodocontable
UNION ALL SELECT 'contraparte', COUNT(*) FROM contraparte
UNION ALL SELECT 'asientocontable', COUNT(*) FROM asientocontable
UNION ALL SELECT 'lineaasiento', COUNT(*) FROM lineaasiento
UNION ALL SELECT 'documentocxc', COUNT(*) FROM documentocxc
UNION ALL SELECT 'lineadocumentocxc', COUNT(*) FROM lineadocumentocxc
UNION ALL SELECT 'recibopagocliente', COUNT(*) FROM recibopagocliente
UNION ALL SELECT 'aplicacionpagocliente', COUNT(*) FROM aplicacionpagocliente
UNION ALL SELECT 'documentocxp', COUNT(*) FROM documentocxp
UNION ALL SELECT 'lineadocumentocxp', COUNT(*) FROM lineadocumentocxp
UNION ALL SELECT 'pagoproveedorcabecera', COUNT(*) FROM pagoproveedorcabecera
UNION ALL SELECT 'aplicacionpagoproveedor', COUNT(*) FROM aplicacionpagoproveedor
UNION ALL SELECT 'saldocuentaperiodo', COUNT(*) FROM saldocuentaperiodo
UNION ALL SELECT 'bitacoraauditoria', COUNT(*) FROM bitacoraauditoria;

SELECT SUM(total_debito) AS total_debito, SUM(total_credito) AS total_credito,
       SUM(total_debito) = SUM(total_credito) AS balanceado
FROM vw_balance_saldos;

SELECT nombre, estado, cierres FROM periodocontable ORDER BY fecha_inicio;

SELECT 'cxc' AS tipo, d.numero, d.monto_total, s.saldo_pendiente
FROM documentocxc d JOIN vw_saldodocumentocxc s ON s.documento_id = d.id
UNION ALL
SELECT 'cxp', d.numero, d.monto_total, s.saldo_pendiente
FROM documentocxp d JOIN vw_saldodocumentocxp s ON s.documento_id = d.id
ORDER BY 1, 2;
