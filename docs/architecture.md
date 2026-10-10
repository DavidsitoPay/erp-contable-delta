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
  en asientos manuales. `AsientoTesoreriaBuilder` construye el asiento de todo
  movimiento de tesorería (una línea de banco y una línea por contrapartida, del lado
  opuesto), por lo que cuadra por construcción; `TesoreriaRules` valida montos,
  movimientos, transferencias, cuentas bancarias, apertura y saldo de extracto, y agrupa
  las aplicaciones de un pago por cuenta de control. `IDocumentoFactura` e `ILineaFactura` abstraen los documentos
  y líneas de CxC y CxP.
- Reglas fiscales (`Domain/Rules/`, puras): `MotorFiscal` calcula cada línea y el resumen
  del documento (total con IVA incluido, base, IVA, IVA acreditable) con redondeo por
  línea, acepta un tipo de cambio (1 en la moneda funcional) y clasifica cada línea para
  el libro fiscal. `ImpuestoRules` valida altas de impuestos, vigencia, ámbito y
  consistencia con el régimen de la contraparte. `ConfiguracionFiscalRules` valida régimen,
  agente de retención y cuentas de IVA. `NitRules` normaliza y valida el NIT (módulo 11).
  `DteRules` valida el UUID, serie, número y fecha de certificación. `AsientoFacturaBuilder`
  recibe el resumen fiscal: separa el IVA en su propia línea (débito fiscal en ventas,
  crédito fiscal en compras), lleva a costo el IVA no acreditable e invierte los lados en
  una nota de crédito.
- `DeltaERP.Api/Services/`: `ValidacionContable` verifica contra la base la
  contraparte (RN-04), el periodo abierto (RN-02), las cuentas (existentes, activas,
  no de mayor), la cuenta de control y los centros de costo. `FacturaService` arma y
  persiste asiento y factura. `FacturaFiscalService` valida impuestos (existencia, estado,
  vigencia, ámbito y régimen de la contraparte), calcula con `MotorFiscal` y comprueba las
  cuentas de IVA configuradas; `ConfiguracionFiscalService` lee la fila única de
  configuración fiscal y `LibroFiscalService` arma el libro de compras y ventas. Las
  violaciones de unicidad del DTE y el exceso de una nota de crédito sobre el saldo de su
  origen los resuelve la base de datos y `FacturaService` los traduce a `409`.
  `AuditoriaService` ejecuta la operación y la llamada a
  `sp_registrar_auditoria` (RN-08) en una misma transacción. `PerfilFactura` reúne lo
  que distingue a CxC de CxP (sigla, tipo de tercero, tipos de documento, cuenta de
  control, lado de control). `TesoreriaService` (M6) es el único lugar que une cuenta
  bancaria, periodo abierto, asiento y movimiento: lo usan los movimientos manuales, las
  transferencias, la apertura de cuentas y los cobros y pagos de CxC/CxP (que derivan la
  cuenta de control de cada factura aplicada y registran movimiento y asiento dentro de
  la transacción del pago). `ErroresPostgres` traduce de forma compartida las fallas de
  los procedimientos almacenados (periodos y conciliaciones): `P0001` a `403`, `P0002`
  a `404` y `55000` a `409`, y reconoce violaciones de unicidad por nombre de restricción.
- Endpoints de tesorería (rol Administrador o Contador, `Roles.GestionTesoreria`):
  `api/cuentas-bancarias` (listar, obtener, crear, actualizar banco/número/tipo,
  desactivar), `api/movimientos-tesoreria` (listar con filtros, registrar movimiento
  manual, `POST transferencias`) y `api/conciliaciones` (listar, detalle, crear, editar,
  `cancelar`, marcar y desmarcar movimientos, `finalizar`). Registrar cobros y pagos
  (`POST api/cxc/pagos`, `POST api/cxp/pagos`) exige `Roles.RegistroPagos`
  (Administrador o Contador).
