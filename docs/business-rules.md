# Reglas de negocio — Delta ERP Contable

> Actualizado según el documento oficial v2 (12/09/2026). Se agrega la columna
> "Dónde se aplica" para dejar explícito qué reglas viven en la base de datos
> (trigger/procedimiento) y cuáles en la API — ver docs/architecture.md.

| Código | Regla | Dónde se aplica |
|---|---|---|
| RN-01 | Todo asiento contable debe cumplir partida doble (suma de débitos = suma de créditos) antes de confirmarse. | Trigger (`trg_validar_partida_doble`) |
| RN-02 | No se permite registrar, modificar ni eliminar líneas de asiento de un periodo contable cerrado. | Trigger (`trg_bloquear_periodo_cerrado_linea`) |
| RN-03 | Un asiento solo puede reversarse mediante un asiento de reversión; nunca se elimina físicamente. La reversa se vincula a su original mediante `asientocontable.reversa_de_id` (único: un asiento tiene a lo sumo una reversa). Solo se reversa un asiento `Confirmado` que no sea a su vez una reversa (`reversa_de_id IS NULL`), por lo que no puede reversarse dos veces. El original queda `Anulado` (confirmado y ya revertido) y la reversa queda `Confirmado` con las líneas invertidas; ambos permanecen contabilizados y se netean a 0. El número de la reversa es `'REV-' + numero` truncado a 30 caracteres; la trazabilidad es el vínculo, no el texto. | Trigger (bloquea DELETE) + Procedimiento (`sp_reversar_asiento`) |
| RN-04 | Toda factura CxC/CxP debe asociarse a un cliente/proveedor existente en el catálogo. | FK (`contraparte`) |
| RN-05 | La aplicación de un pago a una factura no puede exceder el saldo pendiente de esa factura. | Trigger (`trg_limite_pago_cxc` / `trg_limite_pago_cxp`) |
| RN-06 | Toda transacción se registra en la moneda funcional (GTQ); operaciones en otra moneda se convierten con el tipo de cambio vigente. | API (al construir el asiento) |
| RN-07 | El cálculo de impuestos (IVA/ISR) se aplica según los parámetros tributarios configurados, no manualmente. | API |
| RN-08 | Toda operación relevante genera un registro en `BitacoraAuditoria` (usuario, fecha, acción). | API, vía `sp_registrar_auditoria` (nunca INSERT directo ni trigger) |
| RN-09 | El acceso a cada módulo/operación depende del perfil y permisos del usuario autenticado. | API (autorización) + Procedimientos sensibles verifican perfil internamente |
| RN-10 | La conciliación bancaria contrasta los movimientos del sistema contra el saldo del estado de cuenta (extracto); solo Contador y Administrador del sistema la finalizan, y solo con diferencia 0. Ver «Tesorería y conciliación bancaria». | Vista (`vw_conciliacion_resumen`) + Procedimiento (`sp_finalizar_conciliacion`, verifica perfil y diferencia) + API |
| RN-11 | La carga inicial (M9) valida consistencia antes de integrarse a la base productiva. | API + funciones de soporte (`02_functions.sql`) |

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
- `sp_cerrar_periodo` solo cierra un periodo `Abierto` y `sp_reabrir_periodo` solo
  reabre uno `Cerrado`; en otro estado fallan con el código `55000`, que la API
  responde `409 Conflict`. Las fallas de autorización del procedimiento responden `403`.
- `cierre_numero` proviene de `periodocontable.cierres`, que `sp_cerrar_periodo` incrementa en cada cierre, incluso si el periodo no tuvo movimientos. `vw_saldocuentaperiodo_vigente` une con ese contador, por lo que queda vacía para un periodo cuyo cierre vigente no tuvo movimientos.

## Tesorería y conciliación bancaria

- Cada cuenta bancaria está ligada a una única cuenta contable de tipo Activo (hoja y activa); una cuenta contable no puede asociarse a dos cuentas bancarias, y el par banco + número es único. La cuenta contable no se cambia después de creada.
- El saldo de apertura se fija solo al crear la cuenta (no es editable). Si es mayor que 0 se exige fecha de apertura y una cuenta de contrapartida de tipo Capital, y en la misma transacción se registra el asiento (debe Banco / haber Capital) y un movimiento de ingreso de origen `Apertura`. Hay a lo sumo un movimiento de apertura por cuenta y no es conciliable.
- Todo movimiento de tesorería genera su asiento contable en un periodo `Abierto` cuyo rango contiene la fecha del movimiento. Ingreso: debe Banco / haber contrapartida. Egreso: debe contrapartida / haber Banco. La contrapartida de un movimiento manual es cualquier cuenta hoja activa distinta de la cuenta contable del banco (incluidas Gasto e Ingreso, p. ej. comisiones e intereses). Los montos son positivos, con máximo 2 decimales.
- Una transferencia entre cuentas bancarias distintas registra un solo asiento (debe cuenta destino / haber cuenta origen) y dos movimientos con el mismo identificador de transferencia: egreso en el origen e ingreso en el destino.
- Los movimientos son inmutables (trigger): no se editan ni se eliminan. Un error se corrige registrando un movimiento manual inverso; no existe anulación de movimientos. La API no ofrece reversa de asientos de tesorería; reversar por SQL un asiento ligado a un movimiento descuadra libros contra banco.
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
