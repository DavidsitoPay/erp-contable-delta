# Checklist — Configuración de Azure DevOps (DevOps 1)

Estos pasos se hacen manualmente en [dev.azure.com](https://dev.azure.com), no se pueden
automatizar desde el repositorio. Estado actualizado al 19/09/2026.

## 1. Organización y proyecto
- [x] Organización creada: `drecinosg2`
- [x] Proyecto creado: `erp-contable-delta`

## 2. Azure Repos
- [x] **Decisión tomada:** GitHub (`DavidsitoPay/erp-contable-delta`) es la fuente única del
      código. Azure Pipelines y Azure Boards apuntan directo a GitHub — no se usa Azure Repos
      como copia del código, para no mantener dos repositorios sincronizados manualmente
      (ver decisión y justificación en la conversación de configuración del 17/09).
- [ ] Política de rama en `main` de GitHub: requerir Pull Request antes de fusionar
      *(pendiente — opcional para DevOps 1, recomendable antes de que el equipo empiece a
      programar en paralelo en la Etapa 3)*

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

- [ ] Descomponer cada Epic en **Features** (ej. M2 → "Registro de asientos", "Reversión
      de asientos", "Validación de partida doble") — **pendiente**
- [ ] Descomponer cada Feature en **User Stories** / Product Backlog Items con estimación
      en horas (máx. 16h/tarea) — **pendiente, prioridad para hoy** (requerido como
      "Sprint Backlog del Sprint 1" en el entregable del 19/09)
- [ ] Crear el Sprint 1 en Azure Boards y asignarle las historias de M8 (Seguridad) y M1
      (Catálogo) — **pendiente, prioridad para hoy**

## 4. Azure Pipelines
- [x] Pipeline creado, apuntando a `devops/azure-pipelines.yml`, fuente GitHub
- [x] Pipeline compila correctamente — build #20260917.2 en verde (backend + frontend +
      build de imágenes Docker)
- [ ] (DevOps 2, 26/09) Ampliar con pruebas automatizadas y despliegue

## 5. Azure Test Plans
- [ ] **Bloqueado por licencia.** La organización está en el nivel gratuito de Azure
      DevOps, que solo da acceso a un "subset limitado" de Test Plans; crear un plan de
      pruebas completo requiere activar el trial de 30 días o una licencia paga (decisión
      de cuenta, no técnica — pendiente de que el equipo/catedrático lo autorice)
- [ ] (DevOps 2, 26/09) Completar con los casos de prueba reales, empezando por RN-01,
      RN-02 y RN-05 (las reglas de negocio más críticas)

## 6. Documentar en el informe de DevOps 1
Entregables oficiales del 19/09 pendientes de generar:
- [ ] Documento PDF "DEVOPS 1" (10–15 páginas)
- [ ] Video YouTube #1 "Implementación DEVOPS 1" (5–10 min)
- [ ] Evidencia Scrum: Sprint Backlog (≤16h/tarea), Burn down chart, Daily Scrum (3
      preguntas), Revisión y retrospectiva del Sprint 1

Usar este checklist como evidencia dentro del documento, junto con capturas de pantalla
de Boards, Pipelines, y el repositorio de GitHub.