- Reversa de asientos (RN-03, rol Administrador, `Roles.Administrador`): `POST api/asientos/{id}/reversar` recibe `{ motivo }` (obligatorio, máx. 250 caracteres; `400` si falta o excede). Responde `403` si el perfil no es Administrador (en el token o en la base de datos), `404` si el asiento no existe y `409` si no está `Confirmado`, ya es una reversa, su periodo no está `Abierto` o está vinculado a un documento CxC, CxP o movimiento de tesorería (`ReversaAsiento` centraliza estos chequeos). Ejecuta `CALL sp_reversar_asiento(id, usuario, motivo)` en una transacción, que revalida todo con bloqueo de fila y escribe la bitácora. Responde `200` con `originalId`, `originalNumero`, `reversaId` y `reversaNumero`. `GET api/libros/diario` agrega a cada asiento `estado`, `reversaDeId`, `reversaDeNumero`, `reversadoPorId`, `reversadoPorNumero` y `reversible`; el libro diario del frontend muestra el botón Reversar (solo Administrador, con diálogo de motivo) y los indicadores de estado.
- Reportes (M7, rol Administrador o Contador, `Roles.GestionCatalogo`): `ReportesController` expone `GET api/reportes/balance-general` y `GET api/reportes/estado-resultados` (parámetro `periodoId`), de solo lectura. Lee `fn_reporte_saldos` (`database/11_reportes.sql`) con `FromSql` (entidad sin llave `SaldoReporteCuenta`) y delega en `DeltaERP.Domain/Rules/EstadosFinancieros` (pura) la sección por tipo, los totales de nivel 1, el resultado del ejercicio y el cuadre. El frontend los muestra en las pestañas Balance general y Estado de resultados (`components/Reportes/`), visibles solo para esos perfiles.
- Fiscal (M10): `api/configuracion-fiscal` (`GET` con `Roles.GestionCatalogo`; `PUT` solo
  Administrador, con auditoría `actualizar_configuracion_fiscal`); `api/impuestos` (`GET`
  y `GET {id}` para cualquier usuario autenticado, porque el Vendedor factura; `POST`,
  `PUT {id}` y `DELETE {id}`, que desactiva, con `Roles.GestionCatalogo`);
  `api/libros/fiscal/ventas` y `api/libros/fiscal/compras` (`anio`, `mes`;
  `Roles.ReportesFiscales`, Administrador o Contador). `api/contrapartes` acepta y devuelve
  régimen de IVA, régimen de ISR, residencia y agente de retención. Las facturas
  (`api/cxc/facturas`, `api/cxp/facturas`) reciben por línea `impuestoId` y
  `tipoBienServicio`, el precio con IVA incluido, los datos del DTE y, en notas de crédito,
  `documentoOrigenId`; el `POST` responde `201` con el mismo detalle que el `GET`, con montos
  de base e IVA, datos del DTE y de la factura de origen.
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
- Tesorería (M6) → `CuentaBancaria`, `MovimientoTesoreria` (cada uno con su asiento), `ConciliacionBancaria`, `DetalleConciliacion`.
- Seguridad y auditoría (M8) → `Usuario`, `Perfil`, `BitacoraAuditoria` (poblada por la API, no por trigger).
- Cierre contable → `PeriodoContable` y `SaldoCuentaPeriodo` (saldos consolidados e inmutables por periodo).
- Reportes (M7) → sin tablas propias: `fn_reporte_saldos` combina `SaldoCuentaPeriodo` de periodos cerrados con asientos en vivo.
- Fiscal (M10) → `Impuesto`, `ConfiguracionFiscal` (fila única), `Moneda` (moneda funcional) y los campos fiscales y de DTE de `Contraparte`, `DocumentoCxC`/`DocumentoCxP` y sus líneas.

Ningún módulo del frontend accede directamente a la base de datos; todo pasa por la
API.

## Pruebas y calidad

**Backend** (`backend/tests/DeltaERP.Tests`, xUnit). Dos grupos:

- Unitarias (`Unit/`): reglas puras del dominio (`AsientoFacturaBuilder`,
  `AsientoTesoreriaBuilder`, `FacturaRules`, `PagoRules`, `PartidaDobleValidator`,
  `TesoreriaRules`, `EstadosFinancieros`, `MotorFiscal`, `ImpuestoRules`,
  `ConfiguracionFiscalRules`, `NitRules`, `DteRules`) y `ErroresPostgres`, sin base de datos.
- Integración (`Integration/`, trait `Category=Integration`): levantan la API con
  `WebApplicationFactory` contra un PostgreSQL 16 real, de modo que los triggers
  (RN-01, RN-03, RN-05) actúan igual que en producción. Cubren todos los controladores
  de la API: asientos, auth, centros de costo, contrapartes, cuentas bancarias,
  cuentas contables, conciliaciones, configuración fiscal, CxC, CxP, impuestos, libros,
  libro fiscal, movimientos de tesorería, periodos contables y reportes, y el esquema
  fiscal (`FiscalEsquemaTests`). Las pruebas de login verifican la emisión del
  token (`AuthController`/`TokenService`) con un cliente anónimo y el esquema JWT
  real; el resto usa `TestAuthHandler`. Las operaciones cuya autorización vive en un
  procedimiento almacenado (cerrar y reabrir periodo) se validan contra el perfil
  del usuario en la base de datos, no contra el encabezado de prueba.

Soporte de las pruebas de integración (`Support/`):

- `PostgresFixture` lee la cadena de conexión de la variable `TEST_PG_CONN`; si falta,
  las pruebas fallan (no se omiten). El nombre de la base debe contener `test`. Al
  iniciar recrea el esquema `public` y aplica `database/01` a `05` y `07` a `14`; no aplica
  `06` (datos semilla). Después deja configuradas las cuentas de IVA, da `LOGIN` con una
  contraseña aleatoria por ejecución a `delta_api` y conecta toda la suite HTTP con ese rol;
  la siembra de datos y las pruebas de triggers siguen con el propietario. `PermisosApiTests`
  verifica lo permitido, lo denegado (42501) y la decisión de permisos por tabla. `FacturaPayloads`
  construye las facturas de prueba (impuesto, líneas y DTE).
