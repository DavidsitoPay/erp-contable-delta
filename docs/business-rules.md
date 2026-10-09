# Reglas de negocio — Delta ERP Contable

> Actualizado según el documento oficial v2 (12/09/2026). Se agrega la columna
> "Dónde se aplica" para dejar explícito qué reglas viven en la base de datos
> (trigger/procedimiento) y cuáles en la API — ver docs/architecture.md.

| Código | Regla | Dónde se aplica |
|---|---|---|
| RN-01 | Todo asiento contable debe cumplir partida doble (suma de débitos = suma de créditos) antes de confirmarse. | Trigger (`trg_validar_partida_doble`) |
| RN-02 | No se permite registrar, modificar ni eliminar líneas de asiento de un periodo contable cerrado. | Trigger (`trg_bloquear_periodo_cerrado_linea`) |
| RN-03 | Un asiento solo puede reversarse mediante un asiento de reversión; nunca se elimina físicamente. La reversa se vincula a su original mediante `asientocontable.reversa_de_id` (único: un asiento tiene a lo sumo una reversa). Solo se reversa un asiento `Confirmado` que no sea a su vez una reversa (`reversa_de_id IS NULL`), por lo que no puede reversarse dos veces. El original queda `Anulado` (confirmado y ya revertido) y la reversa queda `Confirmado` con las líneas invertidas; ambos permanecen contabilizados y se netean a 0. El número de la reversa es `'REV-' + numero` truncado a 30 caracteres; la trazabilidad es el vínculo, no el texto. Solo el Administrador del sistema reversa (el procedimiento verifica el perfil del usuario en la base de datos). La reversa exige un motivo obligatorio de hasta 250 caracteres, que queda en la bitácora (`reversar_asiento`). El periodo del asiento debe estar `Abierto`, y la reversa se registra en el mismo periodo con fecha `GREATEST(fecha del original, LEAST(hoy, fin del periodo))`. No se reversa un asiento vinculado a un documento de cuentas por cobrar, de cuentas por pagar o a un movimiento de tesorería. | Trigger (bloquea DELETE) + Procedimiento (`sp_reversar_asiento`) + Endpoint (`POST /api/asientos/{id}/reversar`) |
| RN-04 | Toda factura CxC/CxP debe asociarse a un cliente/proveedor existente en el catálogo. | FK (`contraparte`) |
| RN-05 | La aplicación de un pago a una factura no puede exceder el saldo pendiente de esa factura. | Trigger (`trg_limite_pago_cxc` / `trg_limite_pago_cxp`) |
| RN-06 | La moneda funcional es el quetzal (GTQ): todo importe de documentos, líneas y asientos se registra en GTQ y `tipo_cambio_aplicado` lo fija el servidor en 1 (el valor que envíe el cliente se descarta). La multimoneda, el tipo de cambio de Banguat y las diferencias cambiarias no están implementados; están pendientes dentro de M10. | API (`MotorFiscal`, `AsientoFacturaBuilder`) + BD (`moneda.es_funcional` único, `configuracionfiscal.moneda_funcional_id`) |
| RN-07 | El IVA se calcula con el catálogo `impuesto` (vigencia, ámbito ventas/compras, crédito fiscal), no manualmente. El precio unitario incluye el IVA (Decreto 27-92, art. 10): IVA = total × tasa / (100 + tasa), base = total − IVA, con redondeo a 2 decimales por línea (`AwayFromZero`) y totales como suma de líneas. En ventas el IVA va a la cuenta de IVA débito fiscal. En compras el IVA acreditable (impuesto con `genera_credito`, arts. 15 y 16) va a IVA crédito fiscal; el IVA de operaciones sin derecho a crédito y todo el valor de las compras a pequeño contribuyente (arts. 45 a 50, art. 49) se registran como costo. Exento (exportación de servicios, art. 7 num. 2) y no afecto (arts. 2 y 3) no generan IVA. Cada línea guarda `impuesto_id` y `tasa_aplicada` como fotografía. La cuenta de IVA se exige solo si la factura genera IVA de ese tipo; si no está configurada, la API responde 409. El impuesto debe estar activo, vigente en la fecha del documento y aplicar al ámbito del documento; un proveedor pequeño contribuyente solo admite el impuesto de pequeño contribuyente y una contraparte exenta solo impuestos exentos o no afectos. Las vigencias de los impuestos sembrados son parámetros referenciales: validar con asesor. Las retenciones de ISR e IVA no están implementadas. | API (`MotorFiscal`, `ImpuestoRules`, `FacturaFiscalService`) + Triggers (`trg_linea_impuesto_valido_cxc/cxp`, `trg_impuesto_sin_traslape`, `trg_impuesto_inmutable_si_usado`) |
| RN-08 | Toda operación relevante genera un registro en `BitacoraAuditoria` (usuario, fecha, acción). | API, vía `sp_registrar_auditoria` (nunca INSERT directo ni trigger) |
| RN-09 | El acceso a cada módulo/operación depende del perfil y permisos del usuario autenticado. | API (autorización) + Procedimientos sensibles verifican perfil internamente |
| RN-10 | La conciliación bancaria contrasta los movimientos del sistema contra el saldo del estado de cuenta (extracto); solo Contador y Administrador del sistema la finalizan, y solo con diferencia 0. Ver «Tesorería y conciliación bancaria». | Vista (`vw_conciliacion_resumen`) + Procedimiento (`sp_finalizar_conciliacion`, verifica perfil y diferencia) + API |
| RN-11 | La carga inicial (M9) valida consistencia antes de integrarse a la base productiva. | API + funciones de soporte (`02_functions.sql`) |
| RN-12 | Los datos del DTE de FEL (UUID de autorización, serie, número y fecha de certificación) se registran, no se certifican: Delta ERP no emite documentos con validez fiscal ni se conecta con la SAT. Son obligatorios en las facturas de cuentas por cobrar y, en cuentas por pagar, cuando la factura genera crédito fiscal (se admiten el trío UUID, serie y número juntos, o ninguno). El UUID debe tener forma canónica de 36 caracteres; serie y número, de 1 a 20; la fecha de certificación no puede ser futura ni anterior a la fecha del documento. El UUID es único y la serie y número son únicos (en CxP, por proveedor); el duplicado responde 409. | API (`DteRules`, `FacturaRules`) + BD (CHECK `ck_documentocxc_dte`, `ck_documentocxp_dte_completo` e índices únicos `ux_documentocxc_dte_*`, `ux_documentocxp_*dte*`) |
| RN-13 | El NIT de clientes y proveedores se valida con el dígito verificador módulo 11 y se guarda normalizado (`cuerpo-DV`, `K` si el resto es 10). `CF` (consumidor final) solo es válido en clientes. | API (`NitRules`, `ContrapartesController`) |
| RN-14 | Una nota de crédito debe referenciar la factura vigente de origen (`documento_origen_id`), del mismo cliente o proveedor, y su monto no puede exceder el saldo pendiente de esa factura (409). Su asiento invierte los lados del de la factura y reduce el IVA débito o crédito. Reduce el saldo del documento de origen (`vw_saldodocumentocxc` / `vw_saldodocumentocxp`) y el límite de pago de RN-05; la nota en sí no admite pagos ni cobros y su saldo es 0. | Trigger (`trg_nota_credito_valida_cxc/cxp`, `fn_limite_pago_cxc/cxp`) + Vistas + API (`AsientoFacturaBuilder`, `FacturaService`) |
| RN-15 | La fecha de un documento debe estar dentro del rango del periodo contable seleccionado, y ese periodo debe estar `Abierto`. | API (`ValidacionContable`) + Trigger (RN-02) |
| RN-16 | Los documentos anteriores a la configuración fiscal quedan con `calculo_legado = TRUE`: no se recalculan, no exigen DTE, sus líneas apuntan a `IVA_GENERAL` (tasa 12), `NO_AFECTO` (tasa 0) o, para cualquier otra tasa, a un impuesto `LEGADO_<tasa>` inactivo y no creable por API, y se clasifican como `SERVICIO`. El libro de compras y ventas los incluye y advierte su cantidad. | BD (migración 13) + API |

