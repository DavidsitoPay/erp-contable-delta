# Arquitectura — Delta ERP Contable

> Fuente: Diagrama Modelo Relacional de Base de Datos (documento oficial, v2, 12/09/2026).
> Este documento sustituye la versión anterior de este archivo.

## Arquitectura general del sistema

Delta ERP Contable (DEC) se estructura en tres capas desacopladas:

**Capa de presentación.** SPA en React. Responsable únicamente de la interacción con
el usuario y la validación de forma. No contiene reglas de negocio ni acceso directo
a la base de datos.

**Capa de lógica y servicios.** API REST en C# sobre ASP.NET Core. Concentra las
reglas de negocio (RN-01 partida doble, RN-02 bloqueo de periodos cerrados, RN-03
reversión en vez de eliminación, RN-05 límites de aplicación de pagos, RN-08 registro
en bitácora de auditoría, RN-09 control de acceso por perfil, entre otras). Autenticación
por token.

Las reglas que comparten CxC y CxP viven en un único lugar y cada módulo solo aporta
su perfil:

- `DeltaERP.Domain/Rules/` (puras, sin acceso a datos): `AsientoFacturaBuilder`
  construye el asiento de una factura y es el único punto donde CxC (debita la cuenta
  de control, acredita las líneas) y CxP (acredita la cuenta de control, debita las
  líneas) se diferencian; el lado de control suma lo que suma el otro lado, así que
  RN-01 se cumple por construcción. `FacturaRules` valida la cabecera y prepara el
  documento nuevo. `PagoRules` valida la solicitud de pago y las aplicaciones,
  incluida la comprobación de RN-05 (varias aplicaciones a la misma factura se suman
  antes de compararlas con el saldo pendiente). `PartidaDobleValidator` valida RN-01
  en asientos manuales. `IDocumentoFactura` e `ILineaFactura` abstraen los documentos
  y líneas de CxC y CxP.
- `DeltaERP.Api/Services/`: `ValidacionContable` verifica contra la base la
  contraparte (RN-04), el periodo abierto (RN-02), las cuentas (existentes, activas,
  no de mayor), la cuenta de control y los centros de costo. `FacturaService` arma y
  persiste asiento y factura. `AuditoriaService` ejecuta la operación y la llamada a
  `sp_registrar_auditoria` (RN-08) en una misma transacción. `PerfilFactura` reúne lo
  que distingue a CxC de CxP (sigla, tipo de tercero, tipos de documento, cuenta de
  control, lado de control).
- `DeltaERP.Api/Models/`: modelos de detalle de factura y de sus líneas que devuelven
  los controladores `CxCController` y `CxPController`.

**Capa de datos.** PostgreSQL. Incluye integridad referencial declarativa, restricciones
de dominio, procedimientos almacenados para cierre de periodo y reversión de asientos,
y **disparadores responsables de hacer cumplir las reglas de integridad que no pueden
delegarse en la aplicación**: validación de partida doble, bloqueo de periodos cerrados,
prohibición de eliminar asientos ya registrados, límites en la aplicación de pagos, e
inmutabilidad tanto de la bitácora de auditoría como de los saldos consolidados por
periodo.

## Responsabilidad de la trazabilidad (RN-08)

El registro de **quién** ejecutó cada operación corresponde a la capa de lógica, no a
la capa de datos — la API se conecta a PostgreSQL mediante un usuario de aplicación
y un pool de conexiones, así que el motor no tiene contexto sobre qué usuario final
originó la transacción, y un disparador no puede extraer el usuario desde un token
que nunca llega a la base de datos.

Por eso la API es la única responsable de alimentar `BitacoraAuditoria`, pero **no
mediante un INSERT directo**: lo hace invocando `sp_registrar_auditoria` (ver
`04_procedures.sql`) dentro de la misma transacción de la operación que se está
registrando, de modo que si la operación se revierte, su registro de auditoría también.
El identificador de usuario proviene del token ya validado por la API.

La base de datos conserva lo que sí le corresponde: exigir por llave foránea que el
usuario exista y esté activo, e impedir — mediante trigger — que cualquiera, incluido
el administrador, modifique o elimine registros de auditoría ya escritos.

## Relación con el modelo de datos

- Núcleo contable (M2, M3) → `AsientoContable`, `LineaAsiento`, `CuentaContable`, `CentroCosto`.
- CxC/CxP (M4, M5) → `DocumentoCxC`, `DocumentoCxP`, `AplicacionPagoCliente`, `AplicacionPagoProveedor`.
- Tesorería (M6) → `CuentaBancaria`, `MovimientoTesoreria`, `ConciliacionBancaria`.
- Seguridad y auditoría (M8) → `Usuario`, `Perfil`, `BitacoraAuditoria` (poblada por la API, no por trigger).
- Cierre contable → `PeriodoContable` y `SaldoCuentaPeriodo` (saldos consolidados e inmutables por periodo).

Ningún módulo del frontend accede directamente a la base de datos; todo pasa por la
API.

## Pruebas y calidad

**Backend** (`backend/tests/DeltaERP.Tests`, xUnit). Dos grupos:

