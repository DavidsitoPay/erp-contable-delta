# CODEMAP

Índice para ubicar código. Léelo primero; abre archivos solo en lo que señale. Rutas relativas al repo.

Abreviaturas: `API` = `backend/src/DeltaERP.Api`, `DOM` = `backend/src/DeltaERP.Domain`, `INF` = `backend/src/DeltaERP.Infrastructure`, `TI` = `backend/tests/DeltaERP.Tests/Integration`, `TU` = `backend/tests/DeltaERP.Tests/Unit`, `FC` = `frontend/src/components`, `FU` = `frontend/src/utils`, `SQL` = `database`.
Entidades: `DOM/Entities/<Nombre>.cs`. Reglas puras: `DOM/Rules/`. Controllers: `API/Controllers/`. Servicios: `API/Services/`. Tests frontend: `<archivo>.test.jsx` junto al componente.
Roles (`API/Auth/Roles.cs`): GestionCatalogo, GestionTesoreria, RegistroPagos, ReportesFiscales, GestionCxP = Administrador+Contador; GestionCxC suma Vendedor; Administrador solo admin.

## 1. Módulos funcionales

### Catálogo contable (E1): cuentas, centros de costo, periodos
- Controllers: `CuentasContablesController` (`api/cuentas`), `CentrosCostoController` (`api/centros-costo`), `PeriodosContablesController` (`api/periodos`; `POST {id}/cerrar`, `POST {id}/reabrir`).
- Dominio: `JerarquiaCuentas`, `VigenciaRules`, `PeriodoFiscalRules`. Entidades: CuentaContable, CentroCosto, PeriodoContable, BalanceSaldoCuenta.
- SQL: tablas `cuentacontable`, `centrocosto`, `periodocontable`, `saldocuentaperiodo` (01); `sp_cerrar_periodo`, `sp_reabrir_periodo` (versión vigente en 08); `fn_validar_padre_sin_saldo` + `trg_validar_padre_sin_saldo` (10); `trg_prevenir_eliminacion_cuentacontable` (03).
- Frontend: `FC/Catalogo/` (`CuentasContables`, `CentrosCosto`, `PeriodosContables`, `CatalogoCrud`, `CierreVigencia`, `useAccionCatalogo`), `FC/CamposVigencia`, `FC/CamposAnioMes`; `FU/cuentas.js`, `FU/vigencia.js`. api.js: `cuentasApi`, `centrosCostoApi`, `periodosApi`.
- Tests: TI `CuentasContablesControllerTests`, `CentrosCostoControllerTests`, `PeriodosContablesControllerTests`, `BalanceJerarquicoEsquemaTests`; TU `JerarquiaCuentasTests`, `VigenciaYPeriodoRulesTests`.
- RN: 02, 15.

### Asientos contables (E2)
- Controller: `AsientosController` (`api/asientos`; `POST` registrar, `POST {id}/reversar` solo admin).
- Dominio: `PartidaDobleValidator`; servicios `ReversaAsiento`, `ValidacionContable`. Entidades: AsientoContable, LineaAsiento.
- SQL: tablas `asientocontable`, `lineaasiento`, `plantillaasiento`, `lineaplantillaasiento` (01); `fn_validar_partida_doble` (07), `trg_bloquear_periodo_cerrado_linea`, `trg_prevenir_eliminacion_asiento` (03); `sp_reversar_asiento` (vigente en 16, previas 04/08/12).
- Frontend: `FC/Asientos/RegistrarAsiento`, `FC/Libros/DialogoReversa`. api.js: `asientosApi`.
- Tests: TI `AsientosControllerTests`; TU `PartidaDobleValidatorTests`.
- RN: 01, 02, 03, 08, 15.

### Libros y balance de saldos (E3)
- Controller: `LibrosController` (`api/libros/balance-saldos`, `/diario`, `/mayor`).
- SQL: `vw_balance_saldos` (vigente en 10), `vw_saldocuentaperiodo_vigente` (08), `fn_reporte_saldos` (11).
- Frontend: `FC/Libros/` (`BalanceSaldos`, `LibroDiario`, `LibroMayor`). api.js: `librosApi`.
- Tests: TI `LibrosControllerTests`, `BalanceJerarquicoEsquemaTests`.

