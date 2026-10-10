# Diccionario de Datos — Delta ERP Contable

> Actualizado según el documento oficial v2 (12/09/2026).

## 1. Seguridad y Auditoría

### Perfil
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| nombre | VARCHAR(100) | No | | Nombre del perfil |

### Usuario
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| nombre | VARCHAR(150) | No | | Nombre completo |
| email | VARCHAR(150) | No | | Correo, único |
| password_hash | VARCHAR(255) | No | | Contraseña cifrada (derivación con salt) |
| perfil_id | INT | No | FK -> Perfil(id) | Perfil asignado |
| activo | BOOLEAN | No | | Usuario activo (sin eliminación física — ver triggers) |

### BitacoraAuditoria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| usuario_id | INT | No | FK -> Usuario(id) | Usuario que realizó la acción |
| fecha | DATE | No | | Fecha de la acción |
| accion | VARCHAR(100) | No | | crear / editar / eliminar |
| tabla_afectada | VARCHAR(100) | No | | Tabla afectada |
| detalle | TEXT | Sí | | Detalle adicional |

Se puebla exclusivamente vía `sp_registrar_auditoria`, invocado por la API. Inmutable
(trigger bloquea UPDATE/DELETE).

## 2. Catálogo y Configuración General

### HistorialTipoCambio / PeriodoContable
Sin cambios respecto a la versión anterior — ver estructura completa en el documento
oficial (05/09, sección 2).

### Moneda
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR | No | UQ (`uq_moneda_codigo`) | Código de la moneda |
| nombre | VARCHAR | No | | Nombre |
| es_funcional | BOOLEAN | No | | Moneda funcional; índice único parcial `ux_moneda_funcional` (solo una). Sembrada: GTQ |
| activa | BOOLEAN | No | | Por defecto TRUE |

### Contraparte
Además de sus campos previos, tiene datos fiscales:

| Campo | Tipo | Nulo | Descripción |
|---|---|---|---|
| regimen_iva | VARCHAR(25) | No | GENERAL (defecto) / PEQUENO_CONTRIBUYENTE / EXENTO |
| regimen_isr | VARCHAR(20) | No | UTILIDADES (defecto) / SIMPLIFICADO |
| es_residente | BOOLEAN | No | TRUE por defecto |
| es_agente_retencion_iva | BOOLEAN | No | FALSE por defecto |

El NIT se guarda normalizado (`cuerpo-DV`, o `CF` en clientes) y se valida en la API (RN-13).

### Impuesto
Catálogo de impuestos con vigencia (RN-07). Una versión nueva es una fila nueva con el mismo `codigo`.

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR(30) | No | UQ con `vigente_desde` | Código estable entre versiones |
| nombre | VARCHAR(100) | No | | Nombre |
| tipo | VARCHAR(25) | No | | IVA_GENERAL / EXENTO / NO_AFECTO / PEQUENO_CONTRIBUYENTE / LEGADO (LEGADO solo lo crea la migración) |
| tasa | DECIMAL(7,4) | No | | 0 para exento y no afecto; mayor que 0 y hasta 100 para IVA general y pequeño contribuyente |
| aplica_a | VARCHAR(10) | No | | VENTAS / COMPRAS / AMBOS (pequeño contribuyente solo COMPRAS) |
| genera_credito | BOOLEAN | No | | Solo puede ser TRUE en IVA_GENERAL |
| articulo_legal | VARCHAR(200) | No | | Referencia legal |
| nota | VARCHAR(300) | Sí | | Observación; los sembrados indican «[VERIFICAR con asesor] vigencia referencial» |
| vigente_desde | DATE | No | | Inicio de vigencia |
| vigente_hasta | DATE | Sí | | Fin de vigencia (>= inicio) |
| activo | BOOLEAN | No | | Baja lógica |

