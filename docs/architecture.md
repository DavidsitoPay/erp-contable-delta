# Arquitectura — Delta ERP Contable

> Fuente: Diagrama Modelo Relacional de Base de Datos (documento oficial, v2, 12/09/2026).
> Este documento sustituye la versión anterior de este archivo.

## Arquitectura general del sistema

Delta ERP Contable (DEC) se estructura en tres capas desacopladas:

**Capa de presentación.** SPA en React. Responsable únicamente de la interacción con
el usuario y la validación de forma. No contiene reglas de negocio ni acceso directo
a la base de datos. El menú principal (`MenuPrincipal`) es una barra de navegación fija (sticky) que agrupa las pantallas en cinco desplegables.

**Capa de lógica y servicios.** API REST en C# sobre ASP.NET Core 10 (.NET 10 LTS). Concentra las
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
  una nota de crédito; en una factura de compra con retención de ISR acredita Cuentas por
  pagar por el neto y agrega al final una línea de crédito por la retención.
  `RetencionRules` calcula la escala de ISR y evalúa si un proveedor retiene (régimen,
  residencia, pequeño contribuyente, umbral), sugiere y valida el IVA retenido por un
  cliente y deriva el estado de una constancia. `ParametroRetencionRules` valida altas y
  cambios de parámetros; `VigenciaRules` y `PeriodoFiscalRules` son comunes a impuestos,
  parámetros y reportes. `AsientoTesoreriaBuilder` acepta retenciones de IVA en un cobro:
  Banco por el neto, una línea por retención y Cuentas por cobrar por el total aplicado.
- `DeltaERP.Api/Services/`: `ValidacionContable` verifica contra la base la
  contraparte (RN-04), el periodo abierto (RN-02), las cuentas (existentes, activas,
  no de mayor), la cuenta de control y los centros de costo. `FacturaService` arma y
  persiste asiento y factura. `FacturaFiscalService` valida impuestos (existencia, estado,
  vigencia, ámbito y régimen de la contraparte), calcula con `MotorFiscal` y comprueba las
  cuentas de IVA configuradas; `ConfiguracionFiscalService` lee la fila única de
  configuración fiscal y `LibroFiscalService` arma el libro de compras y ventas.
  `RetencionService` resuelve parámetros vigentes, evalúa y valida las retenciones de ISR
  (factura CxP) y de IVA (cobro CxC) y persiste las filas de `retencion`;
  `ConstanciaRetencionService` registra y anula constancias y `ReporteRetencionService`
  arma el reporte y su conciliación. Las
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
- Reportes (M7, rol Administrador o Contador, `Roles.GestionCatalogo`): `ReportesController` expone `GET api/reportes/balance-general` y `GET api/reportes/estado-resultados` (parámetro `periodoId`), de solo lectura. Lee `fn_reporte_saldos` (`database/11_reportes.sql`) con `FromSql` (entidad sin llave `SaldoReporteCuenta`) y delega en `DeltaERP.Domain/Rules/EstadosFinancieros` (pura) la sección por tipo, los totales de nivel 1, el resultado del ejercicio y el cuadre. El frontend los muestra en el desplegable Reportes del menú principal (Balance general y Estado de resultados, `components/Reportes/`), visible solo para esos perfiles.
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
- Retenciones (M10, I2): ver "Fiscal: retenciones (M10, I2)" más abajo.
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

## Fiscal: retenciones (M10, I2)

Migración `database/15_retenciones.sql`. Alcance implementado: ISR retenido a proveedores del régimen simplificado, IVA retenido por clientes agentes, constancias registradas, parámetros con vigencia y reporte por período y NIT. Tasas, tramo y umbrales son datos en `parametroretencion`, con la leyenda "Referencial, validar con asesor"; ningún valor fiscal vive en el código.

