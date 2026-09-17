# Reglas de negocio — Delta ERP Contable

> Actualizado según el documento oficial v2 (12/09/2026). Se agrega la columna
> "Dónde se aplica" para dejar explícito qué reglas viven en la base de datos
> (trigger/procedimiento) y cuáles en la API — ver docs/architecture.md.

| Código | Regla | Dónde se aplica |
|---|---|---|
| RN-01 | Todo asiento contable debe cumplir partida doble (suma de débitos = suma de créditos) antes de confirmarse. | Trigger (`trg_validar_partida_doble`) |
| RN-02 | No se permite registrar, modificar ni eliminar líneas de asiento de un periodo contable cerrado. | Trigger (`trg_bloquear_periodo_cerrado_linea`) |
| RN-03 | Un asiento solo puede reversarse mediante un asiento de reversión; nunca se elimina físicamente. | Trigger (bloquea DELETE) + Procedimiento (`sp_reversar_asiento`) |
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
  pero inmutable (trigger bloquea UPDATE/DELETE) y escrito una sola vez, desde
  `sp_cerrar_periodo`, como fotografía del cierre. No se recalcula con cada
  transacción como intentaba hacer un trigger en la versión anterior de este
  documento — la corrección oficial usa vistas para el saldo en tiempo real y
  reserva la tabla solo para el cierre.