### Contrapartes (clientes/proveedores)
- Controller: `ContrapartesController` (`api/contrapartes`). Dominio: `ContraparteRules`, `NitRules`. Entidad: Contraparte.
- SQL: tabla `contraparte` (01); columnas fiscales/retención en 13 y 15; `tipo_renta_nr_codigo` en `database/17`; `pais` en `database/18`.
- Frontend: `FC/Contrapartes/` (`Contrapartes`, `CamposFiscalesContraparte`); `FU/nit.js`, `FU/formularioContraparte.js`, `FU/paises.js`. api.js: `contrapartesApi`; combo de tipo de renta NR (art. 104) en CamposFiscalesContraparte; campo `pais` ISO-2 (obligatorio si no residente) en CamposFiscalesContraparte y columna País en la lista.
- Tests: TI `ContrapartesControllerTests`, `ContrapartesFiscalTests`, `ContrapartesRetencionTests`; TU `ContraparteRulesTests`, `ContraparteRulesPaisTests`, `NitRulesTests`, `ContraparteYConfiguracionRetencionRulesTests`.
- RN: 04, 13, 19, 29, 35.

### Cuentas por cobrar (E4) y por pagar (E5)
- Controllers: `CxCController` (`api/cxc`: `facturas`, `facturas/{id}/retencion-iva-sugerida`, `pagos`), `CxPController` (`api/cxp`: `facturas`, `facturas/retencion-previa`, `facturas/{id}/revision-retencion`, `pagos`, `pagos/retencion-previa`); `GET api/cxp/facturas` devuelve `ajusteRetencionPendiente`. Base compartida: `FacturaControllerBase`.
- Servicios: `FacturaService`, `FacturaFiscalService`, `PerfilFactura`, `SaldoAlCorteService`. Dominio: `FacturaRules`, `PagoRules`, `PagoMonedaRules`, `AsientoFacturaBuilder`, `SaldoAntiguedadRules`. Modelos: `API/Models/FacturaDetalle`, `LineaFacturaDetalle`, `PagoMonedaModels`.
- Entidades: DocumentoCxC/CxP (base `DocumentoFacturaBase`), LineaDocumentoCxC/CxP, ReciboPagoCliente, PagoProveedorCabecera, AplicacionPago* (base `AplicacionPagoBase`), SaldoDocumentoCxC/CxP.
- SQL: tablas `documentocxc|cxp`, `lineadocumentocxc|cxp`, `recibopagocliente`, `pagoproveedorcabecera`, `aplicacionpagocliente|proveedor` (01); `fn_limite_pago_cxc|cxp` (vigente 16; también 03/13/15), `fn_nota_credito_valida_cxc|cxp` (vigente 16 cxc, 16 cxp; también 13/15), `fn_linea_impuesto_valido` (13), `vw_saldodocumentocxc|cxp` (vigente 16).
- Frontend: `FC/CxC/CuentasPorCobrar`, `FC/CxP/CuentasPorPagar`, `FC/CuentasPorCobrarPagar/` (formulario, tablas, `PanelLiquidacion`, `PanelRetencionIsr`, `PanelRetencionIsrNoResidente`, `MarcoPanelRetencion`, `RetencionIvaCobro`, hooks `use*` (`useConsultaPrevia`, `useRetencionPrevia`, `useRetencionPreviaPago`)); `FU/liquidacion.js`, `FU/fiscal.js`, `FU/cambio.js`. `TablaFacturas` muestra el ajuste de retención pendiente por NC, que se resuelve con `FC/Retenciones/ResolverAjusteDialog`. api.js: `cxcApi`, `cxpApi` (`retencionPreviaPago`: `POST api/cxp/pagos/retencion-previa`).
- Tests: TI `CxCControllerTests`, `CxPControllerTests`, `FacturasCxCFiscalTests`, `FacturasCxPFiscalTests`, `FacturasCxPRetencionTests`, `FacturasMonedaExtranjeraTests`, `CobrosCxCRetencionTests`, `PagosMonedaExtranjeraTests`, `PagosCxPRetencionNoResidenteTests`; TU `FacturaRulesTests`, `PagoRulesTests`, `PagoRulesNotaCreditoTests`, `PagoMonedaRulesTests`, `AsientoFacturaBuilderTests`, `SaldoAntiguedadRulesTests`.
- RN: 04, 05, 12, 14, 15, 16, 21, 24, 26.

