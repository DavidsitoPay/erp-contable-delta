# Diccionario de Datos — Delta ERP Contable

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
| password_hash | VARCHAR(255) | No | | Contraseña encriptada |
| perfil_id | INT | No | FK -> Perfil(id) | Perfil asignado |
| activo | BOOLEAN | No | | Usuario activo |

### BitacoraAuditoria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| usuario_id | INT | No | FK -> Usuario(id) | Usuario que realizó la acción |
| fecha | DATE | No | | Fecha de la acción |
| accion | VARCHAR(100) | No | | crear / editar / eliminar |
| tabla_afectada | VARCHAR(100) | No | | Tabla afectada |
| detalle | TEXT | Sí | | Detalle adicional |

## 2. Catálogo y Configuración General

### Moneda
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR(10) | No | | GTQ, USD, etc. |
| nombre | VARCHAR(100) | No | | Nombre de la moneda |

### HistorialTipoCambio
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| moneda_id | INT | No | FK -> Moneda(id) | Moneda |
| fecha | DATE | No | | Fecha de la tasa |
| tasa | DECIMAL(12,6) | No | | Tipo de cambio |

### PeriodoContable
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| nombre | VARCHAR(100) | No | | Ej. Enero 2026 |
| fecha_inicio | DATE | No | | Inicio del periodo |
| fecha_fin | DATE | No | | Cierre del periodo |
| estado | VARCHAR(20) | No | | Abierto / Cerrado |

### Contraparte
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| tipo | VARCHAR(20) | No | | Cliente / Proveedor |
| nombre | VARCHAR(200) | No | | Razón social |
| nit | VARCHAR(30) | Sí | | NIT |
| direccion | VARCHAR(255) | Sí | | Dirección |

### CuentaContable
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR(20) | No | | Código dentro del catálogo |
| nombre | VARCHAR(150) | No | | Nombre de la cuenta |
| tipo | VARCHAR(30) | No | | Activo/Pasivo/Capital/Ingreso/Gasto |
| naturaleza | VARCHAR(20) | No | | Deudora / Acreedora |
| cuenta_padre_id | INT | Sí | FK -> CuentaContable(id) | Jerarquía |
| activa | BOOLEAN | No | | Cuenta activa |

> **Nota:** esta tabla NO almacena un campo `saldo`. El saldo se deriva siempre de
> `SUM(debito) − SUM(credito)` de `LineaAsiento`, nunca se guarda como columna mutable.

### CentroCosto
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| codigo | VARCHAR(20) | No | | Código |
| nombre | VARCHAR(150) | No | | Nombre |
| activo | BOOLEAN | No | | Activo |

## 3. Transacciones y Asientos Contables

### AsientoContable
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| numero | VARCHAR(30) | No | | Número correlativo |
| fecha | DATE | No | | Fecha del asiento |
| periodo_id | INT | No | FK -> PeriodoContable(id) | Periodo |
| monto | DECIMAL(14,2) | No | | Monto total |
| estado | VARCHAR(20) | No | | Borrador/Confirmado/Anulado |
| usuario_id | INT | No | FK -> Usuario(id) | Usuario que registró |
| tipo_cambio_aplicado | DECIMAL(12,6) | Sí | | Si aplica moneda extranjera |

### LineaAsiento
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| asiento_id | INT | No | FK -> AsientoContable(id) | Asiento |
| cuenta_id | INT | No | FK -> CuentaContable(id) | Cuenta afectada |
| centro_costo_id | INT | Sí | FK -> CentroCosto(id) | Centro de costo |
| debito | DECIMAL(14,2) | No | | Monto al débito |
| credito | DECIMAL(14,2) | No | | Monto al crédito |

### PlantillaAsiento / LineaPlantillaAsiento
Plantillas predefinidas para asientos recurrentes (ver estructura análoga a AsientoContable/LineaAsiento).

## 4. Cuentas por Cobrar (CxC)

### DocumentoCxC
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| numero | VARCHAR(30) | No | | Número de documento |
| tipo_documento | VARCHAR(20) | No | | Factura/NotaCredito/NotaDebito |
| cliente_id | INT | No | FK -> Contraparte(id) | Cliente |
| fecha | DATE | No | | Fecha de emisión |
| monto_total | DECIMAL(14,2) | No | | Monto total |
| saldo_pendiente | DECIMAL(14,2) | No | | **Derivado — ver RN-12, no editable por la API** |
| estado | VARCHAR(20) | No | | Pendiente/Pagado/Anulado |
| asiento_id | INT | Sí | FK -> AsientoContable(id) | Asiento generado |

### LineaDocumentoCxC, ReciboPagoCliente, AplicacionPagoCliente
Ver estructura completa entregada el 05/09 (líneas de detalle, recibo cabecera, aplicación
de pagos a documentos — misma lógica que su contraparte CxP).

## 5. Cuentas por Pagar (CxP)

### DocumentoCxP
Análogo a DocumentoCxC, con `proveedor_id` en vez de `cliente_id`. `saldo_pendiente`
sujeto a la misma regla RN-12.

### LineaDocumentoCxP, PagoProveedorCabecera, AplicacionPagoProveedor
Estructura análoga a su contraparte de CxC.

## 6. Tesorería

### CuentaBancaria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| banco | VARCHAR(100) | No | | Nombre del banco |
| numero | VARCHAR(50) | No | | Número de cuenta |
| tipo | VARCHAR(30) | No | | Monetaria/Ahorro |

> **Corrección aplicada:** se elimina `saldo` como columna libremente editable. El saldo
> en tiempo real se calcula bajo demanda; el saldo de cierre se consolida en
> `SaldoCuentaPeriodo` (nueva tabla, inmutable).

### SaldoCuentaPeriodo *(nueva — corrección de observación de auditoría)*
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| cuenta_bancaria_id | INT | No | FK -> CuentaBancaria(id) | Cuenta |
| periodo_id | INT | No | FK -> PeriodoContable(id) | Periodo |
| saldo_inicial | DECIMAL(14,2) | No | | Heredado del cierre anterior |
| saldo_final | DECIMAL(14,2) | No | | Resultado del periodo |
| fecha_calculo | TIMESTAMP | No | | Fecha/hora del cálculo |
| generado_por | INT | No | FK -> Usuario(id) | Usuario que ejecutó el cierre |

### MovimientoTesoreria
| Campo | Tipo | Nulo | Llave | Descripción |
|---|---|---|---|---|
| id | SERIAL | No | PK | Identificador único |
| cuenta_bancaria_id | INT | No | FK -> CuentaBancaria(id) | Cuenta afectada |
| fecha | DATE | No | | Fecha |
| tipo | VARCHAR(30) | No | | Ingreso/Egreso |
| monto | DECIMAL(14,2) | No | | Monto |

### ConciliacionBancaria / DetalleConciliacion
Ver estructura entregada el 05/09.
