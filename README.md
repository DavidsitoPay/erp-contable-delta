# Delta ERP Contable (DEC)

Sistema ERP interno de control contable para la empresa Delta.
Proyecto del curso Seminario de Tecnologías de Información (UMG) — 2026.

Ver [`PROJECT.md`](./PROJECT.md) para el plan completo del proyecto y las etapas
del ciclo de desarrollo. Ver [`docs/`](./docs) para arquitectura, reglas de negocio,
diccionario de datos y diagramas.

## Levantar el entorno local

El proyecto corre en la nube (Neon + Azure Container Apps + Vercel); localmente ya no
se usa Docker. Cada quien apunta contra la base de datos de la branch `development` de
Neon (pídele la connection string a un compañero — está en `CREDENTIALS.md`, que no se
sube al repo).

**Backend:**

```bash
cd backend/src/DeltaERP.Api
dotnet user-secrets set "ConnectionStrings:Default" "<connection string de Neon, branch development>"
dotnet run
```

`dotnet user-secrets` guarda la connection string fuera del repo (no toca
`appsettings.json`, que solo trae el valor de ejemplo local). Backend disponible en
`http://localhost:5000` (Swagger en `/swagger`).

**Frontend:**

```bash
cd frontend
npm install
npm run dev
```

Frontend disponible en `http://localhost:3000`, apuntando por defecto a
`http://localhost:5000/api` (ver `VITE_API_URL` en `frontend/src/services/api.js`).

## Entornos desplegados

| Capa | Servicio | Notas |
|---|---|---|
| Frontend | Vercel | Deploy automático en push a `main`/`dev` (Git integration) |
| Backend | Azure Container Apps (Consumption) | `devops/Dockerfile.backend`; deploy vía `.github/workflows/backend-deploy.yml` |
| Base de datos | Neon (Postgres serverless) | Branches `production` (solo esquema) y `development` (+ seed de prueba) |

## Usuarios de prueba (seed de desarrollo)

`database/06_seed_dev.sql` puebla un usuario por perfil, solo para desarrollo/demo. Las
credenciales están en `CREDENTIALS.md` (no se sube al repo — pídeselas a un compañero
o revisa el archivo si ya lo tienes localmente).

## Estructura del repositorio

```
delta-erp-contable/
├── PROJECT.md              # Plan del proyecto y etapas del SDLC
├── docs/                   # Arquitectura, reglas de negocio, diccionario de datos, diagramas
├── backend/                # API REST en ASP.NET Core (Api / Domain / Infrastructure)
├── frontend/                # SPA en React
├── database/                # Scripts SQL (tablas, funciones, triggers, procedimientos, vistas)
└── devops/                  # Dockerfile.backend (Azure Container Apps), azure-pipelines.yml
```