### Tesorería y conciliación bancaria (E6)
- Controllers: `CuentasBancariasController` (`api/cuentas-bancarias`), `MovimientosTesoreriaController` (`api/movimientos-tesoreria`, `POST transferencias`), `ConciliacionesController` (`api/conciliaciones`: `{id}/movimientos`, `{id}/cancelar`, `{id}/finalizar`).
- Servicio: `TesoreriaService` (`PrepararManualAsync`, `PersistirAsync`, reutilizados por `EnteramientoService`). Dominio: `TesoreriaRules`, `AsientoTesoreriaBuilder`. Modelos: `TesoreriaModels`. Entidades: CuentaBancaria, MovimientoTesoreria, ConciliacionBancaria, DetalleConciliacion, ConciliacionResumen, SaldoCuentaBancaria.
- SQL: tablas `cuentabancaria`, `movimientotesoreria`, `conciliacionbancaria`, `detalleconciliacion` (01); `fn_movimiento_inmutable`, `fn_conciliacion_inmutable`, `fn_detalle_conciliacion_valida`, `vw_conciliacion_resumen` (09); `sp_finalizar_conciliacion` (vigente 09; perfil en 07); `vw_saldocuentabancaria` (vigente 16); `fn_movimiento_moneda_valida`, `fn_cuentabancaria_moneda_inmutable` (16).
- Frontend: `FC/Tesoreria/` (`Tesoreria`, `CuentasBancarias`, `Movimientos`, `FormularioMovimiento`, `TransferenciaMoneda`, `Conciliaciones`, `ConciliacionDetalle`). api.js: `cuentasBancariasApi`, `movimientosTesoreriaApi`, `conciliacionesApi`.
- Tests: TI `CuentasBancariasControllerTests`, `CuentasBancariasMonedaTests`, `MovimientosTesoreriaControllerTests`, `ConciliacionesControllerTests`, `TesoreriaEsquemaTests`; TU `TesoreriaRulesTests`, `TesoreriaRulesMonedaTests`, `AsientoTesoreriaBuilderTests`, `AsientoTesoreriaMonedaTests`.
- RN: 01, 10, 26, 27.

### Reportes y estados financieros (E7)
- Controllers: `ReportesController` (`api/reportes/balance-general`, `/estado-resultados`), `ReporteSaldosController` (`api/reportes/saldos-antiguedad`). Servicio: `ReporteSaldosService`. Dominio: `EstadosFinancieros`, `SaldoAntiguedadRules`. Modelos: `ReporteSaldosModels`.
- SQL: `fn_reporte_saldos` (11). Entidad: SaldoReporteCuenta.
- Frontend: `FC/Reportes/` (`BalanceGeneral`, `EstadoResultados`, `SaldosMoneda`, `PaginaReporte`, `TablaReporte`), `FC/AccionesConsulta`; `FU/csv.js`, `FU/saldosMoneda.js`. api.js: `reportesApi`, `reportesSaldosApi`.
- Tests: TI `ReportesControllerTests`, `ReportesEsquemaTests`, `ReporteSaldosMonedaTests`; TU `EstadosFinancierosTests`; front `FC/Reportes/*.test.jsx`.

