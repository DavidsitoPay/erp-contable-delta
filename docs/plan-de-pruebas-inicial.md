# Plan de pruebas inicial — Test Cases (Azure Boards)

> Nota de licencia: la organización está en nivel gratuito de Azure DevOps, que no
> incluye el hub completo de "Test Plans" (crear planes/suites formales requiere
> licencia "Basic + Test Plans"). Sí es gratis crear work items tipo **Test Case**
> (con pasos Action/Expected Result) y enlazarlos a las Historias de Usuario vía el
> link type "Tests" — eso es lo que se usa aquí como plan de pruebas inicial.
>
> Para cargarlo: `New Work Item → Test Case`, título, pasos en la tabla Steps
> (Action / Expected Result), `Related Work → Add link → Existing item → Tests`
> buscando la historia por palabra clave del título, e Iteration ajustada al sprint
> correspondiente a la fecha indicada.
>
> Cobertura: 18 Test Cases, uno por cada Historia de Usuario (En-F1-Hn) del backlog
> completo en `docs/backlog-features-historias.md`.

| Test Case | Pasos (Action → Expected Result) | Parent | Fecha objetivo |
|---|---|---|---|
| TC-01 - CRUD CuentaContable | 1) Crear cuenta válida (código, tipo Activo, naturaleza Deudora) → `201 Created`. 2) Crear cuenta padre generando ciclo → `400`. 3) Crear cuenta con tipo/naturaleza inconsistentes (ej. Pasivo+Deudora) → `400`. | E1-F1-H1 | 19/09/2026 |
| TC-02 - CRUD CentroCosto y cierre de PeriodoContable | 1) Crear CentroCosto válido → `201`. 2) Cerrar un periodo con perfil autorizado → `200`, estado=Cerrado. 3) Reabrir el periodo con perfil NO autorizado → `403`. | E1-F1-H2 | 19/09/2026 |
| TC-03 - Modelo de datos EF Core (AsientoContable/LineaAsiento) | 1) Ejecutar la migración/actualización contra PostgreSQL → tablas creadas sin error. 2) Comparar columnas de `asientocontable` en la BD vs. `01_tables.sql` → coinciden. | E2-F1-H1 | 19/09/2026 |
| TC-04 - Rechazar asiento descuadrado (RN-01) | 1) `POST /api/Asientos` descuadrado → `400`, no se registra. 2) `POST /api/Asientos` balanceado → `201`, se registra. | E2-F1-H2 | 19/09/2026 |
| TC-05 - Balance de saldos | 1) `GET` balance de saldos con asientos confirmados → saldo correcto según naturaleza deudora/acreedora. 2) Incluir un asiento en Borrador → no debe afectar el saldo. | E3-F1-H1 | 26/09/2026 |
| TC-06 - Libro diario y libro mayor | 1) `GET` libro diario de un periodo → asientos ordenados por fecha/número. 2) `GET` libro mayor de una cuenta → movimientos agrupados con saldo acumulado. | E3-F1-H2 | 26/09/2026 |
| TC-07 - CRUD DocumentoCxC | 1) `POST` documento con `cliente_id` inexistente → `400` (RN-04). 2) `POST` con cliente válido y líneas → `201`, monto total calculado. | E4-F1-H1 | 02/10/2026 |
| TC-08 - Aplicación de pagos CxC (RN-05) | 1) Aplicar pago mayor al saldo pendiente → `400`. 2) Aplicar pago válido → `201`, saldo se refleja en `vw_saldodocumentocxc`. | E4-F1-H2 | 02/10/2026 |
| TC-09 - CRUD DocumentoCxP | 1) `POST` documento con `proveedor_id` inexistente → `400` (RN-04). 2) `POST` con proveedor válido → `201`. | E5-F1-H1 | 05/10/2026 |
| TC-10 - Pagos a proveedor (RN-05) | 1) Aplicar pago mayor al saldo pendiente → `400`. 2) Aplicar pago válido → `201`, saldo actualizado. | E5-F1-H2 | 05/10/2026 |
| TC-11 - CRUD CuentaBancaria y movimientos | 1) `POST` cuenta bancaria válida → `201`. 2) Registrar un `MovimientoTesoreria` de Ingreso → el saldo en `vw_saldocuentabancaria` aumenta. | E6-F1-H1 | 10/10/2026 |
| TC-12 - Conciliación bancaria (RN-10) | 1) Finalizar conciliación con perfil NO autorizado → `403`. 2) Finalizar con perfil autorizado → `200`, estado=Conciliado. | E6-F1-H2 | 10/10/2026 |
| TC-13 - Balance general | 1) `GET` balance general → clasifica cuentas por Activo/Pasivo/Capital. 2) Consultar un periodo cerrado → usa `SaldoCuentaPeriodo`, no recálculo en vivo. | E7-F1-H1 | 14/10/2026 |
| TC-14 - Estado de resultados | 1) `GET` estado de resultados de un periodo → solo incluye cuentas Ingreso/Gasto de ese periodo. | E7-F1-H2 | 14/10/2026 |
| TC-15 - Login con credenciales válidas e inválidas | 1) Login correcto → `200` + token JWT. 2) Login con contraseña incorrecta → `401`. | E8-F1-H1 | 19/09/2026 |
| TC-16 - Autorización por rol y auditoría | 1) Endpoint restringido sin rol correcto → `403`. 2) Acción crítica autorizada (ej. registrar asiento) → genera registro en `BitacoraAuditoria`. 3) `UPDATE`/`DELETE` directo sobre `BitacoraAuditoria` (SQL) → rechazado por trigger. | E8-F1-H2 | 19/09/2026 |
| TC-17 - Validación de archivo CSV (RN-11) | 1) Importar CSV con una fila inválida → error reportado con fila/columna, nada se inserta. 2) Importar CSV válido → todas las filas se insertan. | E9-F1-H1 | 19/10/2026 |
| TC-18 - Inserción transaccional (todo o nada) | 1) Importar archivo donde falla una fila intermedia → ninguna fila queda insertada (rollback completo). 2) Importación exitosa → queda registrada en `BitacoraAuditoria`. | E9-F1-H2 | 19/10/2026 |