**ISR al registrar una factura CxP (RN-18, RN-19).** `FacturaFiscalService.PrepararAsync` calcula el IVA como en I1 y, solo en compras, pide a `RetencionService` la evaluación del proveedor con el parámetro `ISR_PROVEEDOR_ROS` vigente en la fecha de la factura. Si retiene, `AsientoFacturaBuilder` acredita Cuentas por pagar por el neto y "ISR retenido por pagar" por la retención; `FacturaService` guarda asiento, factura y fila de `retencion` en una sola transacción con la bitácora. El pago posterior no cambia (Debe Cuentas por pagar / Haber Banco): lo que cambia es el saldo. `vw_saldodocumentocxp`, `fn_limite_pago_cxp` y `fn_nota_credito_valida_cxp` restan la retención leída de `retencion` (`ISR_ROS`), de modo que listados, pagos y notas de crédito usan el saldo neto sin duplicar el importe en `documentocxp`. `TesoreriaService` identifica la cuenta de control de la factura excluyendo las líneas que coinciden con una fila de `retencion`, y la API rechaza una cuenta de control igual a la de ISR retenido por pagar.

Si el proveedor no tiene régimen definido o es no residente sin tipo de renta ni exención (RN-29), la factura se registra sin retención con `revision_retencion = PENDIENTE` y una advertencia; se resuelve con `POST api/cxp/facturas/{id}/revision-retencion` y un motivo. Un proveedor simplificado sin parámetro vigente o una cuenta de retención sin configurar responden `409`. El ajuste manual (`retencionIsrAjuste`) exige motivo. `POST api/cxp/facturas/retencion-previa` devuelve el cálculo sin escribir, y el formulario de factura lo consume en lugar de replicar la escala.

**IVA retenido por clientes en el cobro (RN-21).** `POST api/cxc/pagos` acepta `retencionIva` por aplicación. `RetencionService.PrepararIvaClienteAsync` valida factura, cliente, topes (IVA de la factura menos el de sus notas de crédito, y monto aplicado), una sola retención por factura y cobro neto positivo. `AsientoTesoreriaBuilder` debita Banco por el neto y la cuenta "IVA retenido por clientes" por la retención, y acredita Cuentas por cobrar por el total; el movimiento de tesorería (y la conciliación) usa el neto, mientras el recibo conserva el total aplicado. `GET api/cxc/facturas/{id}/retencion-iva-sugerida` calcula el sugerido con el tipo de agente del cliente; el monto lo decide el usuario y una diferencia solo advierte. Un cliente no agente exige `confirmaClienteNoAgente`.

**Constancias y reporte (RN-22, RN-23).** El sistema registra los datos de la constancia; no la emite. `POST api/retenciones/{retencionId}/constancia` y `POST api/retenciones/constancias/{id}/anular` no tocan asientos y se permiten con el período cerrado. El estado pendiente o vencido se deriva en la API con el parámetro `PLAZO_CONSTANCIA_ISR`. `GET api/retenciones/reporte` (`anio`, `mes`, `sentido`, `concepto`, `nit`, `estadoConstancia`) lee `vw_retencion_reporte`, agrupa por el mes de la factura y concilia contra las cuentas de retención; las diferencias por cobros en otro mes se marcan con `periodoContableDistinto`. El CSV lo genera el frontend.

**Endpoints y roles.** `api/retenciones/parametros` (`GET` y `GET {id}` con `Roles.ReportesFiscales`; `POST` y `PUT {id}` solo Administrador, con auditoría; `PUT` cambia únicamente nota, estado y vigencia final, y una reforma es siempre una versión nueva), `api/retenciones/**` con `Roles.ReportesFiscales` (Administrador o Contador; el Vendedor recibe `403`), `api/cxp/facturas` y su revisión con `Roles.GestionCxP`, y los cobros con `Roles.RegistroPagos`. `api/configuracion-fiscal` y `api/contrapartes` agregan las cuentas de retención, el fundamento de ISR y el tipo de agente de IVA. Toda escritura pasa por `AuditoriaService` (RN-08); `GuardadoAuditado` centraliza el guardado auditado y la traducción de errores de la base (`55000`, `23505`) a `409`.

**Frontend.** Rutas `/catalogo/parametros-retencion` y `/libros/retenciones` (visibles para Administrador y Contador; el Contador ve los parámetros en solo lectura), panel de retención en el formulario de factura CxP, filtro de facturas por revisar, captura del IVA retenido en el cobro con resumen de neto, y campos de retención en Contrapartes y Configuración fiscal.

**Decisiones.**