### Fiscal: IVA, libros fiscales, configuración fiscal (E10-F1)
- Controllers: `ConfiguracionFiscalController` (`api/configuracion-fiscal`; PUT solo admin), `ImpuestosController` (`api/impuestos`), `LibroFiscalController` (`api/libros/fiscal/ventas|compras`).
- Servicios: `ConfiguracionFiscalService`, `LibroFiscalService`. Dominio: `MotorFiscal`, `ImpuestoRules`, `ConfiguracionFiscalRules`, `DteRules`. Modelos: `FiscalModels`, `LibroFiscalModels`. Entidades: Impuesto, ConfiguracionFiscal.
- SQL: todo en `database/13_fiscal_iva.sql` (`impuesto`, `configuracionfiscal`, `fn_impuesto_sin_traslape`, `fn_impuesto_inmutable_si_usado`, `fn_configuracionfiscal_no_eliminable`, triggers de línea/nota de crédito). Cuenta del ISR a no residentes: `database/17`.
- Frontend: `FC/Catalogo/` (`Impuestos`, `FormularioImpuesto`, `ConfiguracionFiscal`), `FC/CuentasPorCobrarPagar/DatosDte`, `FC/Libros/LibroFiscal`; `FU/etiquetasFiscales.js`, `FU/formularioImpuesto.js`, `FU/libroFiscal.js`. api.js: `configuracionFiscalApi`, `impuestosApi`, `librosFiscalesApi`.
- Tests: TI `ImpuestosControllerTests`, `ConfiguracionFiscalControllerTests`, `LibroFiscalControllerTests`, `FiscalEsquemaTests`; TU `MotorFiscalTests`, `ImpuestoRulesTests`, `ConfiguracionFiscalRulesTests`, `DteRulesTests`.
- RN: 06, 07, 12, 13, 14, 16.

### Fiscal: retenciones ISR/IVA (E10-F2)
- Controllers: `ParametrosRetencionController` (`api/retenciones/parametros`; escritura solo admin), `ConstanciasRetencionController` (`api/retenciones/{id}/constancia`, `constancias/{id}/anular`), `ReporteRetencionesController` (`api/retenciones/reporte`), `EnteramientosController` (`api/retenciones/enteramientos`: `saldo`, `vista-previa`, `POST`, `{id}/anular`, `GET`), `AjustesRetencionController` (`api/retenciones/ajustes`: `GET`, `{id}`, `{id}/resolver`), `FeriadosController` (`api/feriados`; escritura solo admin).
- Servicios: `RetencionService`, `ConstanciaRetencionService`, `ReporteRetencionService`, `EnteramientoService`, `AjusteRetencionService` (hook en `FacturaService.RegistrarAsync`). Dominio: `RetencionRules`, `ParametroRetencionRules`, `RetencionNoResidenteRules`, `EnteramientoRules`, `DiaHabilRules`. Modelos: `RetencionModels`, `EnteramientoModels`. Entidades: ParametroRetencion, Retencion, ConstanciaRetencion, EntradasRetencion, VwRetencionReporte, Enteramiento, AjusteRetencion, Feriado, VwSaldoEnterar.
- SQL: `database/15_retenciones.sql` (`parametroretencion`, `retencion`, `constanciaretencion`, `vw_retencion_reporte`, triggers sin traslape/inmutable).
- SQL: `database/17_retencion_no_residentes.sql` (concepto `ISR_NO_RESIDENTE` y semilla art. 104 en `parametroretencion`; `retencion`: concepto `ISR_NORES`, columnas `aplicacion_pago_proveedor_id`, `moneda_id`, `monto_origen`, `tipo_cambio`, `tipo_cambio_fecha`, `tipo_renta_codigo`, índice `ux_retencion_nores_aplicacion`; `configuracionfiscal.cuenta_isr_nr_retenido_pagar_id` (+ cuenta `2.1.03` si hay catálogo); redefine `fn_parametroretencion_inmutable_si_usado` y `vw_retencion_reporte`).
- SQL: `database/18_enteramiento_ajuste_feriado.sql` (`enteramiento`, `ajuste_retencion`, `feriado` con semilla 2026-2027, `contraparte.pais`, cuentas `2`/`2.1`/`2.1.03` + asignación en `configuracionfiscal`, `vw_saldo_enterar`, redefine `vw_retencion_reporte` (+`pais`, `ajuste_pendiente`; enterado/saldo por periodo), `fn_enteramiento_valida/_inmutable`, `fn_ajuste_retencion_valida/_inmutable`; GRANTs propios).
- Frontend: `FC/Retenciones/` (parámetros, constancia, reporte, tablas, `ResolverAjusteDialog` con props `retencionId|notaCreditoId`, `Enteramiento`, `CeldasSaldoPeriodo` (celdas período/retenido/enterado/saldo compartidas por Enteramiento y ResumenRetenciones), `FormularioEnteramiento` (reusa `Tesoreria/SelectCuentaBancaria`), `VistaPreviaPartida`, `Feriados`); `FC/CuentasPorCobrarPagar/PanelRetencionIsr`, `RetencionIvaCobro`; `FU/retenciones.js`, `FU/parametrosRetencion.js`, `FU/enteramiento.js` (`hoyGuatemala`). api.js: `parametrosRetencionApi`, `retencionesApi`, `ajustesRetencionApi`, `enteramientosApi`, `feriadosApi`; catálogo ISR_NO_RESIDENTE (formulario sin umbral), columnas NORES y totales por tipo de renta en el reporte; `TablaRetenciones` con columna País e insignia de ajuste pendiente; CSV con Pais y AjustePendiente.
- Tests: TI `ParametrosRetencionControllerTests`, `ConstanciasRetencionTests`, `ReporteRetencionesTests`, `RetencionesEsquemaTests`, `EnteramientoEsquemaTests`, `EnteramientosTests`, `AjustesRetencionTests`, `FeriadosControllerTests`; TU `RetencionRulesTests`, `ParametroRetencionRulesTests`, `EnteramientoRulesTests`, `DiaHabilRulesTests`, `RetencionAsientosTests`, `RetencionNoResidenteRulesTests`, `ErroresPostgresRetencionTests`; front `FC/Retenciones/ResolverAjusteDialog.test.jsx`, `Enteramiento.test.jsx`, `enteramientoTestData.js` (helper), `FormularioEnteramiento.test.jsx`, `VistaPreviaPartida.test.jsx`, `Feriados.test.jsx`, `FU/enteramiento.test.js`.
- RN: 17 a 23, 29 a 36.