- Unitarias (`Unit/`): reglas puras del dominio (`AsientoFacturaBuilder`,
  `FacturaRules`, `PagoRules`, `PartidaDobleValidator`), sin base de datos.
- Integración (`Integration/`, trait `Category=Integration`): levantan la API con
  `WebApplicationFactory` contra un PostgreSQL 16 real, de modo que los triggers
  (RN-01, RN-03, RN-05) actúan igual que en producción. Cubren los controladores de
  CxC y CxP.

Soporte de las pruebas de integración (`Support/`):

- `PostgresFixture` lee la cadena de conexión de la variable `TEST_PG_CONN`; si falta,
  las pruebas fallan (no se omiten). El nombre de la base debe contener `test`. Al
  iniciar recrea el esquema `public` y aplica `database/01` a `05` y `07`; no aplica
  `06` (datos semilla).
- Aislamiento: bitácora, saldos, asientos y usuarios son inmutables, así que no hay
  limpieza entre pruebas. Cada prueba siembra sus propias filas con sufijos únicos
  (`TestData`) y nunca cuenta filas globales. Las pruebas comparten una sola base
  mediante una colección xUnit.
- Autenticación: `TestAuthHandler` reemplaza el esquema JWT; la identidad y el rol se
  toman de los encabezados `X-Test-User-Id` y `X-Test-Role`.

**Frontend** (Vitest y React Testing Library, `frontend/src/**/*.test.js(x)`).
`npm run test:ci` ejecuta la suite y genera cobertura en `lcov`.

**Dónde corren.** Ambas suites corren en GitHub Actions con un contenedor de servicio
`postgres:16`: en cada push a `dev` (`backend-build-check.yml`) y en cada Pull Request
y push a `main` (`sonarcloud-analysis.yml`), este último con cobertura enviada a
SonarCloud.

**Quality gate de SonarCloud.** Cobertura mínima de 80 % sobre código nuevo y
duplicación máxima de 3 %. Quedan fuera de la cobertura `Program.cs`,
`DeltaErpDbContext.cs`, `Entities/`, `main.jsx`, hojas de estilo, archivos
`*.config.js`, `frontend/src/test/` y los propios archivos de prueba. Quedan fuera de
la detección de duplicación `database/04_procedures.sql`, `backend/tests/` y las
pruebas del frontend.

## Arquitectura de despliegue

Cada capa se despliega en un servicio administrado independiente, sin infraestructura
propia que mantener. El backend se empaqueta como imagen Docker (`devops/Dockerfile.backend`,
dos etapas: compilación y ejecución) y corre en Azure Container Apps. El frontend, al
ser una aplicación de una sola página sin estado de servidor, se despliega directamente
en Vercel a partir del código fuente, sin pasar por una imagen de contenedor. La base
de datos PostgreSQL corre en Neon (proveedor sin servidor), con una rama `production`
—la única que usa el backend desplegado— separada de la rama `development` de uso
local.

La integración y el despliegue continuo corren en GitHub Actions, en cuatro workflows
independientes (`.github/workflows/`):

- `backend-deploy.yml`: en cada push a `main` (ignora los cambios que solo tocan
  `backend/tests/`), aplica las migraciones pendientes
  contra la rama `production` de Neon y, solo si eso funciona, construye y publica
  la imagen del backend y actualiza Azure Container Apps.
- `frontend-alias.yml`: tras cada deploy de producción en Vercel, reapunta el
  dominio corto del proyecto al nuevo despliegue.
- `sonarcloud-analysis.yml`: en cada Pull Request y en cada push a `main` corre las
  pruebas con cobertura y analiza calidad y seguridad del código (backend, frontend
  y SQL) en SonarCloud.
- `backend-build-check.yml`: en cada push a `dev` corre la suite completa de pruebas
  del backend y del frontend, antes de que el cambio llegue a un Pull Request o a `main`.

Comunicación: navegador → frontend (HTTPS) → backend (HTTPS/JSON) → base de datos
(SQL vía Npgsql/EF Core); backend → SMTP externo (notificaciones, diseñado, no
implementado todavía).

## Consideraciones de seguridad

La seguridad es una **responsabilidad compartida** entre la capa de lógica y la capa
de datos:

- La API autentica por token y valida perfil/permisos antes de cada operación, y
  registra en `BitacoraAuditoria` al responsable de cada transacción.
- La base de datos garantiza que ese registro no pueda alterarse después (trigger de
  inmutabilidad), almacena contraseñas cifradas con función de derivación y salt
  (nunca en texto claro), y no permite eliminación física de usuarios ni de cuentas
  contables con movimiento — solo desactivación (`activo`/`activa`).
- Operaciones sensibles (reapertura de un periodo cerrado, finalización de una
  conciliación bancaria) verifican el perfil del usuario **dentro del propio
  procedimiento almacenado** (`sp_reabrir_periodo`, `sp_finalizar_conciliacion`).
- La API se conecta con un rol de base de datos con permisos de **ejecución** sobre
  procedimientos y de **lectura** sobre vistas, pero sin permisos directos de
  modificación sobre las tablas transaccionales — toda operación pasa
  necesariamente por las reglas implementadas.