- Delta no es agente de retención de IVA: si la configuración fiscal lo marca, solo se avisa (RN-20).
- La retención de ISR se reconoce al registrar la factura (no al pagar), con el saldo neto como fuente única; `retencion` es append-only por privilegios y guarda una instantánea (base, tasa, parámetro, cuenta, montos).
- Los parámetros de retención son una tabla propia (no se reutiliza `impuesto`) con el mismo patrón de vigencia y los mismos códigos de error `55000`.
- Un tercero sin datos de retención se advierte y se marca para revisión; no se bloquea el registro.
- El ajuste de la retención por nota de crédito posterior y el registro del enteramiento a SAT están implementados (I3b-2, ver las secciones siguientes).
- Fuera de alcance: retención de IVA como agente y a pequeño contribuyente, porque Delta no es agente de retención de IVA.

## Fiscal: retención de ISR a no residentes (M10, I3b-1)

Migración `database/17_retencion_no_residentes.sql`. Alcance implementado: catálogo de tipos de renta del art. 104, retención definitiva de ISR al pagar a un proveedor no residente con el tipo de cambio de Banguat del día del pago, y su inclusión en el reporte de retenciones. El ajuste por nota de crédito posterior (H11), el enteramiento a SAT (H12), el país del no residente y el calendario de feriados se describen en la sección siguiente (I3b-2).

**Catálogo (RN-29).** Los tipos de renta son filas de `parametroretencion` con concepto `ISR_NO_RESIDENTE` (código = tipo de renta, tasa de 0 a 100, sin tramo, umbral ni día límite), administrados por las rutas existentes `api/retenciones/parametros`; no hay endpoint propio. `contraparte.tipo_renta_nr_codigo` lo guarda el proveedor no residente; `ContrapartesController` exige un código vigente hoy o régimen EXENTO con fundamento. La cuenta 2.1.03 "ISR retenido a no residentes por pagar" y su asignación en `ConfiguracionFiscal.cuenta_isr_nr_retenido_pagar_id` las completa la migración 18 (ver la sección siguiente); si la cuenta no está configurada, el pago responde `409`.

**Factura (RN-19).** `RetencionRules.EvaluarCondicionesPrevias` devuelve "no aplica, se retiene al pagar" para un no residente con tipo de renta o exento con fundamento; sin ellos la factura queda `PENDIENTE` de revisión.

**Retención al pagar (RN-30, RN-31).** `RetencionService.PrepararIsrNoResidenteAsync` es el único cálculo: lo usan `POST api/cxp/pagos/retencion-previa` (solo calcula, `RegistroPagos`) y `POST api/cxp/pagos`. Por aplicación resuelve el tipo de renta (el enviado o el del proveedor), el parámetro vigente en la fecha del pago y el tipo de cambio con `TipoCambioService.ResolverParaDocumentoAsync`; `RetencionNoResidenteRules` calcula base y retención. La respuesta del pago agrega `retenciones`. `TesoreriaService` pasa las retenciones al asiento tanto en pagos GTQ como en pagos con documento o cuenta en moneda extranjera: `AsientoTesoreriaBuilder.ConstruirLiquidacion` y `ConstruirConRetenciones` ponen la línea de retención del mismo lado que el banco y el banco se reduce por ella. `RetencionService` guarda la fila `ISR_NORES` (una por aplicación) junto al pago en la misma transacción auditada. La retención no se resta del saldo de la factura.

**Reporte (RN-23).** `ReporteRetencionService` incluye el concepto `ISR_NORES`: período = mes del pago, estado de constancia `NO_APLICA` (no cuenta como pendiente), totales por tipo de renta, columnas de tipo de renta, moneda, monto origen y tipo de cambio en la respuesta y en el CSV, y conciliación contra la cuenta de ISR a no residentes. La constancia de esta retención no existe: `POST api/retenciones/{id}/constancia` responde `409`.

**Frontend.** Los tipos de renta se administran en `/catalogo/parametros-retencion` (concepto "ISR no residentes"); Contrapartes muestra el combo "Tipo de renta (art. 104)" a proveedores no residentes; el pago CxP muestra `PanelRetencionIsrNoResidente` (tipo de renta editable por aplicación, tasa, tipo de cambio, base, retención y neto al banco, calculados por la API); Configuración fiscal agrega la cuenta de ISR a no residentes; el reporte agrega el concepto y las columnas.