### Multimoneda, tipo de cambio y revaluación (E10-F3)
- Controllers: `MonedasController` (`api/monedas`), `TiposCambioController` (`api/tipos-cambio`: `historial`, `manual`, `sustituir-por-banguat`, `{id}/baja`), `RevaluacionesController` (`api/revaluaciones`: `vista-previa`, `{id}/anular`).
- Servicios: `TipoCambioService`, `RevaluacionService`, `ConsultasRevaluacion`, `SaldoAlCorteService`; cliente Banguat SOAP: `IBanguatClient`, `BanguatSoapClient`, `BanguatXml`, `BanguatOptions`. Dominio: `CambioRules`, `DiferencialCambiarioRules`, `RevaluacionRules`, `PagoMonedaRules`. Modelos: `TipoCambioModels`, `RevaluacionModels`. Entidades: Moneda, HistorialTipoCambio, Revaluacion.
- SQL: `database/16_multimoneda.sql` (`revaluacion`, `revaluacionpartida`, `trg_00_multimoneda_completar`, `fn_historialtipocambio_*`, `fn_documento_moneda_*`; redefine funciones/vistas de CxC/CxP/tesorería); tablas base `moneda`, `historialtipocambio` en 01.
- Frontend: `FC/Revaluaciones/`, `FC/SelectMoneda`, `FC/Reportes/SaldosMoneda`, ruta `/catalogo/tipos-cambio`; `FU/cambio.js`, `FU/revaluacion.js`. api.js: `monedasApi`, `tiposCambioApi`, `revaluacionesApi`.
- Tests: TI `TiposCambioControllerTests`, `RevaluacionesControllerTests`, `MultimonedaEsquemaTests`; TU `CambioRulesTests`, `DiferencialCambiarioRulesTests`, `RevaluacionRulesTests`, `BanguatSoapClientTests`, `BanguatXmlTests`, `MotorFiscalMonedaTests`. Doble de prueba: `FakeBanguatClient`.
- RN: 06, 24 a 28.

