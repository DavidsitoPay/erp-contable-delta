-- =====================================================================
-- Delta ERP Contable — 01_tables.sql
-- Basado en: Diagrama_Modelo_Relacional_de_Base_de_Datos... (v2, 12/09/2026)
-- Incorpora la corrección oficial: sin saldos precalculados editables,
-- SaldoCuentaPeriodo inmutable, trazabilidad de auditoría vía procedimiento.
-- =====================================================================

-- 1. Seguridad y Auditoría -------------------------------------------------

CREATE TABLE perfil (
    id      SERIAL PRIMARY KEY,
    nombre  VARCHAR(100) NOT NULL
);

CREATE TABLE usuario (
    id             SERIAL PRIMARY KEY,
    nombre         VARCHAR(150) NOT NULL,
    email          VARCHAR(150) NOT NULL UNIQUE,
    password_hash  VARCHAR(255) NOT NULL, -- función de derivación con salt (ver docs/architecture.md)
    perfil_id      INT NOT NULL REFERENCES perfil(id),
    activo         BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE bitacoraauditoria (
    id              SERIAL PRIMARY KEY,
    usuario_id      INT NOT NULL REFERENCES usuario(id),
    fecha           DATE NOT NULL DEFAULT CURRENT_DATE,
    accion          VARCHAR(100) NOT NULL,
    tabla_afectada  VARCHAR(100) NOT NULL,
    detalle         TEXT
);
-- Se puebla EXCLUSIVAMENTE mediante sp_registrar_auditoria (04_procedures.sql),
-- invocado por la API dentro de la misma transacción de negocio. Ver RN-08.
-- Un trigger (03_triggers.sql) impide su modificación o borrado posterior.

-- 2. Catálogo y Configuración General --------------------------------------

CREATE TABLE moneda (
    id      SERIAL PRIMARY KEY,
    codigo  VARCHAR(10) NOT NULL,
    nombre  VARCHAR(100) NOT NULL
);

CREATE TABLE historialtipocambio (
    id         SERIAL PRIMARY KEY,
    moneda_id  INT NOT NULL REFERENCES moneda(id),
    fecha      DATE NOT NULL,
    tasa       DECIMAL(12,6) NOT NULL
);

CREATE TABLE periodocontable (
    id            SERIAL PRIMARY KEY,
    nombre        VARCHAR(100) NOT NULL,
    fecha_inicio  DATE NOT NULL,
    fecha_fin     DATE NOT NULL,
    estado        VARCHAR(20) NOT NULL DEFAULT 'Abierto' -- Abierto | Cerrado
);

CREATE TABLE contraparte (
    id         SERIAL PRIMARY KEY,
    tipo       VARCHAR(20) NOT NULL,   -- Cliente | Proveedor
    nombre     VARCHAR(200) NOT NULL,
    nit        VARCHAR(30),
    direccion  VARCHAR(255)
);

CREATE TABLE cuentacontable (
    id               SERIAL PRIMARY KEY,
    codigo           VARCHAR(20) NOT NULL UNIQUE,
    nombre           VARCHAR(150) NOT NULL,
    tipo             VARCHAR(30) NOT NULL,   -- Activo|Pasivo|Capital|Ingreso|Gasto
    naturaleza       VARCHAR(20) NOT NULL,   -- Deudora|Acreedora
    cuenta_padre_id  INT REFERENCES cuentacontable(id),
    activa           BOOLEAN NOT NULL DEFAULT TRUE
);
-- Sin columna "saldo": se calcula en tiempo real (balance de saldos, ver 05_views.sql)
-- y se consolida de forma inmutable al cierre en saldocuentaperiodo.

CREATE TABLE centrocosto (
    id      SERIAL PRIMARY KEY,
    codigo  VARCHAR(20) NOT NULL UNIQUE,
    nombre  VARCHAR(150) NOT NULL,
    activo  BOOLEAN NOT NULL DEFAULT TRUE
);

-- 3. Transacciones y Asientos Contables -------------------------------------

CREATE TABLE asientocontable (
    id                    SERIAL PRIMARY KEY,
    numero                VARCHAR(30) NOT NULL,
    fecha                 DATE NOT NULL,
    periodo_id            INT NOT NULL REFERENCES periodocontable(id),
    monto                 DECIMAL(14,2) NOT NULL,
    estado                VARCHAR(20) NOT NULL DEFAULT 'Borrador', -- Borrador|Confirmado|Anulado
    usuario_id            INT NOT NULL REFERENCES usuario(id),
    tipo_cambio_aplicado  DECIMAL(12,6)
);

CREATE TABLE lineaasiento (
    id               SERIAL PRIMARY KEY,
    asiento_id       INT NOT NULL REFERENCES asientocontable(id),
    cuenta_id        INT NOT NULL REFERENCES cuentacontable(id),
    centro_costo_id  INT REFERENCES centrocosto(id),
    debito           DECIMAL(14,2) NOT NULL DEFAULT 0,
    credito          DECIMAL(14,2) NOT NULL DEFAULT 0
);

CREATE TABLE plantillaasiento (
    id           SERIAL PRIMARY KEY,
    nombre       VARCHAR(150) NOT NULL,
    descripcion  TEXT
);

CREATE TABLE lineaplantillaasiento (
    id               SERIAL PRIMARY KEY,
    plantilla_id     INT NOT NULL REFERENCES plantillaasiento(id),
    cuenta_id        INT NOT NULL REFERENCES cuentacontable(id),
    centro_costo_id  INT REFERENCES centrocosto(id),
    distribucion     VARCHAR(20) NOT NULL -- Monto | Porcentaje
);

-- 4. Cuentas por Cobrar (CxC) -----------------------------------------------

CREATE TABLE documentocxc (
    id                    SERIAL PRIMARY KEY,
    numero                VARCHAR(30) NOT NULL,
    tipo_documento        VARCHAR(20) NOT NULL, -- Factura|NotaCredito|NotaDebito
    cliente_id            INT NOT NULL REFERENCES contraparte(id),
    fecha                 DATE NOT NULL,
    fecha_vencimiento     DATE NOT NULL,
    monto_total           DECIMAL(14,2) NOT NULL,
    tipo_cambio_aplicado  DECIMAL(12,6),
    estado                VARCHAR(20) NOT NULL DEFAULT 'Vigente', -- Vigente | Anulado
    asiento_id            INT REFERENCES asientocontable(id)
);
-- Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxc (05_views.sql).

CREATE TABLE lineadocumentocxc (
    id                   SERIAL PRIMARY KEY,
    documento_id         INT NOT NULL REFERENCES documentocxc(id),
    descripcion          VARCHAR(255),
    cantidad             DECIMAL(12,2) NOT NULL,
    precio_unitario      DECIMAL(14,2) NOT NULL,
    porcentaje_impuesto  DECIMAL(5,2) DEFAULT 0,
    centro_costo_id      INT REFERENCES centrocosto(id),
    cuenta_contable_id   INT NOT NULL REFERENCES cuentacontable(id)
);

CREATE TABLE recibopagocliente (
    id                   SERIAL PRIMARY KEY,
    cliente_id           INT NOT NULL REFERENCES contraparte(id),
    fecha                DATE NOT NULL,
    monto_total          DECIMAL(14,2) NOT NULL,
    metodo_pago          VARCHAR(30) NOT NULL,
    referencia_bancaria  VARCHAR(100),
    cuenta_bancaria_id   INT -- FK -> cuentabancaria(id), agregada tras crear esa tabla
);

CREATE TABLE aplicacionpagocliente (
    id              SERIAL PRIMARY KEY,
    recibo_pago_id  INT NOT NULL REFERENCES recibopagocliente(id),
    documento_id    INT NOT NULL REFERENCES documentocxc(id),
    monto_aplicado  DECIMAL(14,2) NOT NULL
);
-- RN-05: monto_aplicado no puede exceder el saldo pendiente del documento — trigger.

-- 5. Cuentas por Pagar (CxP) -------------------------------------------------

CREATE TABLE documentocxp (
    id                    SERIAL PRIMARY KEY,
    numero                VARCHAR(30) NOT NULL,
    tipo_documento        VARCHAR(20) NOT NULL,
    proveedor_id          INT NOT NULL REFERENCES contraparte(id),
    fecha                 DATE NOT NULL,
    fecha_vencimiento     DATE NOT NULL,
    monto_total           DECIMAL(14,2) NOT NULL,
    tipo_cambio_aplicado  DECIMAL(12,6),
    estado                VARCHAR(20) NOT NULL DEFAULT 'Vigente',
    asiento_id            INT REFERENCES asientocontable(id)
);
-- Sin columna "saldo_pendiente": se calcula en vw_saldodocumentocxp (05_views.sql).

CREATE TABLE lineadocumentocxp (
    id                   SERIAL PRIMARY KEY,
    documento_id         INT NOT NULL REFERENCES documentocxp(id),
    descripcion          VARCHAR(255),
    cantidad             DECIMAL(12,2) NOT NULL,
    precio_unitario      DECIMAL(14,2) NOT NULL,
    porcentaje_impuesto  DECIMAL(5,2) DEFAULT 0,
    centro_costo_id      INT REFERENCES centrocosto(id),
    cuenta_contable_id   INT NOT NULL REFERENCES cuentacontable(id)
);

CREATE TABLE pagoproveedorcabecera (
    id                   SERIAL PRIMARY KEY,
    proveedor_id         INT NOT NULL REFERENCES contraparte(id),
    fecha                DATE NOT NULL,
    monto_total          DECIMAL(14,2) NOT NULL,
    metodo_pago          VARCHAR(30) NOT NULL,
    referencia_bancaria  VARCHAR(100),
    cuenta_bancaria_id   INT
);

CREATE TABLE aplicacionpagoproveedor (
    id                 SERIAL PRIMARY KEY,
    pago_cabecera_id   INT NOT NULL REFERENCES pagoproveedorcabecera(id),
    documento_id       INT NOT NULL REFERENCES documentocxp(id),
    monto_aplicado     DECIMAL(14,2) NOT NULL
);
-- RN-05: monto_aplicado no puede exceder el saldo pendiente del documento — trigger.

-- 6. Tesorería ----------------------------------------------------------------

CREATE TABLE cuentabancaria (
    id      SERIAL PRIMARY KEY,
    banco   VARCHAR(100) NOT NULL,
    numero  VARCHAR(50) NOT NULL,
    tipo    VARCHAR(30) NOT NULL
    -- Sin columna "saldo": se calcula en vw_saldocuentabancaria (05_views.sql).
);

ALTER TABLE recibopagocliente
    ADD CONSTRAINT fk_recibopagocliente_cuenta FOREIGN KEY (cuenta_bancaria_id) REFERENCES cuentabancaria(id);
ALTER TABLE pagoproveedorcabecera
    ADD CONSTRAINT fk_pagoproveedorcabecera_cuenta FOREIGN KEY (cuenta_bancaria_id) REFERENCES cuentabancaria(id);

CREATE TABLE movimientotesoreria (
    id                  SERIAL PRIMARY KEY,
    cuenta_bancaria_id  INT NOT NULL REFERENCES cuentabancaria(id),
    fecha               DATE NOT NULL,
    tipo                VARCHAR(30) NOT NULL, -- Ingreso | Egreso
    monto               DECIMAL(14,2) NOT NULL
);

CREATE TABLE conciliacionbancaria (
    id                  SERIAL PRIMARY KEY,
    cuenta_bancaria_id  INT NOT NULL REFERENCES cuentabancaria(id),
    periodo_id          INT NOT NULL REFERENCES periodocontable(id),
    fecha               DATE NOT NULL,
    estado              VARCHAR(20) NOT NULL DEFAULT 'Pendiente' -- Pendiente | Conciliado
);

CREATE TABLE detalleconciliacion (
    id               SERIAL PRIMARY KEY,
    conciliacion_id  INT NOT NULL REFERENCES conciliacionbancaria(id),
    movimiento_id    INT NOT NULL REFERENCES movimientotesoreria(id)
);

-- 7. Consolidación de Saldos --------------------------------------------------

CREATE TABLE saldocuentaperiodo (
    id             SERIAL PRIMARY KEY,
    periodo_id     INT NOT NULL REFERENCES periodocontable(id),
    cuenta_id      INT NOT NULL REFERENCES cuentacontable(id),
    total_debito   DECIMAL(14,2) NOT NULL,
    total_credito  DECIMAL(14,2) NOT NULL,
    saldo_final    DECIMAL(14,2) NOT NULL,
    UNIQUE (periodo_id, cuenta_id)
);
-- Fotografía inalterable del saldo de cada cuenta contable al cierre del periodo.
-- Se escribe UNA sola vez desde sp_cerrar_periodo (04_procedures.sql).
-- Un trigger (03_triggers.sql) impide UPDATE/DELETE posterior.
-- El saldo del periodo EN CURSO se sigue calculando en tiempo real (05_views.sql),
-- esta tabla solo aplica a periodos ya cerrados.

-- =====================================================================
-- Fin de 01_tables.sql — ver 03_triggers.sql, 04_procedures.sql y 05_views.sql
-- =====================================================================