## Libro de compras y ventas

- `GET api/libros/fiscal/ventas` y `GET api/libros/fiscal/compras` (parámetros `anio` y `mes` calendario; Administrador o Contador) listan los documentos con fecha en el mes, de cualquier tipo, ordenados por fecha y número.
- Columnas: fecha, tipo de documento, serie, número, NIT, nombre, base de bienes, base de servicios, exento / no afecto, IVA y total; las notas de crédito aparecen con importes negativos y la referencia (serie, número y UUID) de su origen. Los documentos anulados aparecen con importes 0.
- En compras, la base de operaciones sin crédito fiscal y de pequeño contribuyente se suma a la base de bienes o servicios, porque es costo.
- La respuesta incluye NIT y nombre legal de la empresa y advertencias (por ejemplo, documentos de cálculo legado). La exportación a CSV se genera en el cliente.

## Saldos: nunca columnas editables

Ninguna entidad almacena un saldo como columna libremente editable:

- `CuentaContable` — sin columna `saldo`; se deriva de `vw_balance_saldos`.
- `CuentaBancaria` — sin columna `saldo`; se deriva de `vw_saldocuentabancaria` (suma firmada de sus movimientos). `saldo_apertura` es un dato fijado al crear la cuenta, no un saldo vivo.
- `DocumentoCxC` / `DocumentoCxP` — sin columna `saldo_pendiente`; se deriva de
  `vw_saldodocumentocxc` / `vw_saldodocumentocxp`.