## Fiscal: enteramiento, ajuste por nota de crédito y feriados (M10, I3b-2)

Migración `database/18_enteramiento_ajuste_feriado.sql` (re-ejecutable). Alcance: enteramiento a SAT del ISR retenido a no residentes (RN-32), plazo con días hábiles (RN-33), ajuste por nota de crédito posterior (RN-34), país obligatorio del no residente (RN-35) y calendario de feriados editable (RN-36). Todo se limita al concepto `ISR_NORES`.

**Enteramiento (RN-32).** `EnteramientosController` (`api/retenciones/enteramientos`, `ReportesFiscales`) expone `GET saldo`, `POST vista-previa`, `POST` (registrar), `POST {id}/anular` y `GET` (lista por período y estado). `EnteramientoService.RegistrarAsync` valida la solicitud (`EnteramientoRules`), el banco (activo y en GTQ, vía `TesoreriaService`), la cuenta de ISR a no residentes por pagar y el saldo, y dentro de `_auditoria.EjecutarAsync` crea el movimiento manual de egreso con su asiento (`TesoreriaService.PrepararManualAsync` y `PersistirAsync`) y la fila de `enteramiento`. La verdad del saldo está en el trigger `trg_enteramiento_valida`, que toma un `pg_advisory_xact_lock` por período para serializar enteros simultáneos, exige banco en moneda funcional y rechaza montos sobre el saldo (`55000` se mapea a `409`). Anular no usa `sp_reversar_asiento` (rechaza asientos de tesorería): crea un movimiento manual de ingreso y su asiento compensatorio con fecha de hoy, y marca el enteramiento `ANULADO` (el trigger `trg_enteramiento_inmutable` solo admite ese cambio y prohíbe el `DELETE`).

**Plazo y feriados (RN-33, RN-36).** `GET .../saldo` lee `vw_saldo_enterar` y `DiaHabilRules` calcula `fechaVencimiento`, `diasParaVencer`, `estadoVencimiento`, `avisoMulta` y `calendarioIncompleto` con los feriados de la tabla `feriado`; las filas de medio día no entran en el conjunto de inhábiles. `FeriadosController` (`api/feriados`) lista por año con `ReportesFiscales` y escribe (`POST`, `PUT {fecha}`, `DELETE {fecha}`) solo con `Administrador`, con bitácora.

**Ajuste por nota de crédito (RN-34).** `FacturaService.RegistrarAsync` llama a `AjusteRetencionService.RegistrarSiAplicaAsync` dentro de la transacción de registro de una factura de compra, solo para notas de crédito con documento de origen: por cada retención `ISR_NORES` del origen cuyo período ya tiene enteramiento vigente inserta un ajuste `PENDIENTE` (idempotente por `uq_ajuste_retencion_nc`). `AjustesRetencionController` (`api/retenciones/ajustes`) lista, consulta y resuelve con motivo y referencia. `GET api/cxp/facturas` agrega `ajusteRetencionPendiente`. Una nota registrada antes del pago no necesita código propio: la retención se calcula sobre el monto aplicado, ya neto.

**País (RN-35).** `Contraparte.Pais` (`contraparte.pais`) lo valida `ContraparteRules.ValidarPais` en `ContrapartesController` (400 si falta en un no residente; se guarda vacío en un residente). `vw_retencion_reporte` lo expone con el indicador `ajuste_pendiente`; el reporte, la tabla y el CSV (`Pais`, `AjustePendiente`) los muestran, y el enterado y el saldo por enterar del resumen provienen de `vw_saldo_enterar`, no de la suma de filas.

**Catálogo de cuentas.** La migración 18 es una excepción a que las migraciones no siembran el plan de cuentas: crea, solo si faltan, `2` Pasivo, `2.1` Pasivo corriente (únicamente si `2` es de tipo Pasivo) y `2.1.03` ISR retenido a no residentes por pagar (únicamente si `2.1` es Pasivo), y asigna `2.1.03` en `configuracionfiscal.cuenta_isr_nr_retenido_pagar_id` si estaba vacío. Con `ON CONFLICT (codigo) DO NOTHING` y un `UPDATE` condicionado, la segunda ejecución no cambia nada.