### Seguridad, roles y auditoría (E8)
- Ver sección 2 (auth). Auditoría: `API/Services/AuditoriaService`, `API/Controllers/GuardadoAuditado`; SQL `sp_registrar_auditoria` (04), `bitacoraauditoria` (01), `fn_bitacora_inmutable` (03). Perfiles: `FU/perfiles.js`; `FC/Login.jsx`.
- Tests: TI `AuthControllerTests`, `PermisosApiTests`; front `FC/Login.test.jsx`, `FU/perfiles.test.js`.
- RN: 08, 09.

### Carga inicial (E9, RN-11)
- Sin controller dedicado. Datos: `database/06_seed_dev.sql`, `database/demo/*.sql`; funciones de soporte en `database/02_functions.sql` (contenido no verificado en detalle).

## 2. Transversal

| Tema | Dónde |
|---|---|
| JWT / auth | `API/Program.cs` (AddJwtBearer, CORS vía `Cors:AllowedOrigins`, registro de servicios), `API/Auth/{JwtOptions,TokenService,Roles,ControllerExtensions}.cs`, `AuthController` (`POST api/auth/login`), sección `Jwt` en `API/appsettings.json` |
| Roles | constantes en `API/Auth/Roles.cs`; se usan como `[Authorize(Roles = Roles.X)]` por acción; frontend filtra por perfil en `frontend/src/App.jsx` y `FU/perfiles.js` |
| DbContext | `INF/Data/DeltaErpDbContext.cs` (único mapeo EF); excluido de coverage en Sonar |
| Errores Postgres | `API/Services/ErroresPostgres.cs` (SQLSTATE -> HTTP, restricciones únicas -> mensaje (incl. `ux_enteramiento_boleta`), `TraducirProcedimientoAsync`), `ErrorValidacion.cs`, `ControllerExtensions.Fallo`; tests TU `ErroresPostgresTests` |
| Guardado con auditoría | `GuardadoAuditado.GuardarAuditadoAsync` (usado por controllers de catálogo) |
| Frontend base | `frontend/src/App.jsx` (rutas), `main.jsx`, `services/api.js` (un objeto `*Api` por recurso), `tema.js`, `styles/{theme,components}.css`, `test/setup.js`, `test/reportesFixtures.js`, `test/temporizadores.js`; componentes compartidos `FC/CampoFormulario`, `FC/OpcionesSelect`, `FC/FormularioMotivo` |
| Frontend compartido | `FC/LogoMark.jsx`, `FC/ToggleTema.jsx` (+ `ToggleTema.test.jsx`), `FC/MenuPrincipal.jsx` (+ `MenuPrincipal.test.jsx`; nav sticky agrupado en 5 desplegables, lo usa `App.jsx`), `FC/useLineas.js` (+ `useLineas.test.js`), `FU/formato.js` (+ `formato.test.js`) |
| Rutas frontend | `/asientos`, `/catalogo/{cuentas,centros-costo,periodos,tipos-cambio,configuracion-fiscal,impuestos,parametros-retencion}`, `/libros/{balance,diario,mayor,fiscal,retenciones}`, `/retenciones/enteramiento`, `/catalogo/feriados`, `/contrapartes`, `/cxc`, `/cxp`, `/tesoreria`, `/revaluaciones`, `/reportes/{balance-general,estado-resultados,saldos-moneda}` |
| Migraciones | `database/NN_descripcion.sql` en orden numérico (01..19); `database/run_migrations.sh` aplica los pendientes, uno por transacción, registro en `schema_migrations`. Nunca se edita un script aplicado: corrección = archivo NN+1. Sin migraciones down. Los scripts 07/08/12/16 redefinen objetos de 03/04/05: la versión vigente es la del número mayor. |
| Permisos de BD | `database/14_rol_api.sql` (rol `delta_api`, matriz base); toda tabla/vista/rutina nueva usada por la API declara su GRANT en su propia migración (15, 16, 18) |
| Datos demo | `database/06_seed_dev.sql`, `database/demo/` (no son migraciones) |
| Fixtures de pruebas | `backend/tests/DeltaERP.Tests/Support/`: `PostgresFixture` (Postgres de CI, aplica migraciones), `ApiFactory`, `IntegrationCollection`, `TestAuthHandler`, `TestData`, `FakeBanguatClient`, `FacturaPayloads`, `RetencionPayloads`, `JsonExtensions`, `CompraNr` (`Compra`, `CompraNrExtensions`) |
| Docs vivas | `docs/{architecture,business-rules,data-dictionary,backlog-features-historias,plan-de-pruebas-inicial,azure-devops-checklist}.md`; `docs/entregado/` no se toca |