- `SaldoCuentaPeriodo` — única excepción: es un saldo **almacenado a propósito**,
  pero inmutable (trigger bloquea UPDATE/DELETE) y escrito solo desde
  `sp_cerrar_periodo`, como fotografía del cierre. Cada cierre agrega una versión
  nueva (`cierre_numero` 1, 2, ...); las anteriores se conservan sin cambios y
  `vw_saldocuentaperiodo_vigente` expone las filas del cierre vigente de cada periodo. No se recalcula con
  cada transacción: las vistas dan el saldo en tiempo real y la tabla se reserva
  para el cierre.

## Libros, balance y cierre

- Solo los asientos `Borrador` se excluyen de libro diario, libro mayor, balance de
  saldos (`vw_balance_saldos`) y de la fotografía del cierre; `Confirmado` y `Anulado`
  se contabilizan.
- `vw_balance_saldos` acumula la jerarquía del catálogo: el saldo de una cuenta padre es su saldo propio más el de todos sus descendientes, con el signo de su naturaleza. El balance de saldos totaliza sumando solo las filas de nivel 1 (para no contar dos veces las subcuentas) y muestra el saldo en columnas Deudor o Acreedor.
- El libro mayor de una cuenta padre consolida los movimientos de la cuenta y de todos sus descendientes, ordenados por fecha, número de asiento y línea, e identifica la cuenta de cada movimiento. El saldo acumulado corre con la naturaleza de la cuenta seleccionada.
- Una cuenta con saldo propio contabilizado (neto `Confirmado`/`Anulado` distinto de 0) o con líneas en asientos `Borrador` no puede recibir subcuentas: el trigger `trg_validar_padre_sin_saldo` rechaza el alta o el cambio de padre con `55000`, que la API responde `409 Conflict`. El saldo propio debe reclasificarse antes a una subcuenta.
- `sp_cerrar_periodo` solo cierra un periodo `Abierto` y `sp_reabrir_periodo` solo
  reabre uno `Cerrado`; en otro estado fallan con el código `55000`, que la API
  responde `409 Conflict`. Las fallas de autorización del procedimiento responden `403`.
- `cierre_numero` proviene de `periodocontable.cierres`, que `sp_cerrar_periodo` incrementa en cada cierre, incluso si el periodo no tuvo movimientos. `vw_saldocuentaperiodo_vigente` une con ese contador, por lo que queda vacía para un periodo cuyo cierre vigente no tuvo movimientos.

## Reportes y estados financieros

- El balance general es al cierre de un periodo: acumula todos los periodos con `fecha_fin` menor o igual a la del periodo consultado. El estado de resultados cubre solo el periodo consultado.
- Cada periodo incluido aporta según su estado: un periodo `Cerrado` aporta su cierre vigente (`SaldoCuentaPeriodo` vía `vw_saldocuentaperiodo_vigente`); uno `Abierto` o reabierto aporta en vivo los asientos `Confirmado` y `Anulado`. Los asientos `Borrador` nunca cuentan.
- El balance general se presenta en las secciones Activo, Pasivo y Capital, con roll-up jerárquico (el saldo de una cuenta padre incluye a sus descendientes) y totales de sección que suman solo las cuentas de nivel 1. El saldo de cada cuenta lleva el signo de su naturaleza.
- No existe asiento de cierre de ingresos y gastos, por eso el Capital incluye la línea «Resultado del ejercicio (no distribuido)» = Ingresos - Gastos acumulados hasta el periodo consultado. Total capital = Capital + resultado.
- El balance cuadra cuando Activo = Pasivo + Total capital; la fila Diferencia muestra Activo - (Pasivo + Total capital) y «Cuadra» cuando es 0.
- El estado de resultados lista solo cuentas de tipo Ingreso y Gasto del periodo consultado. Utilidad neta = Ingresos - Gastos; si es negativa se presenta como pérdida neta.
- Cada reporte indica su fuente: `Cierre` si el periodo consultado está `Cerrado`, `Preliminar` en otro caso. El balance general avisa además cuando incluye periodos anteriores que no están cerrados (`incluyePeriodosAbiertos`).
- Los reportes son de solo lectura y no registran bitácora. Consultarlos requiere perfil Administrador del sistema o Contador; Vendedor y Técnico reciben `403`. Un periodo inexistente o ausente responde `400`.