**Frontend.** La pantalla de Retenciones suma el enteramiento (saldo por período con la insignia de vencimiento, formulario con vista previa de la partida, anulación con motivo), los ajustes por nota de crédito (resolver con motivo y referencia), los feriados (edición solo para el Administrador) y el país en Contrapartes; la lista de CxP muestra la insignia de ajuste pendiente.

## Fiscal: multimoneda y diferencial cambiario (M10, I3a)

Migración `database/16_multimoneda.sql`. Alcance implementado: documentos de CxC y CxP en USD o EUR con su equivalente en GTQ, tipo de cambio de Banguat con respaldo manual, diferencial cambiario realizado en cobros, pagos y transferencias, cuentas bancarias en USD, revaluación de fin de mes con reversa y reporte de saldos y antigüedad. Los libros y asientos se llevan siempre en GTQ (RN-06).

**Tipo de cambio (RN-25).** `TipoCambioService` consulta `historialtipocambio` primero y solo llama a Banguat si no hay fila activa para la moneda y la fecha. El cliente SOAP vive detrás de `IBanguatClient` (`BanguatSoapClient`, con `BanguatXml` como parser puro): pide una ventana de 10 días hacia atrás en una sola llamada para resolver fines de semana y feriados, aplica un timeout por intento y un reintento ante errores transitorios, y cualquier falla termina en `BanguatNoDisponibleException`. USD toma la referencia diaria; EUR combina la cotización de venta en USD por EUR con la referencia USD/GTQ del mismo día. La configuración está en la sección `Banguat` de `appsettings.json` (`Endpoint`, `TimeoutSegundos`, `Reintentos`, `EsperaReintentoMs`, `VentanaDias`; en la Container App, `Banguat__Endpoint`). Si Banguat no responde, el servicio devuelve `disponible = false` con el último valor guardado como propuesta y el registro de documentos con esa fecha responde `409` hasta que un Contador capture la tasa manual con motivo. No hay proceso programado: la caché se llena bajo demanda. Las pruebas de integración sustituyen `IBanguatClient` por `FakeBanguatClient` y la CI nunca llama a Banguat. `trg_historialtipocambio_inmutable` impide editar o dar de baja una tasa usada.

**Documentos en moneda extranjera (RN-24).** `FacturaFiscalService.PrepararAsync` resuelve el tipo de cambio con `TipoCambioService` (o hereda el de la factura de origen en una nota de crédito) y `MotorFiscal` calcula cada línea en GTQ a partir del importe original; el cliente no envía el tipo de cambio. `FacturaService` guarda moneda, importe original, tipo de cambio, fuente y marca de arrastrado junto con el asiento, que no cambia de forma porque ya recibe importes en GTQ. `vw_saldodocumentocxc/cxp` exponen el saldo en la moneda del documento (`saldo_pendiente`) y en GTQ (`saldo_pendiente_gtq`); `fn_limite_pago_*` y `fn_nota_credito_valida_*` validan el límite en moneda original y exigen que el saldo en GTQ cierre en cero con el original.

**Diferencial realizado (RN-26, RN-27).** `TesoreriaService.PrepararPagoAsync` valida con `PagoMonedaRules`, valora cada aplicación (cuenta USD: monto por la referencia de la fecha del pago; cuenta GTQ: efectivo digitado) y `DiferencialCambiarioRules` calcula el diferencial contra el valor en libros del documento. `AsientoTesoreriaBuilder.ConstruirLiquidacion` debita o acredita el banco por el efectivo, la cuenta de control por el valor en libros y el diferencial a las cuentas de ganancia (crédito) o pérdida (débito) realizada, por lo que cuadra por construcción. Las aplicaciones guardan `monto_aplicado_gtq`, `valor_gtq`, `diferencial_gtq` y la tasa del pago. Las cuentas bancarias declaran su moneda (GTQ o USD, inmutable con movimientos) y sus movimientos guardan `monto_gtq` y `tipo_cambio`. Una transferencia entre cuentas de distinta moneda usa `ConstruirTransferenciaCruzada` y registra la diferencia contra la referencia de la fecha. La conciliación opera en la moneda de la cuenta.

