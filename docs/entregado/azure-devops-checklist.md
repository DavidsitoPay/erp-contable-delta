# Checklist — Configuración de Azure DevOps (DevOps 1)

Estos pasos se hacen manualmente en [dev.azure.com](https://dev.azure.com), no se pueden
automatizar desde el repositorio. Estado actualizado al 19/09/2026, y revalidado
directamente contra el proyecto real (`az boards`/`az pipelines`, organización
`drecinosg2`, proyecto `erp-contable-delta`) el 24/09/2026.

## 1. Organización y proyecto
- [x] Organización creada: `drecinosg2`
- [x] Proyecto creado: `erp-contable-delta`

## 2. Azure Repos
- [x] **Decisión tomada:** GitHub (`DavidsitoPay/erp-contable-delta`) es la fuente única del
      código. Azure Pipelines y Azure Boards apuntan directo a GitHub — no se usa Azure Repos
      como copia del código, para no mantener dos repositorios sincronizados manualmente
      (ver decisión y justificación en la conversación de configuración del 17/09).
- [x] Política de rama en `main` de GitHub: requerir Pull Request antes de fusionar —
      activada el 24/09/2026 vía `gh api .../branches/main/protection`
      (`required_pull_request_reviews.required_approving_review_count: 0`,
      `enforce_admins: true` para que la regla aplique incluso al dueño del
      repositorio, sin checks de estado obligatorios porque los workflows de GitHub
      Actions solo corren sobre ciertas rutas y un check "esperado" que nunca se
      dispara bloquearía la fusión indefinidamente). Verificado con una lectura
      posterior de la misma API: la protección quedó activa. El repositorio ya tenía
      6 Pull Requests reales previos a esta regla (4 fusionados: #2, #3, #5, #6; 2
      cerrados sin fusionar: #1, #4).

## 3. Azure Boards
- [x] **Epics creados** — uno por cada módulo (M1–M9), IDs 133–141, cada uno con
      Descripción, Acceptance Criteria, Priority (Alta=1 / Media=2) y Effort (story points)
      ya completados:

  | Epic | Módulo | Priority | Effort |
  |---|---|---|---|
  | 133 | M1 Catálogo | 1 | 13 |
  | 134 | M2 Transacciones | 1 | 21 |
  | 135 | M3 Libros | 1 | 8 |
  | 136 | M4 CxC | 1 | 13 |
  | 137 | M5 CxP | 1 | 13 |
  | 138 | M6 Tesorería | 2 | 13 |
  | 139 | M7 Reportes | 1 | 13 |
  | 140 | M8 Seguridad | 1 | 13 |
  | 141 | M9 Importación | 2 | 8 |

- [x] Descomponer cada Epic en **Features** — confirmado vía `az boards query`: 9
      Features, una por Epic (IDs 142–166, ej. E2-F1 "Registro y reversión de asientos
      contables (partida doble)").
- [x] Descomponer cada Feature en **User Stories** / Product Backlog Items con estimación
      en horas (máx. 16h/tarea) — confirmado: 18 PBIs (IDs 143–168), dos por Feature,
      todos con `Microsoft.VSTS.Scheduling.Effort` cargado (entre 4h y 8h, siempre dentro
      del máximo) y `Priority` asignada.
- [x] Crear los Sprints en Azure Boards y distribuir el backlog — confirmado: existen
      Sprint 1 (08/09–21/09), Sprint 2 (22/09–05/10) y Sprint 3 (06/10–19/10), con los
      53 work items del proyecto ya repartidos entre los tres (18 en Sprint 1, 17 en
      Sprint 2, 18 en Sprint 3). Sprint 1 completo: E1 (Catálogo) y E8 (Seguridad) con
      Epic/Feature/ambos PBIs en estado `Done`, coincide con `sprint1-scrum-evidence.md`.

## 4. Azure Pipelines
- [x] Pipeline creado, apuntando a `devops/azure-pipelines.yml`, fuente GitHub
- [x] Pipeline compila correctamente — verificado con `az pipelines build list`: 19
      builds registrados al 24/09/2026, todos en verde; la más reciente es
      #20260924.5 sobre `main` (backend + frontend + build de imagen Docker)
- [x] (DevOps 2, 26/09) **Decisión de arquitectura, no ampliación:** en vez de ampliar
      `azure-pipelines.yml` con pruebas automatizadas y despliegue, el despliegue real
      (backend + migraciones de base de datos) se implementó en GitHub Actions
      (`.github/workflows/backend-deploy.yml`), justificado en detalle en
      `docs/devops2-implementacion.md` (sección "Creación y configuración del
      pipeline"). `azure-pipelines.yml` se mantiene con su alcance original —
      verificación de compilación de backend y frontend — y se le agregó `pr: none`
      para que no bloquee la fusión de Pull Requests en GitHub, ya que esa validación
      la cubre GitHub Actions.

## 5. Azure Test Plans
- [x] **Bloqueo de licencia resuelto el 24/09/2026.** Microsoft habilitó el hub de Test
      Plans (creación de planes, suites estáticas y ejecución manual) para el nivel
      gratuito de Azure DevOps — ya no requiere la licencia paga "Basic + Test Plans" ni
      el trial de 30 días. Verificado directamente: el usuario `drecinosg2@miumg.edu.gt`
      sigue con licencia `Basic` (no `Basic + Test Plans`) según
      `_apis/userentitlements`, y aun así la API de Test Plans acepta lectura y
      escritura sin error de licencia.
- [x] **Plan de pruebas creado:** "Plan de pruebas - Delta ERP Contable" (Plan ID 188),
      con 9 suites estáticas — una por módulo (M1–M9) — bajo la suite raíz. Los 18 Test
      Case ya existentes en Azure Boards (ver `docs/plan-de-pruebas-inicial.md`) se
      enlazaron a su suite correspondiente, 2 por módulo (ej. suite M4 - Cuentas por
      cobrar contiene TC-07 y TC-08); verificado leyendo cada suite de vuelta contra la
      API, 18/18 asignados. Esto sustituye la vía alterna usada antes del 24/09 (Test
      Case sueltos en Boards sin un Test Plan que los agrupe) — ahora existen ambas
      cosas: el work item individual y su lugar dentro del plan formal.

## 6. Documentar en el informe de DevOps 1
Entregables oficiales del 19/09 — confirmado por David Recinos (24/09) que ya se
entregaron los tres:
- [x] Documento PDF "DEVOPS 1" (10–15 páginas)
- [x] Video YouTube #1 "Implementación DEVOPS 1" (5–10 min)
- [x] Evidencia Scrum: Sprint Backlog (≤16h/tarea), Burn down chart, Daily Scrum (3
      preguntas), Revisión y retrospectiva del Sprint 1

Usar este checklist como evidencia dentro del documento, junto con capturas de pantalla
de Boards, Pipelines, y el repositorio de GitHub.

## 7. DevOps 2 (26/09) — resumen

El detalle completo de esta entrega (objetivo, herramientas, infraestructura,
pipeline, errores reales encontrados y corregidos, pruebas funcionales, plan de
pruebas y reflexión Scrum) vive en `docs/devops2-implementacion.md`, no en este
checklist. Pendiente de completar antes de la entrega: verificar que el paquete de
la imagen del backend en GHCR esté marcado como público, y crear el canal de
YouTube con el video de evidencia — ambos detallados en la sección "Pendientes
para completar la entrega" de ese documento.