### CI y calidad

| Archivo | Qué hace |
|---|---|
| `.github/workflows/backend-build-check.yml` | push a `dev`: jobs paralelos `backend` (dotnet test + Postgres service), `frontend`, `jscpd`; `cobertura-diff` (diff-cover 80 % líneas cambiadas sobre artefactos de cobertura; omitido si fallan pruebas) y `resultado` (falla si algún job no fue success); % total de cobertura en el summary; `NODE_VERSION` único |
| `.github/workflows/sonarcloud-analysis.yml` | PR y push a `main`: tests con cobertura + dotnet-sonarscanner + análisis SonarCloud; exclusiones de coverage (`Program.cs`, `DeltaErpDbContext.cs`, `Entities/**`, `main.jsx`) y de CPD (`backend/tests/**`, `*.test.*`, `frontend/src/test/**`) en el propio workflow; `sonar.qualitygate.wait=true`; `end` corre aunque fallen las pruebas; caché NuGet; concurrency (cancela solo en PR) |
| `.github/workflows/backend-deploy.yml` | push a `main`/manual: aplica migraciones pendientes, construye y publica imagen (`devops/Dockerfile.backend`) en GHCR, actualiza Azure Container App; concurrency sin cancelar; migraciones con pipefail; `az` directo; health check post-deploy; resumen con digest y revisión |
| `.github/workflows/migrations-check.yml` | PR a `main`/manual: crea rama Neon efímera desde `production` (copy-on-write, `expires_at` +2 h), corre `database/run_migrations.sh` con pipefail, resumen sin DETAIL y borra la rama con `always()`; job `migrar-copia-prod` (check requerido); omitido en PRs de forks; secret `NEON_API_KEY`, var `NEON_PROJECT_ID`; acciones fijadas por SHA |
| `.github/workflows/frontend-alias.yml` | push a `main`/manual: espera el deploy de Vercel y re-apunta el alias `delta-erp-contable.vercel.app`; concurrency con cancel; tolera errores transitorios de la API de Vercel |
| `.github/requirements-ci.txt` | dependencias Python de CI (diff-cover), usado por job `cobertura-diff` |
| `frontend/package.json`, `frontend/vite.config.js` | script `test:ci` (vitest + coverage v8, salida cobertura/lcov/junit); `frontend/vercel.json` despliegue |
| `devops/recreate-containerapp.sh` | recrea el Container App de Azure |

## 3. Índice RN-xx -> dónde se aplica

Fuente de verdad del texto: `docs/business-rules.md`. Marcas `RN-xx` literales solo existen en algunos archivos; el resto se infiere por módulo (marcado con ~).