**Revaluación (RN-28).** `SaldoAlCorteService` calcula los saldos de documentos y bancos a una fecha de corte y lo comparten `RevaluacionService` y `ReporteSaldosService`. `RevaluacionService` arma las partidas con `RevaluacionRules`, y en una transacción auditada crea el asiento de cierre (último día del mes, cuentas de diferencial no realizado), su reversa automática (día 1 del mes siguiente), la fila de `revaluacion` y sus `revaluacionpartida`. Anular crea los contra-asientos de ambos y marca la revaluación `Anulada`; `sp_reversar_asiento` y la API rechazan la reversa manual de estos asientos. No toca documentos ni pagos.

**Reporte de saldos y antigüedad.** `GET api/reportes/saldos-antiguedad` (`tipo`, `moneda`, `corte`) agrupa por tramo de vencimiento, totaliza por moneda sin mezclarlas y concilia el total en GTQ con el saldo del mayor, explicando la diferencia con la revaluación vigente. El libro de compras y ventas y el libro diario agregan moneda, importe original, tipo de cambio y la marca de revaluación.

**Endpoints y roles.** `api/monedas` y `GET api/tipos-cambio` (cualquier sesión, incluido el Vendedor); `api/tipos-cambio/historial`, captura manual, sustitución por Banguat y baja con `Roles.GestionTesoreria`; `api/revaluaciones/**` con `Roles.GestionTesoreria`; `api/reportes/saldos-antiguedad` con `Roles.ReportesFiscales`; las cuentas de diferencial en `api/configuracion-fiscal` solo las escribe el Administrador. El Vendedor registra facturas y notas de CxC en moneda extranjera y recibe `403` en el resto. Toda escritura pasa por `AuditoriaService`.

**Frontend.** Rutas `/catalogo/tipos-cambio`, `/revaluaciones` y `/reportes/saldos-moneda` (Administrador y Contador). `SelectMoneda` y `PanelTipoCambio` (con `useTipoCambio`) se comparten entre facturas, cuentas bancarias, transferencias y revaluación; el formulario de factura muestra el total original, la tasa con su fuente y el total en GTQ; el cobro y el pago piden el efectivo en Q con cuenta GTQ y muestran el diferencial estimado; la configuración fiscal suma las cuatro cuentas cambiarias.

**Decisiones.**

- La moneda funcional es GTQ: asientos, libros y saldos de cuentas se llevan en quetzales y el importe original solo se conserva en los documentos, aplicaciones y movimientos.
- Una sola tasa activa por fecha y moneda; una tasa usada nunca se edita ni se da de baja.
- Ninguna propuesta de Banguat se convierte sola en dato: la tasa manual exige motivo, usuario y perfil Administrador o Contador.
- El diferencial realizado se calcula contra el tipo de cambio del documento; la revaluación es no realizada y se revierte sola, de modo que no altera el diferencial ni los saldos de documentos.
- Fuera de esta versión: cobros con retención de IVA de cliente sobre facturas en moneda extranjera, cuentas bancarias en EUR y pago de documentos en GTQ desde cuentas USD.

## Relación con el modelo de datos

