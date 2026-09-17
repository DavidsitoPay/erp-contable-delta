# Reglas de negocio — Delta ERP Contable

| Código | Regla |
|---|---|
| RN-01 | Todo asiento contable debe cumplir partida doble (suma de cargos = suma de abonos) antes de registrarse. |
| RN-02 | No se permite registrar, modificar ni eliminar asientos en un periodo contable cerrado. |
| RN-03 | Un asiento solo puede reversarse mediante un asiento de reversión; nunca se elimina físicamente. |
| RN-04 | Toda factura CxC/CxP debe asociarse a un cliente/proveedor existente en el catálogo. |
| RN-05 | La aplicación de un pago a una factura no puede exceder el saldo pendiente de esa factura. |
| RN-06 | Toda transacción se registra en la moneda funcional (GTQ); operaciones en otra moneda se convierten con el tipo de cambio vigente. |
| RN-07 | El cálculo de impuestos (IVA/ISR) se aplica según los parámetros tributarios configurados, no manualmente. |
| RN-08 | Toda operación relevante genera un registro en `BitacoraAuditoria` (usuario, fecha, acción), **insertado por la API**, nunca por trigger (ver architecture.md). |
| RN-09 | El acceso a cada módulo/operación depende del perfil y permisos del usuario autenticado. |
| RN-10 | La conciliación bancaria contrasta movimientos del sistema contra el estado de cuenta; solo personal autorizado la finaliza. |
| RN-11 | La carga inicial (M9) valida consistencia antes de integrarse a la base productiva. |
| RN-12 | `saldo_pendiente` de `DocumentoCxC`/`DocumentoCxP` es un campo derivado y de solo lectura para la API; se recalcula por trigger a partir de `AplicacionPagoCliente`/`AplicacionPagoProveedor` = `monto_total − SUM(monto_aplicado)`. Ningún endpoint debe permitir su edición directa. |

## Nota sobre saldos consolidados (CuentaBancaria)

El campo `saldo` de `CuentaBancaria` no debe ser editable libremente desde la API. El
saldo en tiempo real se calcula bajo demanda a partir de `MovimientoTesoreria`
(`saldo_inicial + SUM(Ingresos) − SUM(Egresos)`), y el saldo de cierre de cada periodo
se consolida de forma inmutable en la tabla `SaldoCuentaPeriodo` (ver data-dictionary.md).
