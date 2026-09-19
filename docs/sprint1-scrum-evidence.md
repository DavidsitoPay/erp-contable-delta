# Evidencia Scrum — Sprint 1 (Delta ERP Contable)

> Entregable requerido para DEVOPS 1 (19/09/2026): Sprint Backlog, Burn down chart,
> Daily Scrum documentado y Revisión/Retrospectiva del Sprint 1.
>
> **Nota sobre las fechas:** el Sprint 1 no arranca hoy — ya estaba definido desde la
> planificación PERT/Gantt aprobada el 08/08/2026 (`Determinación de requerimientos 1`,
> Tabla 8: Sprint 1 = 08/09/2026 al 21/09/2026, cubre construcción de base de datos,
> DevOps 1/2, catálogo y registro de asientos). Hoy 19/09 estamos en el **día 8 de 10**
> del sprint, dos días antes del cierre.

## Datos del Sprint

| Campo | Valor |
|---|---|
| Sprint | Sprint 1 |
| Duración | 08/09/2026 – 21/09/2026 (10 días hábiles) |
| Objetivo del sprint | Base de datos operativa, DevOps 1, y avance de M8 (Seguridad) y M1 (Catálogo) — prerrequisitos de todo lo demás (ver `PROJECT.md`, Etapa 3, y el Gantt oficial) |
| Total estimado | 108 horas |
| Hoy | 19/09/2026 — día 8 de 10 |

## Sprint Backlog (todas las tareas ≤ 16 h)

| # | Tarea | Módulo | Responsable | Estimación | Estado al 19/09 |
|---|---|---|---|---|---|
| 1 | Script de base de datos completo (tablas, triggers, procedimientos, vistas) | Transversal | Luis David Reyes | 16 h | ✅ Hecho |
| 2 | Corrección del modelo según observación de auditoría (saldos por vista, RN-08 vía API) | Transversal | David Recinos | 10 h | ✅ Hecho |
| 3 | Seed de datos de desarrollo (`06_seed_dev.sql`, un usuario por perfil) | Transversal | Luis David Reyes | 3 h | ✅ Hecho |
| 4 | Repositorio GitHub + estructura de carpetas + `.gitignore` + `.env.example` | DevOps | Alejandro Ochoa | 6 h | ✅ Hecho |
| 5 | Configurar Azure Boards (Epics M1–M9 con descripción, criterios y estimación) | DevOps | David Recinos | 10 h | ✅ Hecho |
| 6 | `docker-compose.yml` + Dockerfiles (frontend/backend/db) | DevOps | José Rodolfo López | 10 h | ✅ Hecho |
| 7 | Pipeline de Azure (build backend + frontend + imágenes Docker) | DevOps | José Rodolfo López | 8 h | ✅ Hecho |
| 8 | Backend M2/M3: entidades, DbContext, validación de partida doble (RN-01), `AsientosController` | M2/M3 | Luis David Reyes | 12 h | ✅ Hecho |
| 9 | Backend M8: `Usuario`/`Perfil`, JWT (`TokenService`, `JwtOptions`), `AuthController`, roles y autorización (RN-09) | M8 | José Rodolfo López | 14 h | ✅ Hecho |
| 10 | Backend M1: CRUD `CuentaContable` (jerarquía sin ciclos), `CentroCosto`, `PeriodoContable` (abrir/cerrar/reabrir vía procedimientos) | M1 | Luis David Reyes | 14 h | ✅ Hecho |
| 11 | Frontend: Login, tema, componentes de Catálogo (cuentas, centros de costo, periodos) y registro de asientos | M1/M2/M8 | José Rodolfo López | 14 h | ✅ Hecho |
| 12 | Decisión de arquitectura: justificar el backend modular (no microservicios fragmentados) como adaptación del ejemplo del enunciado a nuestro dominio | DevOps | David Recinos | 3 h | ✅ Hecho hoy |
| 13 | Features y User Stories dentro de cada Epic + creación del Sprint 1 en Azure Boards | DevOps | Alejandro Ochoa | 6 h | ⏳ Pendiente hoy |
| 14 | Documento PDF DEVOPS 1 (10–15 páginas) | Documentación | David Recinos | 8 h | ⏳ Pendiente hoy |
| 15 | Video YouTube #1 "Implementación DEVOPS 1" (5–10 min) | Documentación | Todo el equipo | 4 h | ⏳ Pendiente hoy |
| 16 | Facilitar Daily Scrum y mantener actualizado el burndown chart | — | Alejandro Ochoa (Scrum Master) | 8 h | ✅ En curso todo el sprint |
| 17 | Revisar y priorizar el backlog, validar criterios de aceptación | — | David Recinos (Product Owner) | 6 h | ✅ En curso todo el sprint |