- Núcleo contable (M2, M3) → `AsientoContable`, `LineaAsiento`, `CuentaContable`, `CentroCosto`.
- CxC/CxP (M4, M5) → `DocumentoCxC`, `DocumentoCxP`, `AplicacionPagoCliente`, `AplicacionPagoProveedor`.
- Tesorería (M6) → `CuentaBancaria`, `MovimientoTesoreria` (cada uno con su asiento), `ConciliacionBancaria`, `DetalleConciliacion`.
- Seguridad y auditoría (M8) → `Usuario`, `Perfil`, `BitacoraAuditoria` (poblada por la API, no por trigger).
- Cierre contable → `PeriodoContable` y `SaldoCuentaPeriodo` (saldos consolidados e inmutables por periodo).
- Reportes (M7) → sin tablas propias: `fn_reporte_saldos` combina `SaldoCuentaPeriodo` de periodos cerrados con asientos en vivo.
- Fiscal (M10) → `Impuesto`, `ConfiguracionFiscal` (fila única), `Moneda` (funcional, USD y EUR) y los campos fiscales y de DTE de `Contraparte`, `DocumentoCxC`/`DocumentoCxP` y sus líneas.
- Retenciones (M10, I2) → `ParametroRetencion`, `Retencion`, `ConstanciaRetencion`, `vw_retencion_reporte`, las cuentas de retención de `ConfiguracionFiscal` y los campos de retención de `Contraparte` y `DocumentoCxP`.
- Retención a no residentes (M10, I3b-1) → conceptos `ISR_NO_RESIDENTE` en `ParametroRetencion` e `ISR_NORES` en `Retencion` (con moneda, monto original, tipo de cambio y tipo de renta), `Contraparte.tipo_renta_nr_codigo`, la cuenta de ISR a no residentes de `ConfiguracionFiscal` y las columnas nuevas de `vw_retencion_reporte`.
- Enteramiento, ajuste y feriados (M10, I3b-2) → `Enteramiento`, `AjusteRetencion`, `Feriado`, `Contraparte.pais`, las vistas `vw_saldo_enterar` y `vw_retencion_reporte` (con `pais` y `ajuste_pendiente`) y la cuenta de ISR a no residentes por pagar (2.1.03) de `ConfiguracionFiscal`.
- Multimoneda (M10, I3a) → `HistorialTipoCambio`, `Revaluacion`, `RevaluacionPartida`, las cuentas cambiarias de `ConfiguracionFiscal` y las columnas de moneda, importe original y tipo de cambio de documentos, aplicaciones, `CuentaBancaria` y `MovimientoTesoreria`.

Ningún módulo del frontend accede directamente a la base de datos; todo pasa por la
API.

## Pruebas y calidad

**Backend** (`backend/tests/DeltaERP.Tests`, xUnit). Dos grupos:

- Unitarias (`Unit/`): reglas puras del dominio (`AsientoFacturaBuilder`,
  `AsientoTesoreriaBuilder`, `FacturaRules`, `PagoRules`, `PartidaDobleValidator`,
  `TesoreriaRules`, `EstadosFinancieros`, `MotorFiscal`, `ImpuestoRules`,
  `ConfiguracionFiscalRules`, `NitRules`, `DteRules`, `RetencionRules`,
  `ParametroRetencionRules`, `ContraparteRules`) y `ErroresPostgres`, sin base de datos.
- Integración (`Integration/`, trait `Category=Integration`): levantan la API con
  `WebApplicationFactory` contra un PostgreSQL 16 real, de modo que los triggers
  (RN-01, RN-03, RN-05) actúan igual que en producción. Cubren todos los controladores
  de la API: asientos, auth, centros de costo, contrapartes, cuentas bancarias,
  cuentas contables, conciliaciones, configuración fiscal, CxC, CxP, impuestos, libros,
  libro fiscal, movimientos de tesorería, periodos contables y reportes, y el esquema
  fiscal (`FiscalEsquemaTests`), las retenciones (parámetros, facturas CxP, cobros CxC,
  constancias, reporte y esquema). Las pruebas de login verifican la emisión del
  token (`AuthController`/`TokenService`) con un cliente anónimo y el esquema JWT
  real; el resto usa `TestAuthHandler`. Las operaciones cuya autorización vive en un
  procedimiento almacenado (cerrar y reabrir periodo) se validan contra el perfil
  del usuario en la base de datos, no contra el encabezado de prueba.

Soporte de las pruebas de integración (`Support/`):

