-- =====================================================================
-- 14_rol_api.sql
-- Rol de runtime delta_api con mínimo privilegio. Re-ejecutable: converge
-- a la matriz de permisos. El login y la contraseña se fijan fuera del
-- repositorio (ALTER ROLE delta_api WITH LOGIN PASSWORD ...).
--
-- Convención: toda migración posterior que cree una tabla, vista o rutina
-- usada por la API debe declarar aquí mismo su GRANT a delta_api; sin él la
-- API recibe 42501. Solo las secuencias nuevas se conceden por defecto.
-- =====================================================================


-- 1. Rol

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'delta_api') THEN
        CREATE ROLE delta_api NOLOGIN;
    END IF;
END
$$;

ALTER ROLE delta_api SET search_path = public;


-- 2. Endurecimiento de PUBLIC

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE EXECUTE ON ALL ROUTINES IN SCHEMA public FROM PUBLIC;
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON ROUTINES FROM PUBLIC;

DO $$
BEGIN
    EXECUTE format('REVOKE TEMPORARY ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO delta_api', current_database());
END
$$;


-- 3. Reinicio de los privilegios de delta_api

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM delta_api;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM delta_api;
REVOKE ALL ON ALL ROUTINES IN SCHEMA public FROM delta_api;

GRANT USAGE ON SCHEMA public TO delta_api;


-- 4. Tablas

GRANT SELECT ON perfil, usuario, moneda TO delta_api;

GRANT SELECT, INSERT, UPDATE ON
    periodocontable,
    contraparte,
    cuentacontable,
    centrocosto,
    asientocontable,
    documentocxc,
    documentocxp,
    cuentabancaria,
    conciliacionbancaria,
    impuesto
TO delta_api;

GRANT SELECT, INSERT ON
    lineaasiento,
    lineadocumentocxc,
    recibopagocliente,
    aplicacionpagocliente,
    lineadocumentocxp,
    pagoproveedorcabecera,
    aplicacionpagoproveedor,
    movimientotesoreria
TO delta_api;

GRANT SELECT, INSERT, DELETE ON detalleconciliacion TO delta_api;
GRANT SELECT, UPDATE ON configuracionfiscal TO delta_api;
GRANT INSERT ON bitacoraauditoria, saldocuentaperiodo TO delta_api;


-- 5. Vistas

GRANT SELECT ON
    vw_balance_saldos,
    vw_saldodocumentocxc,
    vw_saldodocumentocxp,
    vw_saldocuentabancaria,
    vw_conciliacion_resumen,
    vw_saldocuentaperiodo_vigente
TO delta_api;


-- 6. Secuencias

GRANT USAGE ON SEQUENCE
    periodocontable_id_seq,
    contraparte_id_seq,
    cuentacontable_id_seq,
    centrocosto_id_seq,
    asientocontable_id_seq,
    lineaasiento_id_seq,
    documentocxc_id_seq,
    lineadocumentocxc_id_seq,
    recibopagocliente_id_seq,
    aplicacionpagocliente_id_seq,
    documentocxp_id_seq,
    lineadocumentocxp_id_seq,
    pagoproveedorcabecera_id_seq,
    aplicacionpagoproveedor_id_seq,
    cuentabancaria_id_seq,
    movimientotesoreria_id_seq,
    conciliacionbancaria_id_seq,
    detalleconciliacion_id_seq,
    impuesto_id_seq,
    bitacoraauditoria_id_seq,
    saldocuentaperiodo_id_seq
TO delta_api;

ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE ON SEQUENCES TO delta_api;


-- 7. Rutinas

GRANT EXECUTE ON FUNCTION fn_reporte_saldos(integer, boolean) TO delta_api;
GRANT EXECUTE ON FUNCTION fn_usuario_tiene_perfil_autorizado(integer, character varying[]) TO delta_api;
GRANT EXECUTE ON FUNCTION fn_perfil_contador() TO delta_api;
GRANT EXECUTE ON FUNCTION fn_perfil_administrador_sistema() TO delta_api;
GRANT EXECUTE ON PROCEDURE sp_registrar_auditoria(integer, character varying, character varying, text) TO delta_api;
GRANT EXECUTE ON PROCEDURE sp_cerrar_periodo(integer, integer) TO delta_api;
GRANT EXECUTE ON PROCEDURE sp_reabrir_periodo(integer, integer) TO delta_api;
GRANT EXECUTE ON PROCEDURE sp_reversar_asiento(integer, integer, text) TO delta_api;
GRANT EXECUTE ON PROCEDURE sp_finalizar_conciliacion(integer, integer) TO delta_api;
