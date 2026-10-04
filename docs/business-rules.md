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
| RN-10 | La conciliación bancaria contrasta movimientos del sistema contra el estado de cuenta; solo personal autorizado la finaliza. | Procedimiento (`sp_finalizar_conciliacion`, verifica perfil) |
| RN-11 | La carga inicial (M9) valida consistencia antes de integrarse a la base productiva. | API + funciones de soporte (`02_functions.sql`) |

## Saldos: nunca columnas editables

Ninguna entidad almacena un saldo como columna libremente editable:

- `CuentaContable` — sin columna `saldo`; se deriva de `vw_balance_saldos`.
- `CuentaBancaria` — sin columna `saldo`; se deriva de `vw_saldocuentabancaria`.
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
