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
| Frontend | React (SPA) |
| Backend / API | C# con ASP.NET Core (API REST) |
| Base de datos | PostgreSQL |
| Contenedores | Docker / docker-compose |
| CI/CD | Azure Pipelines |
| Gestión de proyecto | Azure Boards |
| Control de versiones | GitHub |
| Editor de código | Visual Studio Code |

## Artefactos ya entregados (referencia)

- `docs/architecture.md` — Arquitectura de 3 capas (entregado 05/09)
- `docs/data-dictionary.md` — Diccionario de datos completo, 22 tablas (entregado 05/09)
- `docs/business-rules.md` — Reglas de negocio RN-01 a RN-11, con la columna "Dónde se aplica" (entregado 15/08 + corrección oficial v2 12/09)
- `docs/diagrams/` — Diagramas de clases (1/2 y 2/2), componentes, secuencia, contexto, arquitectura

## Módulos funcionales (Product Backlog de alto nivel)

| Código | Módulo | Prioridad | Estado |
|---|---|---|---|
| M1 | Catálogo y parametrización contable | Alta | Implementado (cuentas contables, centros de costo, periodos — CRUD + backend) |
| M2 | Registro de transacciones | Alta | Implementado (registro de asientos con RN-01/RN-02/RN-08; falta listado `GET /api/asientos`) |
| M3 | Libros y auxiliares | Alta | Diseñado |
| M4 | Cuentas por cobrar | Alta | Diseñado |
| M5 | Cuentas por pagar | Alta | Diseñado |
| M6 | Tesorería y bancos | Media | Diseñado |
| M7 | Reportes y estados financieros | Alta | Diseñado |
| M8 | Seguridad y auditoría | Alta | Implementado (login, JWT, roles, usuarios seed) |
| M9 | Carga inicial e importación | Media | Diseñado |

## Etapas del ciclo de desarrollo (SDLC) aplicadas al proyecto

Cada etapa se mapea a un entregable del curso y a una carpeta del repositorio.

### Etapa 0 — Fundamentos (ya completada)
Diagnóstico, modelo de negocio, requerimientos, UML, modelo relacional, diccionario de
datos y arquitectura. Ver `/docs`.

### Etapa 1 — DevOps 1 (entregable 19/09) ← **estamos aquí**
Objetivo: dejar el repositorio y la infraestructura base listos para empezar a programar.

- [x] Repositorio Git inicializado con esta estructura (`git init`, primer commit, push a GitHub)
- [x] `devops/docker-compose.yml` — 3 servicios: `frontend`, `backend`, `db` (igual al diagrama de arquitectura)
- [x] `devops/Dockerfile.backend` y `devops/Dockerfile.frontend`
- [x] Proyecto backend inicial (ASP.NET Core Web API) con estructura por capas (Api / Domain / Infrastructure) — ya con M1/M2/M8 funcionando, no solo el esqueleto
- [x] Proyecto frontend inicial (React) con estructura de carpetas — ya con login, catálogo y registro de asientos funcionando, no solo el esqueleto
- [ ] Azure Boards: crear el Product Backlog con los 9 módulos (M1–M9) como Epics/Features — pendiente, paso manual en el portal (ver `docs/azure-devops-checklist.md`)
- [ ] Azure Repos o espejo del repo de GitHub conectado a Azure Pipelines — pendiente, paso manual en el portal
- [x] `azure-pipelines.yml` — pipeline mínimo de build (compilar backend + frontend)
- [ ] Azure Test Plans: crear el plan de pruebas vacío (se llena en DevOps 2) — pendiente, paso manual en el portal

### Etapa 2 — Base de datos (entregable 12/09, en paralelo/ya vencido)
- [x] `database/01_tables.sql` — DDL de las 22 tablas del diccionario de datos
- [x] `database/02_functions.sql` — funciones de apoyo de propósito general (ej. validación de consistencia de M9 Importación)
- [x] `database/03_triggers.sql` — disparadores (partida doble, bloqueo de periodo cerrado, no eliminación de asientos, límites de pago, e inmutabilidad de `BitacoraAuditoria` y `SaldoCuentaPeriodo`)
- [x] `database/04_procedures.sql` — procedimientos almacenados (cierre de periodo)
- [x] `database/05_views.sql` — vistas (ej. saldo de cuenta contable en tiempo real)
- [x] `database/06_seed_dev.sql` — usuarios de desarrollo (admin, contador, vendedor, técnico) para probar el login localmente