Sembrados: `IVA_GENERAL`, `IVA_GENERAL_SIN_CREDITO` (compras), `EXENTO_EXPORTACION`, `NO_AFECTO` y `PEQUENO_CONTRIBUYENTE` (compras), más un impuesto `LEGADO_<tasa>` inactivo por cada tasa distinta de 0 y 12 hallada en líneas previas. Triggers: `trg_impuesto_sin_traslape` (sin vigencias traslapadas del mismo código), `trg_impuesto_inmutable_si_usado` (con líneas asociadas no se cambian código, tipo, tasa, ámbito, crédito ni inicio, ni se cierra antes de la fecha del último documento) y `trg_prevenir_eliminacion_impuesto`. Los errores de negocio de estos triggers usan el código `55000` (409 en la API).

### ConfiguracionFiscal
Fila única (`CHECK (id = 1)`); `trg_configuracionfiscal_no_eliminable` impide borrarla.

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | INT | No | PK | Siempre 1 |
| nit_empresa | VARCHAR(30) | Sí | | NIT de la empresa |
| nombre_legal | VARCHAR(200) | Sí | | Nombre legal |
| moneda_funcional_id | INT | No | FK -> Moneda(id) | GTQ; solo lectura en la API |
| regimen_isr | VARCHAR(20) | No | | UTILIDADES (defecto) / SIMPLIFICADO |
| agente_retencion_iva | BOOLEAN | No | | `CHECK (agente_retencion_iva = (tipo_agente_iva IS NOT NULL))` |
| tipo_agente_iva | VARCHAR(30) | Sí | | EXPORTADOR_HABITUAL / SECTOR_PUBLICO / TARJETA_CREDITO / COMBUSTIBLE / CONTRIBUYENTE_ESPECIAL |
| cuenta_iva_debito_id | INT | Sí | FK -> CuentaContable(id) | IVA débito fiscal (Pasivo, Acreedora) |
| cuenta_iva_credito_id | INT | Sí | FK -> CuentaContable(id) | IVA crédito fiscal (Activo, Deudora); distinta de la de débito |
| actualizado_en | TIMESTAMPTZ | No | | Última actualización |
| actualizado_por | INT | Sí | FK -> Usuario(id) | Quién actualizó |

Las cuentas de IVA quedan nulas tras la migración; las configura el Administrador. Régimen de ISR y agente de retención se guardan, pero las retenciones no están implementadas.

### CuentaContable
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR(20) | No | | Código dentro del catálogo |
| nombre | VARCHAR(150) | No | | Nombre de la cuenta |
| tipo | VARCHAR(30) | No | | Activo/Pasivo/Capital/Ingreso/Gasto |
| naturaleza | VARCHAR(20) | No | | Deudora / Acreedora |
| cuenta_padre_id | INT | Sí | FK -> CuentaContable(id) | Jerarquía |
| activa | BOOLEAN | No | | Sin eliminación física — ver triggers |

**Sin columna `saldo`.** Se calcula en `vw_balance_saldos` (periodo en curso /
histórico) y se consolida de forma inmutable en `SaldoCuentaPeriodo` al cerrar cada periodo.

El trigger `trg_validar_padre_sin_saldo` (BEFORE INSERT o UPDATE de `cuenta_padre_id`) impide asignar como padre una cuenta con saldo propio contabilizado distinto de 0 o con líneas en asientos `Borrador` (código `55000`).

### CentroCosto
Sin cambios.

## 3. Transacciones y Asientos Contables

### AsientoContable / LineaAsiento / PlantillaAsiento / LineaPlantillaAsiento
Sin cambios estructurales respecto a la versión anterior.

Estados de `AsientoContable`: `Borrador` (no contabilizado), `Confirmado` y `Anulado`
(confirmado y ya revertido por su asiento de reversa). `Confirmado` y `Anulado`
permanecen contabilizados.

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| reversa_de_id | INT | Sí | FK -> AsientoContable(id) | En un asiento de reversa, el asiento original que revierte; NULL en los demás. Índice único parcial (`ux_asientocontable_reversa_de_id`, `WHERE reversa_de_id IS NOT NULL`): un asiento tiene a lo sumo una reversa |

La entidad EF `AsientoContable` mapea `reversa_de_id` como `ReversaDeId`; la API lo fuerza a NULL al registrar un asiento.

