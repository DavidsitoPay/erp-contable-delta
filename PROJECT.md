# Delta ERP Contable (DEC) — Proyecto de Seminario de Tecnologías de Información 2026

Sistema ERP interno de control contable para la empresa Delta, desarrollado bajo
metodología Scrum como parte del curso Seminario de Tecnologías de Información (UMG).
Fase 1: uso interno. Fase 2 (futura): comercialización SaaS B2B a PyMEs guatemaltecas.

## Equipo

| Rol Scrum | Integrante |
|---|---|
| Product Owner / Líder de grupo | David Edgar Recinos García |
| Scrum Master | Alejandro Rocael Ochoa Pérez |
| Equipo de desarrollo | José Rodolfo López y López |
| Equipo de desarrollo | Luis David Reyes Mijangos |

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Frontend | React 18 + Vite (SPA), desplegado en Vercel |
| Backend / API | C# con ASP.NET Core 8 (API REST) + EF Core/Npgsql, imagen Docker desplegada en Azure Container Apps |
| Base de datos | PostgreSQL 16 en Neon (ramas `production` y `development`) |
| CI/CD | GitHub Actions (`backend-build-check`, `sonarcloud-analysis`, `backend-deploy`, `frontend-alias`) |
| Calidad | SonarCloud (análisis, cobertura y duplicación) |
| Gestión de proyecto | Azure Boards y Azure Test Plans |
| Control de versiones | GitHub |
| Editor de código | Visual Studio Code |

## Artefactos ya entregados (referencia)

- `docs/architecture.md` — Arquitectura de 3 capas
- `docs/data-dictionary.md` — Diccionario de datos completo, 28 tablas
- `docs/business-rules.md` — Reglas de negocio RN-01 a RN-16, con la columna "Dónde se aplica"
- `docs/diagrams/` — Diagramas de clases (1/2 y 2/2), componentes, secuencia, contexto, arquitectura

## Módulos funcionales (Product Backlog de alto nivel)

| Código | Módulo | Prioridad | Estado |
|---|---|---|---|
| M1 | Catálogo y parametrización contable | Alta | Implementado (cuentas contables, centros de costo, periodos) |
| M2 | Registro de transacciones | Alta | Implementado (registro de asientos con RN-01/RN-02/RN-08 y reversión formal RN-03; la consulta de asientos se hace desde el libro diario) |
| M3 | Libros y auxiliares | Alta | Implementado (balance de saldos, libro diario y libro mayor jerárquicos, calculados en tiempo real) |
| M4 | Cuentas por cobrar | Alta | Implementado (facturas con asiento automático, notas de crédito, recibos y aplicación de pagos con límite RN-05) |
| M5 | Cuentas por pagar | Alta | Implementado (facturas con asiento automático, notas de crédito, pagos y aplicación con límite RN-05) |
| M6 | Tesorería y bancos | Media | Implementado (cuentas bancarias, movimientos, transferencias y conciliación bancaria) |
| M7 | Reportes y estados financieros | Alta | Implementado (balance general y estado de resultados) |
| M8 | Seguridad y auditoría | Alta | Implementado (login, JWT, roles, bitácora de auditoría) |
| M9 | Carga inicial e importación | Media | Diseñado |
| M10 | Cumplimiento fiscal y multimoneda | Alta | En curso (parámetros fiscales e IVA, registro de DTE de FEL, notas de crédito, libro de compras y ventas implementados; retenciones ISR/IVA y multimoneda con tipo de cambio de Banguat pendientes) |

## Etapas del ciclo de desarrollo (SDLC) aplicadas al proyecto

Cada etapa se mapea a un entregable del curso y a una carpeta del repositorio.

### Etapa 0 — Fundamentos (ya completada)
Diagnóstico, modelo de negocio, requerimientos, UML, modelo relacional, diccionario de
datos y arquitectura. Ver `/docs`.

### Etapa 1 — DevOps 1 (completada)
Repositorio e infraestructura base.

- [x] Repositorio Git en GitHub con estructura por capas
- [x] `devops/Dockerfile.backend` — imagen del backend para Azure Container Apps
- [x] Proyecto backend (ASP.NET Core Web API) con capas Api / Domain / Infrastructure
- [x] Proyecto frontend (React + Vite)
- [x] Azure Boards: Product Backlog con los módulos como Epics (E1–E10), ver `docs/backlog-features-historias.md`
- [x] Azure Test Plans: plan de pruebas, ver `docs/plan-de-pruebas-inicial.md`
- [x] Pipelines en GitHub Actions (`.github/workflows/`)

### Etapa 2 — Base de datos (completada)
Scripts numerados en `database/`, aplicados en orden por `database/run_migrations.sh` (cada script con `--single-transaction`). Un script ya aplicado no se edita: las correcciones van en un archivo nuevo.

