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

## Arquitectura de despliegue

> Actualizado el 23/09/2026 tras la migración a la nube (DevOps 2). La versión
> anterior de esta sección describía la arquitectura planeada en la etapa de
> DevOps 1: contenedores Docker locales con una ruta de migración futura a Azure.
> Esa migración ya ocurrió, y no exactamente como se planeó originalmente — ver
> `docs/devops2-implementacion.md` para el detalle y la justificación de cada
> sustitución.

Cada capa se despliega en un servicio administrado independiente, sin infraestructura
propia que mantener. El backend se empaqueta como imagen Docker (`devops/Dockerfile.backend`,
dos etapas: compilación y ejecución) y corre en Azure Container Apps. El frontend, al
ser una aplicación de una sola página sin estado de servidor, se despliega directamente
en Vercel a partir del código fuente, sin pasar por una imagen de contenedor. La base
de datos PostgreSQL corre en Neon (proveedor sin servidor), con una rama `production`
—la única que usa el backend desplegado— separada de la rama `development` de uso
local.

La integración y el despliegue continuo se implementaron en GitHub Actions, no en
Azure DevOps: cada push a la rama principal del repositorio dispara la validación y
aplicación del esquema de base de datos, seguida de la construcción y el despliegue
de la nueva versión del backend, sin intervención manual. Azure Pipelines se conserva
con un alcance reducido, limitado a verificar que el backend y el frontend compilen
correctamente, como remanente de la configuración inicial de DevOps 1.

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