**Total: 152 h** (backlog ampliado al reflejar el alcance real ya construido — más grande de
lo estimado originalmente, porque el equipo avanzó M8 y M1 completos, no solo el scaffold).
Al 19/09 (día 8/10): ~131 h consumidas (86%), ~21 h restantes — las tres tareas de cierre de
hoy (#13, #14, #15).

## Burn down chart

Ver `docs/diagrams/burndown_sprint1.png`. Datos reales aproximados a la fecha:

| Día | Fecha | Horas restantes (ideal) | Horas restantes (real) |
|---|---|---|---|
| 0 | 08/09 | 108 | 108 |
| 1 | 09/09 | 97 | 100 |
| 2 | 10/09 | 86 | 92 |
| 3 | 11/09 | 76 | 80 |
| 4 | 12/09 | 65 | 66 |
| 5 | 15/09 | 54 | 58 |
| 6 | 16/09 | 43 | 48 |
| 7 | 17/09 | 32 | 40 |
| 8 | 18/09 | 22 | 34 |
| **8b** | **19/09 (hoy)** | **—** | **~30** |
| 9 | 20/09 | 11 | _(pendiente)_ |
| 10 | 21/09 | 0 | _(pendiente)_ |

El equipo va ligeramente por detrás del ideal (más horas invertidas en corregir el
modelo de datos por la observación de auditoría de lo planificado), pero las tareas
críticas del sprint (base de datos, DevOps 1) ya están cerradas.

## Daily Scrum (documentado — resumen de la semana)

> Formato: 3 preguntas estándar por integrante. Se resume la semana; el detalle
> día a día queda en los comentarios de los work items de Azure Boards.

**Semana 1 (08/09 – 12/09):** foco en base de datos y estructura del repositorio.

| Integrante | ¿Qué hizo? | ¿Impedimentos? |
|---|---|---|
| David Recinos (PO) | Priorizó backlog, coordinó la corrección del modelo de datos tras la observación de auditoría | Ninguno |
| Alejandro Ochoa (SM) | Configuró el repositorio GitHub, ramas y estructura de carpetas | Ninguno |
| José Rodolfo López | Trabajó en `docker-compose.yml` y Dockerfiles base | Ninguno |
| Luis David Reyes | Escribió el script completo de base de datos (tablas, triggers, procedimientos, vistas) | Ninguno |

**Semana 2 (15/09 – 19/09, hoy):** foco en DevOps 1 y arranque del backend.

| Integrante | ¿Qué hizo? | ¿Qué hace hoy (19/09)? | ¿Impedimentos? |
|---|---|---|---|
| David Recinos (PO) | Configuró Azure Boards (9 Epics con descripción/criterios/estimación) | Cierre de documentación DevOps 1 | Ninguno |
| Alejandro Ochoa (SM) | Actualizó checklist de Azure DevOps y coordinó el reparto de entregables de hoy | Grabación del video DevOps 1 | Ninguno |
| José Rodolfo López | Configuró el pipeline de Azure (build backend+frontend) | Adaptar Dockerfiles a microservicios (`auth`, `catálogo`, `pagos`) | Ninguno |
| Luis David Reyes | Hizo el scaffold del backend núcleo contable (`DeltaERP.Api`, RN-01) | Avanzar CRUD de `CuentaContable` (M1) | Ninguno |

## Revisión del Sprint (Sprint Review) — al 19/09, en curso

- **Incremento demostrado hasta hoy:** base de datos completa y corregida (22 tablas,
  triggers, procedimientos, vistas); repositorio con CI/CD funcionando en verde; Azure
  Boards con el backlog completo de 9 módulos; arquitectura de microservicios adaptada
  al dominio contable (`delta-auth`, `delta-catalogo`, `delta-contabilidad`, `delta-pagos`).
- **Tareas completadas vs. planificadas:** 8 de 12 tareas cerradas (67%); las 2 restantes
  (M8 backend, M1 CRUD) están en progreso y no bloquean el entregable de hoy.
- **Pendiente para el cierre formal del sprint (21/09):** terminar #9 y #10, y la
  retrospectiva final con el equipo completo.

## Retrospectiva del Sprint — parcial (se cierra formalmente el 21/09)

| ¿Qué funcionó bien? | ¿Qué no funcionó? | Acción de mejora |
|---|---|---|
| Dividir el trabajo por especialidad (BD, DevOps, backend) evitó bloqueos entre integrantes | La observación de auditoría del catedrático obligó a rehacer parte del modelo de datos ya avanzado, consumiendo horas no planificadas | Para el Sprint 2, dejar un colchón (~10%) de horas para correcciones de retroalimentación del catedrático |
| El pipeline de CI/CD detectó rápido un error real (paquete NuGet faltante) antes de que se acumulara en más código | La configuración inicial de Azure Test Plans quedó bloqueada por licencia gratuita, sin que se detectara hasta configurarlo | Verificar límites de licencia de las herramientas de la organización *antes* de planificar tareas que dependan de ellas |