- [x] `01_tables.sql` — DDL base
- [x] `02_functions.sql`, `03_triggers.sql`, `04_procedures.sql`, `05_views.sql` — funciones, disparadores (partida doble, periodo cerrado, no eliminación de asientos, límites de pago, inmutabilidad de auditoría y saldos), procedimientos y vistas
- [x] `06_seed_dev.sql` — usuarios de prueba por perfil
- [x] `07` a `13` — perfil autorizado, correcciones de balance y cierre, tesorería, balance jerárquico, reportes, reversa de asientos y parámetros fiscales e IVA (M10)
- [x] `database/demo/` — datos de demostración

### Etapa 3 — Backend (completada para M1–M8; M10 en curso)
Orden seguido, según la dependencia entre módulos:

1. ✅ **M8 Seguridad** — `AuthController`, JWT, roles (`Administrador`, `Contador`, `Vendedor`, `Tecnico`), bitácora de auditoría desde la API.
2. ✅ **M1 Catálogo** — cuentas contables, centros de costo y periodos.
3. ✅ **M2 Transacciones** — registro de asientos (RN-01/RN-02/RN-08) y reversión (RN-03, `POST api/asientos/{id}/reversar`). No existe `GET /api/asientos`: la consulta se hace por el libro diario.
4. ✅ **M3 Libros** — balance de saldos, libro diario y libro mayor jerárquicos, calculados en tiempo real.
5. ✅ **M4 / M5 CxC y CxP** — contrapartes, facturas con asiento automático, notas de crédito, y pagos con límite RN-05 en la API y en el trigger.
6. ✅ **M6 Tesorería** — cuentas bancarias, movimientos, transferencias y conciliación (RN-10).
7. ✅ **M7 Reportes** — balance general y estado de resultados.
8. **M10 Cumplimiento fiscal** — en curso: configuración fiscal, catálogo de impuestos, IVA en facturas, DTE de FEL, notas de crédito y libro fiscal implementados; retenciones y multimoneda pendientes.
9. **M9 Importación** — diseñado, no iniciado.

### Etapa 4 — Frontend
Por módulo, cada pantalla consume un endpoint ya probado del backend.

✅ Implementado: login, catálogo de cuentas contables, centros de costo, periodos contables, registro y reversión de asientos, balance de saldos, libro diario y libro mayor, clientes/proveedores, facturación y pagos de CxC y CxP, tesorería (cuentas bancarias, movimientos, conciliación), reportes (balance general, estado de resultados), configuración fiscal, catálogo de impuestos y libro fiscal de compras y ventas. Tema sobrio con modo oscuro conmutable.

### Etapa 5 — DevOps 2 (completada)
Integración continua en GitHub Actions: pruebas del backend (xUnit contra un servicio PostgreSQL) y del frontend (Vitest), SonarCloud con puerta de calidad, cobertura mínima en el código modificado y control de duplicación. Plan de pruebas en `docs/plan-de-pruebas-inicial.md`.

### Etapa 6 — Manual de usuario y cierre
Documentación de usuario final, código terminado, documento integrado y presentación.

## Reglas de negocio críticas a implementar primero (ver docs/business-rules.md)

- **RN-01:** todo asiento contable debe cumplir partida doble antes de registrarse.
- **RN-02:** no se permite modificar asientos en un periodo cerrado.
- **RN-08:** toda operación relevante genera un registro en `BitacoraAuditoria`, **insertado por la API** (no por trigger, según corrección de auditoría — ver `docs/architecture.md`).
- **RN-05:** la aplicación de un pago a una factura no puede exceder el saldo pendiente de esa factura (`trg_limite_pago_cxc` / `trg_limite_pago_cxp`).
- **Nota sobre saldos:** `saldo_pendiente` (CxC/CxP), `saldo` (CuentaContable, CuentaBancaria) **no existen como columnas editables** — se calculan en vistas (`vw_saldodocumentocxc`, `vw_saldodocumentocxp`, `vw_balance_saldos`, `vw_saldocuentabancaria`). La única excepción es `SaldoCuentaPeriodo`, escrita una sola vez al cierre y luego inmutable.

## Convenciones del repositorio

- Ramas: `main` (estable; despliega backend y migra la base `production`) y `dev` (integración). El trabajo llega a `main` por Pull Request desde `dev`.
- Commits: estilo Conventional Commits en español, `tipo(ámbito): descripción` (ej. `feat(asientos): reversión de asientos desde la API (RN-03)`).
- Los módulos funcionales se llaman M1–M10 en el proyecto y E1–E10 (Epics) en Azure Boards.
