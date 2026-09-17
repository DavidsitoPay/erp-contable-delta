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

### Moneda / HistorialTipoCambio / PeriodoContable / Contraparte
Sin cambios respecto a la versión anterior — ver estructura completa en el documento
oficial (05/09, sección 2).

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

### CentroCosto
Sin cambios.

## 3. Transacciones y Asientos Contables

### AsientoContable / LineaAsiento / PlantillaAsiento / LineaPlantillaAsiento
Sin cambios estructurales respecto a la versión anterior.

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
| monto_total | DECIMAL(14,2) | No | | Monto total |
| tipo_cambio_aplicado | DECIMAL(12,6) | Sí | | Si aplica moneda extranjera |
| estado | VARCHAR(20) | No | | **Vigente / Anulado** (antes: Pendiente/Pagado/Anulado) |
| asiento_id | INT | Sí | FK -> AsientoContable(id) | Asiento generado |

**Sin columna `saldo_pendiente`.** Se calcula en `vw_saldodocumentocxc`
(`monto_total − SUM(monto_aplicado)`).

### LineaDocumentoCxC, ReciboPagoCliente, AplicacionPagoCliente
Sin cambios estructurales.

## 5. Cuentas por Pagar (CxP)

### DocumentoCxP
Análogo a DocumentoCxC: gana `fecha_vencimiento`, `estado` se simplifica a
Vigente/Anulado, y **sin columna `saldo_pendiente`** (ver `vw_saldodocumentocxp`).

### LineaDocumentoCxP, PagoProveedorCabecera, AplicacionPagoProveedor
Sin cambios estructurales.

## 6. Tesorería

### CuentaBancaria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| banco | VARCHAR(100) | No | | Nombre del banco |
| numero | VARCHAR(50) | No | | Número de cuenta |
| tipo | VARCHAR(30) | No | | Monetaria/Ahorro |

**Sin columna `saldo`.** Se calcula en `vw_saldocuentabancaria`.

### MovimientoTesoreria, ConciliacionBancaria, DetalleConciliacion
Sin cambios estructurales.

## 7. Consolidación de Saldos

### SaldoCuentaPeriodo
Fotografía inalterable del saldo de cada cuenta contable al momento del cierre
contable. Se escribe una sola vez desde `sp_cerrar_periodo`; un trigger impide
cualquier modificación o borrado posterior. La combinación de periodo y cuenta es
única.

| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| periodo_id | INT | No | FK -> PeriodoContable(id) | Periodo al que corresponde |
| cuenta_id | INT | No | FK -> CuentaContable(id) | Cuenta consolidada |
| total_debito | DECIMAL(14,2) | No | | Suma de débitos del periodo |
| total_credito | DECIMAL(14,2) | No | | Suma de créditos del periodo |
| saldo_final | DECIMAL(14,2) | No | | Saldo al cierre, según naturaleza de la cuenta |

Distinción clave: un saldo almacenado y editable es un riesgo (puede desincronizarse
de los movimientos); un saldo consolidado **e inalterable** por periodo es una
práctica contable estándar — equivale al saldo de cierre que se asienta en libros. El
saldo del periodo en curso se sigue calculando siempre en tiempo real
(`vw_balance_saldos`), esta tabla solo aplica a periodos ya cerrados.

## Vistas (reemplazan las columnas de saldo eliminadas)

| Vista | Reemplaza a | Descripción |
|---|---|---|
| `vw_balance_saldos` | `CuentaContable.saldo` (nunca existió como columna) | Saldo en tiempo real por cuenta contable |
| `vw_saldocuentabancaria` | `CuentaBancaria.saldo` | Saldo en tiempo real por cuenta bancaria |
| `vw_saldodocumentocxc` | `DocumentoCxC.saldo_pendiente` | Saldo pendiente por documento de CxC |
| `vw_saldodocumentocxp` | `DocumentoCxP.saldo_pendiente` | Saldo pendiente por documento de CxP |