### Etapa 3 — Backend (núcleo contable primero, por dependencia de datos)
Orden recomendado, siguiendo la dependencia real entre módulos (ver diagrama de componentes):

1. ✅ **M8 Seguridad** (Usuario, Perfil, autenticación por token) — todo lo demás depende de esto. Implementado: `AuthController`, JWT, roles (`Administrador`, `Contador`, `Vendedor`, `Tecnico`).
2. ✅ **M1 Catálogo** (CuentaContable, CentroCosto, PeriodoContable). Implementado: CRUD completo de los tres controladores, con validación de jerarquía y naturaleza contable.
3. ✅ **M2 Transacciones** (AsientoContable, LineaAsiento + validación de partida doble RN-01). Implementado: `AsientosController.Registrar` con RN-01/RN-02/RN-08 y monto calculado server-side. **Pendiente:** endpoint de listado/consulta (`GET /api/asientos`).
4. **M3 Libros** (reportes de LineaAsiento) — no iniciado
5. **M4 / M5 CxC y CxP** (DocumentoCxC/CxP + aplicaciones de pago) — no iniciado
6. **M6 Tesorería** (CuentaBancaria, MovimientoTesoreria, conciliación) — no iniciado
7. **M7 Reportes** — no iniciado
8. **M9 Importación** — no iniciado

### Etapa 4 — Frontend
En paralelo al backend, por módulo: login/autenticación → catálogo → registro de
transacciones → CxC/CxP → reportes. Cada pantalla consume un endpoint ya probado del backend.

✅ Implementado: login, catálogo de cuentas contables (búsqueda, filtro por tipo, orden por
columna, jerarquía con sangría), centros de costo, periodos contables, registro de asientos.
Rediseño visual completo (tema sobrio, paleta de marca) con modo oscuro conmutable persistido
por navegador. **Pendiente conocido:** las pantallas de catálogo no ocultan aún sus botones de
edición a perfiles sin permiso (el backend sí devuelve 403; es solo inconsistencia de UX).

### Etapa 5 — DevOps 2 (entregable 26/09)
Integración continua real, plan de pruebas completo, pruebas automatizadas de las
reglas de negocio críticas (RN-01, RN-02, RN-05, RN-08).

### Etapa 6 — Manual de usuario (03/10) y cierre (10/10 – 24/10)
Documentación de usuario final, código terminado, documento integrado y presentación.

## Reglas de negocio críticas a implementar primero (ver docs/business-rules.md)

- **RN-01:** todo asiento contable debe cumplir partida doble antes de registrarse.
- **RN-02:** no se permite modificar asientos en un periodo cerrado.
- **RN-08:** toda operación relevante genera un registro en `BitacoraAuditoria`, **insertado por la API** (no por trigger, según corrección de auditoría — ver `docs/architecture.md`).
- **RN-05:** la aplicación de un pago a una factura no puede exceder el saldo pendiente de esa factura (`trg_limite_pago_cxc` / `trg_limite_pago_cxp`).
- **Nota sobre saldos:** `saldo_pendiente` (CxC/CxP), `saldo` (CuentaContable, CuentaBancaria) **no existen como columnas editables** — se calculan en vistas (`vw_saldodocumentocxc`, `vw_saldodocumentocxp`, `vw_balance_saldos`, `vw_saldocuentabancaria`). La única excepción es `SaldoCuentaPeriodo`, escrita una sola vez al cierre y luego inmutable.

## Convenciones del repositorio

- Ramas: `main` (estable), `develop` (integración), `feature/<módulo>-<descripción>` por historia de usuario.
- Commits: `[M#] descripción corta` (ej. `[M2] agrega validación de partida doble`).
- Cada Pull Request a `develop` debe referenciar el work item de Azure Boards correspondiente.