| RN | Tema | Archivos |
|---|---|---|
| 01 | Partida doble | `database/03_triggers.sql`+`07` (`fn_validar_partida_doble`), `DOM/Rules/PartidaDobleValidator.cs`, `AsientoFacturaBuilder.cs`, `AsientoTesoreriaBuilder.cs`, `RevaluacionRules.cs`, `AsientosController` |
| 02 | Periodo cerrado bloquea líneas | `database/03_triggers.sql` (`fn_bloquear_periodo_cerrado`), `AsientosController` |
| 03 | Reversa, sin borrado físico | `database/12_reversa_asientos.sql`, `16` (`sp_reversar_asiento`), `API/Services/ReversaAsiento.cs`, `AsientosController`, `03_triggers.sql` |
| 04 | Factura requiere contraparte | `API/Controllers/FacturaControllerBase.cs`, `API/Services/ValidacionContable.cs` |
| 05 | Pago no excede saldo | `database/16_multimoneda.sql` (`fn_limite_pago_*`; antes 03/13/15), `DOM/Rules/PagoRules.cs` |
| 06 | Moneda funcional GTQ | ~ `DOM/Rules/CambioRules.cs`, `MotorFiscal.cs`, `database/16` |
| 07 | IVA desde catálogo `impuesto` | ~ `DOM/Rules/MotorFiscal.cs`, `ImpuestoRules.cs`, `API/Services/FacturaFiscalService.cs`, `database/13` |
| 08 | Bitácora de auditoría | `API/Services/AuditoriaService.cs`, `database/04_procedures.sql` (`sp_registrar_auditoria`), `AsientosController` |
| 09 | Acceso por perfil | `API/Auth/Roles.cs`, atributos `[Authorize]` en controllers, `AsientosController` |
| 10 | Conciliación bancaria | `database/09_tesoreria.sql`, `ConciliacionesController` |
| 11 | Carga inicial | ~ `database/02_functions.sql` (sin controller) |
| 12, 13 | DTE (FEL) y NIT | ~ `DOM/Rules/DteRules.cs`, `NitRules.cs`, `ContraparteRules.cs`, `database/13` |
| 14 | Nota de crédito | ~ `database/16` (`fn_nota_credito_valida_*`; antes 13/15), `PagoRules.cs` |
| 15 | Fecha dentro de periodo abierto | ~ `API/Services/ValidacionContable.cs`, `database/03` |
| 16 | Cálculo legado | ~ `database/13_fiscal_iva.sql`, `MotorFiscal.cs`, `ConfiguracionFiscalRules.cs` |
| 17 a 20, 22, 23, 29 a 36 | Retenciones, constancias, reporte | ~ `DOM/Rules/RetencionRules.cs`, `RetencionNoResidenteRules.cs`, `ParametroRetencionRules.cs`, `API/Services/{Retencion,ConstanciaRetencion,ReporteRetencion}Service.cs`, `database/15`, `database/17`, `database/18` |
| 21 | IVA retenido por cliente en cobro | ~ `RetencionService.cs`, `CxCController` (`retencion-iva-sugerida`), `AsientoFacturaBuilder.cs` |
| 24, 25 | Documento multimoneda, tipo de cambio único | ~ `DOM/Rules/CambioRules.cs`, `API/Services/TipoCambioService.cs`, `database/16` |
| 26 | Diferencial cambiario realizado | `DOM/Rules/DiferencialCambiarioRules.cs`, `AsientoTesoreriaBuilder.cs`, `database/16` |
| 27 | Moneda de cuenta bancaria | `API/Services/TesoreriaService.cs`, `AsientoTesoreriaBuilder.cs`, `PagoMonedaRules.cs`, `database/16` |
| 28 | Revaluación fin de mes | ~ `DOM/Rules/RevaluacionRules.cs`, `API/Services/RevaluacionService.cs`, `database/16` |
| 29 | Catálogo art. 104 | `database/17`, `ParametroRetencionRules`, `ContraparteRules`, `RetencionService` |
| 30 | Retención NORES al pago | `database/17`, `RetencionNoResidenteRules`, `RetencionService`, `CxPController`, `AsientoTesoreriaBuilder` |
| 31 | TC del día del pago NORES | `database/17`, `RetencionService`, `TipoCambioService`, `RetencionNoResidenteRules` |
| 32 | Enteramiento ISR NR | `EnteramientoRules`, `EnteramientoService`, `database/18` |
| 33 | Plazo de enteramiento (décimo día hábil) | `DiaHabilRules`, `EnteramientoService.SaldoAsync`, `FeriadosController` |
| 34 | Ajuste de retención por NC | `AjusteRetencionService`, `FacturaService` |
| 35 | País de no residente | `ContraparteRules`, `ContrapartesController` |
| 36 | Feriados | ~ `FeriadosController`, `database/18` |
