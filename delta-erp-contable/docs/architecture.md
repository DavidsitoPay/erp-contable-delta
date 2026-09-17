# Arquitectura — Delta ERP Contable

## Arquitectura general del sistema

Delta ERP Contable (DEC) se estructura en tres capas desacopladas:

**Capa de presentación.** SPA en React, ejecutada en el navegador. Responsable únicamente
de la interacción con el usuario y la validación de forma. No contiene reglas de negocio
ni acceso directo a la base de datos.

**Capa de lógica y servicios.** API REST en C# sobre ASP.NET Core. Concentra la totalidad
de las reglas de negocio (RN-01 a RN-12). Expone recursos en JSON, autenticación por token.

**Capa de datos.** PostgreSQL. Integridad referencial declarativa, procedimientos
almacenados para cierre de periodo, y disparadores limitados a validaciones que **no**
dependen del usuario final (ver nota de auditoría abajo).

## Relación con el modelo de datos

- Núcleo contable (M2, M3) → `AsientoContable`, `LineaAsiento`, `CuentaContable`, `CentroCosto`.
- CxC/CxP (M4, M5) → `DocumentoCxC`, `DocumentoCxP`, `AplicacionPagoCliente`, `AplicacionPagoProveedor`.
- Tesorería (M6) → `CuentaBancaria`, `MovimientoTesoreria`, `ConciliacionBancaria`.
- Seguridad y auditoría (M8) → `Usuario`, `Perfil`, `BitacoraAuditoria`.

## Arquitectura de despliegue

Contenedores Docker independientes por capa (frontend, backend, base de datos), sobre
infraestructura propia, con ruta de migración futura a la nube (Azure) sin cambiar código.
CI/CD gestionado con Azure DevOps (Pipelines, Repos, Boards, Test Plans).

Comunicación: navegador → frontend (HTTPS) → backend (HTTPS/JSON) → base de datos
(SQL vía Npgsql/EF Core); backend → SMTP externo (notificaciones).

## Nota crítica de auditoría (corrección aplicada)

El registro de `BitacoraAuditoria` **no puede generarse mediante un trigger de base de
datos**, porque la conexión entre la API y PostgreSQL usa un único usuario de aplicación
(o connection pool) que no conserva el contexto del usuario final autenticado por JWT.
Por lo tanto, la inserción en `BitacoraAuditoria` es responsabilidad explícita de la
**capa de lógica (API)**: al validar el token de cada solicitud, la API obtiene el
`usuario_id` y lo inserta en la misma transacción de negocio.

## Consideraciones de seguridad

Autenticación por token en la API; cada solicitud se valida contra `Perfil` antes de
ejecutar cualquier operación. Toda transacción relevante queda en `BitacoraAuditoria`
de forma inmutable.
