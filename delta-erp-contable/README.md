# Delta ERP Contable (DEC)

Sistema ERP interno de control contable para la empresa Delta.
Proyecto del curso Seminario de Tecnologías de Información (UMG) — 2026.

Ver [`PROJECT.md`](./PROJECT.md) para el plan completo del proyecto y las etapas
del ciclo de desarrollo. Ver [`docs/`](./docs) para arquitectura, reglas de negocio,
diccionario de datos y diagramas.

## Levantar el entorno local

```bash
cd devops
docker compose up --build
```

- Frontend: http://localhost:3000
- Backend (Swagger): http://localhost:5000/swagger
- Base de datos: localhost:5432 (delta_erp / delta_app)

## Estructura del repositorio

```
delta-erp-contable/
├── PROJECT.md              # Plan del proyecto y etapas del SDLC
├── docs/                   # Arquitectura, reglas de negocio, diccionario de datos, diagramas
├── backend/                # API REST en ASP.NET Core (Api / Domain / Infrastructure)
├── frontend/                # SPA en React
├── database/                # Scripts SQL (tablas, funciones, triggers, procedimientos, vistas)
└── devops/                  # docker-compose, Dockerfiles, azure-pipelines.yml
```
