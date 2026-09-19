# Plan de pruebas inicial — Test Cases (Azure Boards)

> Nota de licencia: la organización está en nivel gratuito de Azure DevOps, que no
> incluye el hub completo de "Test Plans" (crear planes/suites formales requiere
> licencia "Basic + Test Plans"). Sí es gratis crear work items tipo **Test Case**
> (con pasos Action/Expected Result) y enlazarlos a las Historias de Usuario vía el
> link type "Tests" — eso es lo que se usa aquí como plan de pruebas inicial.
>
> Para cargarlo: `New Work Item → Test Case`, título, **Descripción** en el campo
> correspondiente, pasos en la tabla Steps (Action / Expected Result), **Priority**
> en Details, `Related Work → Add link → Existing item → Tests` buscando la historia
> por palabra clave del título, e Iteration ajustada al sprint de la fecha indicada.
>
> Cobertura: 18 Test Cases, uno por cada Historia de Usuario (En-F1-Hn) del backlog
> completo en `docs/backlog-features-historias.md`. Priority 1–4 (1=más alto).

| Test Case | Descripción | Pasos (Action → Expected Result) | Parent | Priority | Fecha objetivo |
|---|---|---|---|---|---|
| TC-01 - CRUD CuentaContable | Verifica que el catálogo de cuentas solo acepte jerarquías válidas y consistencia entre tipo y naturaleza. | 1) Crear cuenta válida (código, tipo Activo, naturaleza Deudora) → `201 Created`. 2) Crear cuenta padre generando ciclo → `400`. 3) Crear cuenta con tipo/naturaleza inconsistentes (ej. Pasivo+Deudora) → `400`. | E1-F1-H1 | 1 | 19/09/2026 |
| TC-02 - CRUD CentroCosto y cierre de PeriodoContable | Verifica que los centros de costo se administren libremente y que el cierre/reapertura de periodos respete el control de permisos. | 1) Crear CentroCosto válido → `201`. 2) Cerrar un periodo con perfil autorizado → `200`, estado=Cerrado. 3) Reabrir el periodo con perfil NO autorizado → `403`. | E1-F1-H2 | 1 | 19/09/2026 |
| TC-03 - Modelo de datos EF Core (AsientoContable/LineaAsiento) | Verifica que el modelo de datos de EF Core esté correctamente mapeado contra el esquema real de PostgreSQL. | 1) Ejecutar la migración/actualización contra PostgreSQL → tablas creadas sin error. 2) Comparar columnas de `asientocontable` en la BD vs. `01_tables.sql` → coinciden. | E2-F1-H1 | 2 | 19/09/2026 |
| TC-04 - Rechazar asiento descuadrado (RN-01) | Verifica que el sistema nunca registre un asiento contable que no cumpla partida doble. | 1) `POST /api/Asientos` descuadrado → `400`, no se registra. 2) `POST /api/Asientos` balanceado → `201`, se registra. | E2-F1-H2 | 1 | 19/09/2026 |
| TC-05 - Balance de saldos | Verifica que el balance de saldos refleje correctamente la naturaleza de cada cuenta y excluya asientos no confirmados. | 1) `GET` balance de saldos con asientos confirmados → saldo correcto según naturaleza deudora/acreedora. 2) Incluir un asiento en Borrador → no debe afectar el saldo. | E3-F1-H1 | 2 | 26/09/2026 |
| TC-06 - Libro diario y libro mayor | Verifica que los libros diario y mayor se generen correctamente a partir de los asientos confirmados. | 1) `GET` libro diario de un periodo → asientos ordenados por fecha/número. 2) `GET` libro mayor de una cuenta → movimientos agrupados con saldo acumulado. | E3-F1-H2 | 2 | 26/09/2026 |
| TC-07 - CRUD DocumentoCxC | Verifica que toda factura de cliente quede asociada a un cliente existente y calcule su monto correctamente. | 1) `POST` documento con `cliente_id` inexistente → `400` (RN-04). 2) `POST` con cliente válido y líneas → `201`, monto total calculado. | E4-F1-H1 | 1 | 02/10/2026 |
| TC-08 - Aplicación de pagos CxC (RN-05) | Verifica que la aplicación de pagos de clientes nunca exceda el saldo pendiente de la factura. | 1) Aplicar pago mayor al saldo pendiente → `400`. 2) Aplicar pago válido → `201`, saldo se refleja en `vw_saldodocumentocxc`. | E4-F1-H2 | 1 | 02/10/2026 |
| TC-09 - CRUD DocumentoCxP | Verifica que toda factura de proveedor quede asociada a un proveedor existente y calcule su monto correctamente. | 1) `POST` documento con `proveedor_id` inexistente → `400` (RN-04). 2) `POST` con proveedor válido → `201`. | E5-F1-H1 | 1 | 05/10/2026 |
| TC-10 - Pagos a proveedor (RN-05) | Verifica que la aplicación de pagos a proveedores nunca exceda el saldo pendiente de la factura. | 1) Aplicar pago mayor al saldo pendiente → `400`. 2) Aplicar pago válido → `201`, saldo actualizado. | E5-F1-H2 | 1 | 05/10/2026 |
| TC-11 - CRUD CuentaBancaria y movimientos | Verifica que las cuentas bancarias y sus movimientos actualicen correctamente el saldo calculado. | 1) `POST` cuenta bancaria válida → `201`. 2) Registrar un `MovimientoTesoreria` de Ingreso → el saldo en `vw_saldocuentabancaria` aumenta. | E6-F1-H1 | 2 | 10/10/2026 |
| TC-12 - Conciliación bancaria (RN-10) | Verifica que solo un perfil autorizado pueda finalizar una conciliación bancaria. | 1) Finalizar conciliación con perfil NO autorizado → `403`. 2) Finalizar con perfil autorizado → `200`, estado=Conciliado. | E6-F1-H2 | 2 | 10/10/2026 |
| TC-13 - Balance general | Verifica que el balance general clasifique correctamente las cuentas y use saldos consolidados en periodos cerrados. | 1) `GET` balance general → clasifica cuentas por Activo/Pasivo/Capital. 2) Consultar un periodo cerrado → usa `SaldoCuentaPeriodo`, no recálculo en vivo. | E7-F1-H1 | 1 | 14/10/2026 |
| TC-14 - Estado de resultados | Verifica que el estado de resultados incluya únicamente las cuentas de ingreso y gasto del periodo correspondiente. | 1) `GET` estado de resultados de un periodo → solo incluye cuentas Ingreso/Gasto de ese periodo. | E7-F1-H2 | 1 | 14/10/2026 |
| TC-15 - Login con credenciales válidas e inválidas | Verifica que el inicio de sesión otorgue un token válido solo con credenciales correctas. | 1) Login correcto → `200` + token JWT. 2) Login con contraseña incorrecta → `401`. | E8-F1-H1 | 1 | 19/09/2026 |
| TC-16 - Autorización por rol y auditoría | Verifica el control de acceso por rol y que toda acción crítica quede registrada en la bitácora de auditoría de forma inmutable. | 1) Endpoint restringido sin rol correcto → `403`. 2) Acción crítica autorizada (ej. registrar asiento) → genera registro en `BitacoraAuditoria`. 3) `UPDATE`/`DELETE` directo sobre `BitacoraAuditoria` (SQL) → rechazado por trigger. | E8-F1-H2 | 1 | 19/09/2026 |
| TC-17 - Validación de archivo CSV (RN-11) | Verifica que un archivo CSV con errores se rechace por completo antes de insertar cualquier dato. | 1) Importar CSV con una fila inválida → error reportado con fila/columna, nada se inserta. 2) Importar CSV válido → todas las filas se insertan. | E9-F1-H1 | 2 | 19/10/2026 |
| TC-18 - Inserción transaccional (todo o nada) | Verifica que la importación de datos sea atómica (todo o nada) y quede registrada en auditoría. | 1) Importar archivo donde falla una fila intermedia → ninguna fila queda insertada (rollback completo). 2) Importación exitosa → queda registrada en `BitacoraAuditoria`. | E9-F1-H2 | 2 | 19/10/2026 |