- `PostgresFixture` lee la cadena de conexión de la variable `TEST_PG_CONN`; si falta,
  las pruebas fallan (no se omiten). El nombre de la base debe contener `test`. Al
  iniciar recrea el esquema `public` y aplica `database/01` a `05` y `07` a `15`; no aplica
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
`postgres:16`. `backend-build-check.yml` ejecuta en cada push a `dev` cuatro jobs en
paralelo: `backend` (`dotnet test` con cobertura en formato Cobertura), `frontend`
(`npm run test:ci`, que genera `junit`, `lcov` y Cobertura), `jscpd` (gate de duplicación
de bloques de 80 tokens o más sobre C#, JS y JSX en `backend/src` y `frontend/src`) y
`cobertura-diff`, que exige con `diff-cover` al menos 80 % de cobertura en las líneas
cambiadas respecto a `origin/main`; este último se omite si fallan las pruebas. Un job
`resultado` falla si alguno de los anteriores no terminó en éxito. El resumen del run
muestra el porcentaje total de cobertura de backend y frontend, y el detalle de duplicación
y de líneas no cubiertas. Ambos workflows usan Node 24. `sonarcloud-analysis.yml` corre
las suites con cobertura en cada Pull Request y en cada push a `main`, con la cobertura
enviada a SonarCloud.

**Gate de migraciones.** En cada PR a `main`, `migrations-check.yml` crea una rama Neon temporal desde `production`, aplica las migraciones pendientes con `database/run_migrations.sh` y borra la rama al terminar; el job `migrar-copia-prod` es check requerido en la protección de `main`.

**Quality gate de SonarCloud.** El análisis espera el resultado del quality gate
(`sonar.qualitygate.wait=true`) y su resultado determina el estado del workflow. Cobertura mínima de 80 % sobre código nuevo y
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
local. CI no migra la rama `development`: sus migraciones se aplican a mano y deben dejar el
mismo registro en `schema_migrations` que escribe `run_migrations.sh`.

La integración y el despliegue continuo corren en GitHub Actions, en cinco workflows
independientes (`.github/workflows/`):

- `backend-deploy.yml`: en cada push a `main` (ignora los cambios que solo tocan
  `backend/tests/`), aplica las migraciones pendientes
  contra la rama `production` de Neon con el rol propietario (`run_migrations.sh` ejecuta cada script con
  `psql --single-transaction`: si falla, se revierte completo y no se registra como aplicado; el paso
  usa `pipefail`, de modo que un fallo del script no queda enmascarado por el registro del log).
  Solo si las migraciones terminan bien, construye y publica la imagen del backend y actualiza
  Azure Container Apps con `az` directo. Después verifica `/health` con hasta 12 intentos
  y deja en el resumen del run la imagen, el digest, la revisión y el commit. Los despliegues
  no se cancelan entre sí (concurrency `backend-deploy`).
- `frontend-alias.yml`: tras cada deploy de producción en Vercel, reapunta el
  dominio corto del proyecto al nuevo despliegue. Tolera errores transitorios de la API de
  Vercel mientras espera el deploy; una ejecución nueva cancela la anterior.
- `sonarcloud-analysis.yml`: en cada Pull Request y en cada push a `main` corre las
  pruebas con cobertura y analiza calidad y seguridad del código (backend y frontend)
  en SonarCloud. El paso `end` corre aunque fallen las pruebas y el código de salida de
  las pruebas se propaga al final. Las ejecuciones anteriores de un mismo Pull Request se cancelan.
- `backend-build-check.yml`: en cada push a `dev` corre la suite completa de pruebas
  del backend y del frontend, el gate de duplicación y el de cobertura de líneas cambiadas,
  antes de que el cambio llegue a un Pull Request o a `main`. Un job `resultado` al final
  resume el estado de todos; las ejecuciones anteriores del mismo ref se cancelan.
- `migrations-check.yml`: gate en Pull Request a `main`. Crea una rama temporal de Neon desde
  `production`, aplica las migraciones sobre ella y la borra al terminar.

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
  34 de las 36 tablas (`plantillaasiento` y `lineaplantillaasiento` no tienen GRANT); sin `UPDATE` en las tablas inmutables
  (`bitacoraauditoria`, `saldocuentaperiodo`, `movimientotesoreria`, `lineaasiento`, `retencion`, `revaluacionpartida`, pagos,
  aplicaciones y detalles); `DELETE` solo en `detalleconciliacion`; `bitacoraauditoria` y
  `saldocuentaperiodo` solo con `INSERT` (se escriben vía procedimientos y triggers y se
  leen por vistas y funciones); `SELECT` en 7 vistas; `USAGE` en 27 secuencias; `EXECUTE`
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