### sp_reversar_asiento(p_asiento_id INT, p_usuario_id INT, p_motivo TEXT)
Definido en `database/12_reversa_asientos.sql` (RN-03). Verifica en este orden: perfil Administrador del sistema (si no, excepción por defecto `P0001`); motivo no vacío (`22023`); que el asiento exista (`P0002`); que esté `Confirmado`, que no sea una reversa, que su periodo esté `Abierto` y que no tenga vínculo con `documentocxc`, `documentocxp` ni `movimientotesoreria` (`55000`). Entonces inserta la reversa `Confirmado` en el mismo periodo (número `REV-` + original truncado a 30, fecha `GREATEST(fecha original, LEAST(CURRENT_DATE, fecha_fin del periodo))`, `reversa_de_id` = original) con las líneas invertidas, pasa el original a `Anulado` y registra la auditoría `reversar_asiento` con el motivo. La API traduce `P0001` a `403`, `P0002` a `404` y `55000` a `409`. La firma de dos argumentos no existe.

## 4. Cuentas por Cobrar (CxC)

### DocumentoCxC
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| numero | VARCHAR(30) | No | | Número de documento |
| tipo_documento | VARCHAR(20) | No | | Factura/NotaCredito/NotaDebito |
| cliente_id | INT | No | FK -> Contraparte(id) | Cliente |
| fecha | DATE | No | | Fecha de emisión |
| **fecha_vencimiento** | DATE | No | | **Nuevo** — fecha en que vence el cobro, según condiciones de crédito |
| monto_total | DECIMAL(14,2) | No | | Total en GTQ con IVA incluido; `CHECK (monto_total = monto_base + monto_iva)` |
| monto_base | DECIMAL(14,2) | No | | Suma de la base de las líneas |
| monto_iva | DECIMAL(14,2) | No | | Suma del IVA de las líneas |
| calculo_legado | BOOLEAN | No | | TRUE en documentos previos a la configuración fiscal (RN-16); FALSE en los nuevos |
| dte_uuid | UUID | Sí | | UUID de autorización del DTE; índice único parcial |
| dte_serie | VARCHAR(20) | Sí | | Serie del DTE |
| dte_numero | VARCHAR(20) | Sí | | Número del DTE; serie y número únicos |
| dte_fecha_certificacion | TIMESTAMPTZ | Sí | | Fecha de certificación; en CxC los cuatro datos DTE son obligatorios salvo en documentos legados |
| documento_origen_id | INT | Sí | FK -> DocumentoCxC(id) | Factura de origen; solo notas de crédito (obligatorio salvo en legados) |
| tipo_cambio_aplicado | DECIMAL(12,6) | Sí | | El servidor escribe 1 (moneda funcional) |
| estado | VARCHAR(20) | No | | **Vigente / Anulado** (antes: Pendiente/Pagado/Anulado) |
| asiento_id | INT | Sí | FK -> AsientoContable(id) | Asiento generado |

**Sin columna `saldo_pendiente`.** Se calcula en `vw_saldodocumentocxc`
(`monto_total − SUM(monto_aplicado) − SUM(monto_total de las notas de crédito vigentes cuyo documento_origen_id es el documento)`; una nota de crédito tiene saldo 0).
Triggers: `trg_nota_credito_valida_cxc` (RN-14) y `fn_limite_pago_cxc` (RN-05, descuenta las notas de crédito y rechaza pagos sobre una nota de crédito).

### LineaDocumentoCxC
Campos fiscales (la columna `porcentaje_impuesto` se renombró a `tasa_aplicada`):

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| impuesto_id | INT | No | FK -> Impuesto(id) | Impuesto aplicado |
| tasa_aplicada | DECIMAL(7,4) | No | | Fotografía de la tasa al registrar |
| monto_linea | DECIMAL(14,2) | No | | Total de la línea con IVA incluido; `CHECK (monto_linea = monto_base + monto_iva)` |
| monto_base | DECIMAL(14,2) | No | | Base sin IVA |
| monto_iva | DECIMAL(14,2) | No | | IVA de la línea |
| tipo_bien_servicio | VARCHAR(10) | No | | BIEN / SERVICIO |