- Aislamiento: bitácora, saldos, asientos y usuarios son inmutables, así que no hay
  limpieza entre pruebas. Cada prueba siembra sus propias filas con sufijos únicos
  (`TestData`) y nunca cuenta filas globales. Las pruebas comparten una sola base
  mediante una colección xUnit. Los periodos creados vía API usan rangos únicos
  (años 2100 en adelante, `TestData.RangoPeriodoUnico`), porque la validación de
  solapamiento es global.
- Autenticación: `TestAuthHandler` reemplaza el esquema JWT; la identidad y el rol se
  toman de los encabezados `X-Test-User-Id` y `X-Test-Role`.

**Frontend** (Vitest y React Testing Library, `frontend/src/**/*.test.js(x)`).
`npm run test:ci` ejecuta la suite y genera cobertura en `lcov` (para SonarCloud) y
`cobertura` (para el gate de `diff-cover`), además de un reporte `junit`.

**Dónde corren.** Ambas suites corren en GitHub Actions con un contenedor de servicio
`postgres:16`: en cada push a `dev` (`backend-build-check.yml`) y en cada Pull Request
y push a `main` (`sonarcloud-analysis.yml`), este último con cobertura enviada a
SonarCloud. En los push a `dev`, `backend-build-check.yml` también exige con
`diff-cover` al menos 80 % de cobertura en las líneas cambiadas respecto a `main` y
aplica el gate de duplicación de `jscpd` (bloques de 80 tokens o más) sobre C#, JS y JSX
en `backend/src` y `frontend/src`.

**Quality gate de SonarCloud.** Cobertura mínima de 80 % sobre código nuevo y
duplicación máxima de 3 %. Quedan fuera de la cobertura `Program.cs`,
`DeltaErpDbContext.cs`, `Entities/`, `main.jsx`, hojas de estilo, archivos
`*.config.js`, `frontend/src/test/` y los propios archivos de prueba. Quedan fuera de la detección de duplicación en SonarCloud `backend/tests/` y las
pruebas del frontend (`frontend/**/*.test.*`, `frontend/src/test/**`). La carpeta `database/` se
excluye del análisis de SonarCloud porque su analizador SQL es para Oracle PL/SQL; el
SQL tampoco se analiza por duplicación: las migraciones son solo de adición y las correcciones
redefinen objetos con `CREATE OR REPLACE`, por lo que repiten por diseño cuerpos de scripts anteriores.

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
  contra la rama `production` de Neon con el rol propietario (`run_migrations.sh` ejecuta cada script con
  `psql --single-transaction`: si falla, se revierte completo y no se registra como aplicado) y, solo si eso funciona, construye y publica
  la imagen del backend y actualiza Azure Container Apps.
- `frontend-alias.yml`: tras cada deploy de producción en Vercel, reapunta el
  dominio corto del proyecto al nuevo despliegue.
- `sonarcloud-analysis.yml`: en cada Pull Request y en cada push a `main` corre las
  pruebas con cobertura y analiza calidad y seguridad del código (backend y frontend)
  en SonarCloud.
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
- La base opera con dos roles. El propietario (`neondb_owner`) solo aplica las
  migraciones, mediante el secreto `NEON_PROD_CONNECTION_STRING` de `backend-deploy.yml`.
  La API en ejecución usa `delta_api`, cuya cadena de conexión (por el pooler de Neon) es
  un secreto de la Container App referenciado con `secretref`. `delta_api` se crea por SQL
  (`database/14_rol_api.sql`, `NOLOGIN` y sin contraseña en el repo; `LOGIN` y contraseña se
  asignan por rama con `ALTER ROLE`), por lo que no es miembro de `neon_superuser`.
- Permisos de `delta_api` (mínimo privilegio): `CONNECT` y `USAGE` en `public`; DML solo en
  las 25 de 29 tablas que usa la API; sin `UPDATE` en las tablas inmutables
  (`bitacoraauditoria`, `saldocuentaperiodo`, `movimientotesoreria`, `lineaasiento`, pagos,
  aplicaciones y detalles); `DELETE` solo en `detalleconciliacion`; `bitacoraauditoria` y
  `saldocuentaperiodo` solo con `INSERT` (se escriben vía procedimientos y triggers y se
  leen por vistas y funciones); `SELECT` en 6 vistas; `USAGE` en 21 secuencias; `EXECUTE`
  solo en 9 rutinas (`fn_reporte_saldos`, `sp_registrar_auditoria`, `sp_cerrar_periodo`,
  `sp_reabrir_periodo`, `sp_reversar_asiento`, `sp_finalizar_conciliacion`,
  `fn_perfil_contador`, `fn_perfil_administrador_sistema`,
  `fn_usuario_tiene_perfil_autorizado`). Se revocan a `PUBLIC` el `EXECUTE` en rutinas (también
  como default privilege), `CREATE` en `public` y `TEMPORARY` en la base.
- Ninguna rutina es `SECURITY DEFINER`: triggers y procedimientos corren con los permisos
  del llamador, por lo que la matriz de `delta_api` incluye lo que ellos tocan.
- Convención: toda migración que cree tablas o rutinas declara sus `GRANT` a `delta_api`.
  `PermisosApiTests` falla si una tabla no tiene decisión de permisos o si hay más de 9
  rutinas ejecutables.