## Tesorería y conciliación bancaria

- Cada cuenta bancaria está ligada a una única cuenta contable de tipo Activo (hoja y activa); una cuenta contable no puede asociarse a dos cuentas bancarias, y el par banco + número es único. La cuenta contable no se cambia después de creada.
- El saldo de apertura se fija solo al crear la cuenta (no es editable). Si es mayor que 0 se exige fecha de apertura y una cuenta de contrapartida de tipo Capital, y en la misma transacción se registra el asiento (debe Banco / haber Capital) y un movimiento de ingreso de origen `Apertura`. Hay a lo sumo un movimiento de apertura por cuenta y no es conciliable.
- Todo movimiento de tesorería genera su asiento contable en un periodo `Abierto` cuyo rango contiene la fecha del movimiento. Ingreso: debe Banco / haber contrapartida. Egreso: debe contrapartida / haber Banco. La contrapartida de un movimiento manual es cualquier cuenta hoja activa distinta de la cuenta contable del banco (incluidas Gasto e Ingreso, p. ej. comisiones e intereses). Los montos son positivos, con máximo 2 decimales.
- Una transferencia entre cuentas bancarias distintas registra un solo asiento (debe cuenta destino / haber cuenta origen) y dos movimientos con el mismo identificador de transferencia: egreso en el origen e ingreso en el destino.
- Los movimientos son inmutables (trigger): no se editan ni se eliminan. Un error se corrige registrando un movimiento manual inverso; no existe anulación de movimientos. La API y `sp_reversar_asiento` rechazan (`409`) la reversa de un asiento ligado a un movimiento de tesorería, porque descuadraría libros contra banco.
- El sobregiro está permitido: un egreso puede dejar el saldo bancario negativo.
- Las cuentas bancarias no se eliminan; se desactivan. Una cuenta inactiva no admite movimientos, transferencias, conciliaciones ni pagos.
- Operar tesorería (cuentas, movimientos, transferencias y conciliaciones) requiere perfil Administrador del sistema o Contador.

### Conciliación (RN-10)

- Se concilia una cuenta a una fecha de corte, indicando el saldo del extracto bancario a esa fecha. El periodo de la conciliación es el que contiene la fecha de corte.
- Se marcan los movimientos de la cuenta con fecha hasta la fecha de corte que aún no pertenecen a otra conciliación. El movimiento de apertura y los de otra cuenta o posteriores al corte no se pueden marcar; un movimiento no puede estar en dos conciliaciones.
- Saldo inicial = saldo del extracto de la última conciliación finalizada de la cuenta; si no hay ninguna, el saldo de apertura de la cuenta. Saldo conciliado = saldo inicial + movimientos marcados (ingresos suman, egresos restan). Diferencia = saldo del extracto - saldo conciliado. Todo se calcula en `vw_conciliacion_resumen`.
- Una conciliación solo se finaliza con diferencia 0 y por un usuario con perfil Contador o Administrador del sistema (se valida en el procedimiento). Finalizar una conciliación ya finalizada o cancelada, o con diferencia distinta de 0, responde `409`.
- Estados: `Pendiente`, `Conciliado` y `Cancelada`. Solo una conciliación `Pendiente` se edita (fecha de corte y saldo del extracto), se cancela o cambia sus movimientos marcados. Cancelar libera los movimientos marcados y conserva la fila como rastro. Una conciliación `Conciliado` o `Cancelada` no se modifica ni se reabre.
- Hay a lo sumo una conciliación `Pendiente` por cuenta bancaria. Editar a una fecha de corte anterior a un movimiento ya marcado se rechaza (`409`): el movimiento se desmarca primero.

## Cobros y pagos

- Registrar un cobro (CxC) o un pago a proveedor (CxP) requiere perfil Administrador del sistema o Contador. El Vendedor conserva la facturación y la consulta de CxC, pero recibe `403` al registrar cobros.
- Todo cobro o pago nuevo debe indicar una cuenta bancaria activa; sin ella responde `400`.
- Cada cobro o pago genera un movimiento de tesorería (ingreso para cobros, egreso para pagos) y su asiento, en un periodo `Abierto` que contiene la fecha del pago. Cobro: debe Banco / haber Clientes. Pago: debe Proveedores / haber Banco. La cuenta de control (Clientes o Proveedores) no la elige el usuario: se toma de la línea de control del asiento de cada factura aplicada, con una línea por cuenta de control distinta, de modo que el asiento cuadra por construcción (RN-01).
- El cobro o pago, su movimiento y su asiento se registran en una sola transacción junto con la bitácora (RN-08).
