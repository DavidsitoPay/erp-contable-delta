# Delta ERP Contable (DEC)

Sistema ERP interno de control contable para la empresa Delta.
Proyecto del curso Seminario de Tecnologías de Información (UMG) — 2026.

Ver [`PROJECT.md`](./PROJECT.md) para el plan completo del proyecto y las etapas
del ciclo de desarrollo. Ver [`docs/`](./docs) para arquitectura, reglas de negocio,
diccionario de datos y diagramas.

## Levantar el entorno local

El proyecto corre en la nube (Neon + Azure Container Apps + Vercel); localmente ya no
se usa Docker. Para desarrollo local, cada quien apunta contra la branch `development`
de Neon (un espacio de trabajo separado del que usa el backend desplegado — ver
"Entornos desplegados" abajo). La connection string está en `CREDENTIALS.md`, que no se
sube al repo — pídesela a un compañero.

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
| Frontend | Vercel | Deploy automático en push: `main` → producción, `dev` → preview (Git integration) |
| Backend | Azure Container Apps (Consumption) | `devops/Dockerfile.backend`; deploy solo en push a `main` vía `.github/workflows/backend-deploy.yml` |
| Base de datos | Neon (Postgres serverless), branch `production` | La única branch ligada al pipeline — `database/run_migrations.sh` la migra automáticamente en cada push a `main`, antes de que se despliegue el backend nuevo |

`main` es la única rama que despliega de verdad: un push a `dev` no toca Azure ni
Neon. La branch `development` de Neon es aparte — solo para desarrollo local (ver
arriba), no la usa nada desplegado ni el pipeline.

## Migraciones y pruebas

`database/run_migrations.sh` aplica los scripts numerados (01–13) en orden, cada uno con
`psql -v ON_ERROR_STOP=1 --single-transaction`. Un script ya aplicado no se edita: las
correcciones van en un archivo nuevo.

Pruebas del backend (xUnit, requieren un PostgreSQL vacío en la variable `TEST_PG_CONN`;
en CI corren contra un servicio PostgreSQL de GitHub Actions):

```bash
dotnet test backend/tests/DeltaERP.Tests/DeltaERP.Tests.csproj
```

Pruebas del frontend: `cd frontend && npm test` (Vitest).

## Usuarios de prueba (seed)

`database/06_seed_dev.sql` puebla un usuario por perfil. El nombre del archivo es
histórico (se escribió pensando solo en desarrollo local); hoy también corre contra
`production`, porque es la única forma de entrar al sistema — este proyecto no tiene
un flujo de registro de usuarios ni datos reales que proteger. Las credenciales están
en `CREDENTIALS.md` (no se sube al repo — pídeselas a un compañero o revisa el archivo
si ya lo tienes localmente).

## Estructura del repositorio

```
delta-erp-contable/
├── PROJECT.md              # Plan del proyecto y etapas del SDLC
├── docs/                   # Arquitectura, reglas de negocio, diccionario de datos, diagramas
├── backend/                # API REST en ASP.NET Core (Api / Domain / Infrastructure)
├── frontend/                # SPA en React
├── database/                # Scripts SQL numerados 01–13 (tablas, funciones, triggers, procedimientos, vistas, tesorería, reportes, fiscal) y datos demo
├── devops/                  # Dockerfile.backend (Azure Container Apps)
└── .github/workflows/       # backend-build-check, sonarcloud-analysis, backend-deploy, frontend-alias
```