`trg_linea_impuesto_valido_cxc` rechaza (código `23514`) una línea de documento no legado cuyo impuesto esté inactivo, sea LEGADO, no esté vigente en la fecha del documento, no aplique a VENTAS o tenga una tasa distinta de la del catálogo.

### ReciboPagoCliente, AplicacionPagoCliente
Sin cambios estructurales.

## 5. Cuentas por Pagar (CxP)

### DocumentoCxP
Análogo a DocumentoCxC: gana `fecha_vencimiento`, `estado` se simplifica a
Vigente/Anulado, y **sin columna `saldo_pendiente`** (ver `vw_saldodocumentocxp`). Tiene los mismos campos fiscales, de DTE y de origen. Diferencias: los datos DTE son opcionales, pero se admiten completos (UUID, serie y número) o ninguno (`ck_documentocxp_dte_completo`); la serie y número son únicos por proveedor (`ux_documentocxp_proveedor_dte_serie_numero`); y los triggers son `trg_nota_credito_valida_cxp`, `trg_linea_impuesto_valido_cxp` (ámbito COMPRAS) y `fn_limite_pago_cxp`.

### LineaDocumentoCxP
Mismos campos fiscales que `LineaDocumentoCxC`.

### PagoProveedorCabecera, AplicacionPagoProveedor
Sin cambios estructurales.

## 6. Tesorería

### CuentaBancaria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| banco | VARCHAR(100) | No | | Nombre del banco |
| numero | VARCHAR(50) | No | | Número de cuenta |
| tipo | VARCHAR(30) | No | | Monetaria/Ahorro (`ck_cuentabancaria_tipo`) |
| activa | BOOLEAN | No | | Por defecto TRUE; sin eliminación física (trigger `trg_prevenir_eliminacion_cuentabancaria`) |
| cuenta_contable_id | INT | No | FK -> CuentaContable(id) | Cuenta contable de Activo asociada; única (`uq_cuentabancaria_cuenta_contable`) |
| saldo_apertura | DECIMAL(14,2) | No | | Saldo inicial al crear la cuenta; no negativo |
| fecha_apertura | DATE | Sí | | Obligatoria si `saldo_apertura > 0` (`ck_cuentabancaria_apertura`) |

`UNIQUE(banco, numero)` (`uq_cuentabancaria_banco_numero`).

**Sin columna `saldo`.** Se calcula en `vw_saldocuentabancaria`.

### MovimientoTesoreria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| cuenta_bancaria_id | INT | No | FK -> CuentaBancaria(id) | Cuenta afectada |
| fecha | DATE | No | | Fecha del movimiento |
| tipo | VARCHAR(20) | No | | Ingreso/Egreso (`ck_movimiento_tipo`) |
| monto | DECIMAL(14,2) | No | | Mayor que 0 (`ck_movimiento_monto`) |
| asiento_id | INT | No | FK -> AsientoContable(id) | Asiento que genera el movimiento |
| descripcion | VARCHAR(255) | No | | Descripción |
| referencia | VARCHAR(100) | Sí | | Referencia libre |
| origen | VARCHAR(20) | No | | Manual/Transferencia/CxC/CxP/Apertura (`ck_movimiento_origen`) |
| transferencia_id | UUID | Sí | | Agrupa el egreso y el ingreso de una transferencia |
| recibo_pago_id | INT | Sí | FK -> ReciboPagoCliente(id) | Cobro de origen (`origen = 'CxC'`) |
| pago_proveedor_id | INT | Sí | FK -> PagoProveedorCabecera(id) | Pago de origen (`origen = 'CxP'`) |

