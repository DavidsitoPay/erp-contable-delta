# Checklist — Configuración de Azure DevOps (DevOps 1)

Estos pasos se hacen manualmente en [dev.azure.com](https://dev.azure.com), no se pueden
automatizar desde el repositorio. Marcar conforme se completen.

## 1. Organización y proyecto
- [ ] Crear organización en Azure DevOps (si no existe una del curso/equipo)
- [ ] Crear proyecto `Delta-ERP-Contable`

## 2. Azure Repos
- [ ] Conectar el repositorio de GitHub a Azure Repos (o usar Azure Repos directamente y
      espejar a GitHub) — cualquiera de las dos formas cumple el requisito de "controlador
      de versiones"
- [ ] Configurar política de rama en `main`: requerir Pull Request antes de fusionar

## 3. Azure Boards
- [ ] Crear un **Epic** por cada módulo (M1–M9), usando la tabla de PROJECT.md
- [ ] Descomponer cada Epic en **Features** (por ejemplo, M2 → "Registro de asientos",
      "Reversión de asientos", "Validación de partida doble")
- [ ] Descomponer cada Feature en **User Stories** con estimación en horas (máx. 16h/tarea,
      según la guía del curso)
- [ ] Crear el primer Sprint (Sprint 1) y asignarle las historias de M8 (Seguridad) y M1
      (Catálogo), que son prerrequisito de todo lo demás

## 4. Azure Pipelines
- [ ] Crear un nuevo pipeline apuntando a `devops/azure-pipelines.yml` de este repositorio
- [ ] Verificar que el pipeline compile correctamente backend y frontend
- [ ] (DevOps 2, 26/09) Ampliar con pruebas automatizadas y despliegue

## 5. Azure Test Plans
- [ ] Crear un Test Plan vacío llamado "Delta ERP Contable — Plan de Pruebas"
- [ ] (DevOps 2, 26/09) Completar con los casos de prueba reales, empezando por RN-01,
      RN-02 y RN-12 (las reglas de negocio más críticas)

## 6. Documentar en el informe de DevOps 1
Según la guía del curso, el entregable de esta etapa debe incluir un documento que
describa la implementación anterior. Usar este checklist como evidencia + capturas de
pantalla de Boards, Repos y Pipelines ya configurados.