`ck_movimiento_vinculo` exige que los campos de vínculo sean coherentes con `origen`: `Manual` y `Apertura` sin vínculos (la apertura solo es `Ingreso`); `Transferencia` con `transferencia_id`; `CxC` con `recibo_pago_id` y tipo `Ingreso`; `CxP` con `pago_proveedor_id` y tipo `Egreso`. Índices únicos parciales: un movimiento por recibo (`ux_movimiento_recibo_pago`), por pago a proveedor (`ux_movimiento_pago_proveedor`), uno de apertura por cuenta (`ux_movimiento_apertura`) y un movimiento por tipo en cada transferencia (`ux_movimiento_transferencia_tipo`); índice `ix_movimiento_cuenta_fecha (cuenta_bancaria_id, fecha)`. Inmutable: el trigger `trg_movimiento_inmutable` bloquea UPDATE y DELETE.

### ConciliacionBancaria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| cuenta_bancaria_id | INT | No | FK -> CuentaBancaria(id) | Cuenta conciliada |
| periodo_id | INT | No | FK -> PeriodoContable(id) | Periodo que contiene la fecha de corte |
| fecha | DATE | No | | Fecha de corte |
| estado | VARCHAR(20) | No | | Pendiente/Conciliado/Cancelada (`ck_conciliacion_estado`) |
| saldo_extracto | DECIMAL(14,2) | No | | Saldo según el estado de cuenta del banco a la fecha de corte (puede ser 0 o negativo) |

Índice único parcial `ux_conciliacion_pendiente_por_cuenta`: una sola conciliación `Pendiente` por cuenta. El trigger `trg_conciliacion_inmutable` bloquea DELETE y cualquier UPDATE sobre una conciliación `Conciliado` o `Cancelada` (código `55000`).

### DetalleConciliacion
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| conciliacion_id | INT | No | FK -> ConciliacionBancaria(id) | Conciliación |
| movimiento_id | INT | No | FK -> MovimientoTesoreria(id) | Movimiento marcado; único (`uq_detalleconciliacion_movimiento`): un movimiento pertenece a una sola conciliación |

El trigger `trg_detalle_conciliacion_valida` solo permite insertar o borrar filas si la conciliación está `Pendiente` (si no, `55000`), y al insertar exige que el movimiento sea de la misma cuenta, con fecha no posterior al corte y que no sea de apertura (si no, `23514`).

### vw_conciliacion_resumen
Una fila por conciliación: `conciliacion_id`, `cuenta_bancaria_id`, `periodo_id`, `fecha`, `estado`, `saldo_extracto`, `saldo_inicial`, `total_marcado`, `saldo_conciliado`, `diferencia` y `cantidad_movimientos`. `saldo_inicial` es el `saldo_extracto` de la conciliación `Conciliado` anterior más reciente de la cuenta, o `cuentabancaria.saldo_apertura` si no existe; `total_marcado` suma los movimientos del detalle (Ingreso +, Egreso -); `saldo_conciliado = saldo_inicial + total_marcado`; `diferencia = saldo_extracto - saldo_conciliado`.

### sp_finalizar_conciliacion(p_conciliacion_id, p_usuario_id)
Verifica en este orden: perfil Contador o Administrador del sistema (si no, excepción por defecto `P0001`); que la conciliación exista (`P0002`); que no esté `Conciliado` ni `Cancelada` (`55000`); que `diferencia` en `vw_conciliacion_resumen` sea 0 (`55000`). Entonces la pasa a `Conciliado` y registra la auditoría `finalizar_conciliacion`. La API traduce `P0001` a `403`, `P0002` a `404` y `55000` a `409`.

## 7. Consolidación de Saldos

### PeriodoContable.cierres
`periodocontable.cierres` (INT, no nulo, por defecto 0) cuenta los cierres del periodo; `sp_cerrar_periodo` lo incrementa en cada cierre, incluso sin movimientos, y su valor es el `cierre_numero` vigente. Solo lo escribe el procedimiento: la API no lo recibe ni lo expone en el JSON del periodo.

### SaldoCuentaPeriodo
Fotografía inalterable del saldo de cada cuenta contable al momento del cierre
contable. Se escribe solo desde `sp_cerrar_periodo`; un trigger impide cualquier
modificación o borrado posterior. Cada cierre inserta una versión nueva
(`cierre_numero` 1, 2, ...) y las versiones anteriores se conservan. La combinación
de periodo, cierre y cuenta es única (`UNIQUE(periodo_id, cierre_numero, cuenta_id)`).

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| periodo_id | INT | No | FK -> PeriodoContable(id) | Periodo al que corresponde |
| cierre_numero | INT | No | | Versión del cierre del periodo (1, 2, ...) |
| cuenta_id | INT | No | FK -> CuentaContable(id) | Cuenta consolidada |
| total_debito | DECIMAL(14,2) | No | | Suma de débitos del periodo |
| total_credito | DECIMAL(14,2) | No | | Suma de créditos del periodo |
| saldo_final | DECIMAL(14,2) | No | | Saldo al cierre, según naturaleza de la cuenta |

Distinción clave: un saldo almacenado y editable es un riesgo (puede desincronizarse
de los movimientos); un saldo consolidado **e inalterable** por periodo es una
práctica contable estándar — equivale al saldo de cierre que se asienta en libros. El
saldo del periodo en curso se sigue calculando siempre en tiempo real
(`vw_balance_saldos`), esta tabla solo aplica a periodos ya cerrados.

### fn_reporte_saldos(p_periodo_id, p_acumulado)
Función de tabla, solo lectura (`STABLE`, sin DML), que alimenta el balance general y el estado de resultados. Con `p_acumulado = false` considera solo el periodo indicado; con `true`, todos los periodos con `fecha_fin` menor o igual a la del indicado. Un periodo `Cerrado` con cierre aporta las filas de `vw_saldocuentaperiodo_vigente`; los demás aportan las líneas de asientos `Confirmado` y `Anulado` (nunca `Borrador`). Devuelve todas las cuentas, activas o no, aunque estén en 0, ordenadas por código; con un periodo inexistente devuelve 0 filas.

| Columna | Descripción |
|---|---|
| cuenta_id, codigo, nombre, tipo, naturaleza, cuenta_padre_id | Datos de la cuenta |
| nivel | Profundidad en la jerarquía (1 = raíz) |
| es_hoja | `true` si la cuenta no tiene subcuentas |
| total_debito, total_credito | Acumulados de la cuenta propia más todos sus descendientes |
| saldo | Deudora: débito - crédito; en otro caso crédito - débito |

## Vistas (reemplazan las columnas de saldo eliminadas)

| Vista | Reemplaza a | Descripción |
|---|---|---|
| `vw_balance_saldos` | `CuentaContable.saldo` (nunca existió como columna) | Saldo en tiempo real por cuenta contable, sobre asientos `Confirmado` y `Anulado` (se excluye `Borrador`). `total_debito`, `total_credito` y `saldo` están acumulados: cuenta propia más todos sus descendientes. Además expone `nivel` (1 = raíz), `cuenta_padre_id`, `es_hoja`, `activa` y los valores sin jerarquía `debito_propio` y `credito_propio` |
| `vw_saldocuentaperiodo_vigente` | — | Filas de `SaldoCuentaPeriodo` del cierre vigente de cada periodo (`cierre_numero = PeriodoContable.cierres`); vacía si ese cierre no tuvo movimientos |
| `vw_saldocuentabancaria` | `CuentaBancaria.saldo` | Saldo en tiempo real por cuenta bancaria: suma firmada de sus movimientos (Ingreso +, Egreso -); el saldo de apertura entra por su movimiento de apertura, no se suma aparte |
| `vw_conciliacion_resumen` | — | Saldo inicial, total marcado, saldo conciliado y diferencia de cada conciliación bancaria (fuente de la matemática de RN-10) |
| `vw_saldodocumentocxc` | `DocumentoCxC.saldo_pendiente` | Saldo pendiente por documento de CxC: monto total menos pagos aplicados y notas de crédito vigentes referenciadas; 0 para notas de crédito |
| `vw_saldodocumentocxp` | `DocumentoCxP.saldo_pendiente` | Saldo pendiente por documento de CxP: monto total menos pagos aplicados y notas de crédito vigentes referenciadas; 0 para notas de crédito |
