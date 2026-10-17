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
- SQL: tabla `contraparte` (01); columnas fiscales/retención en 13 y 15.
- Frontend: `FC/Contrapartes/` (`Contrapartes`, `CamposFiscalesContraparte`); `FU/nit.js`, `FU/formularioContraparte.js`. api.js: `contrapartesApi`.
- Tests: TI `ContrapartesControllerTests`, `ContrapartesFiscalTests`, `ContrapartesRetencionTests`; TU `ContraparteRulesTests`, `NitRulesTests`, `ContraparteYConfiguracionRetencionRulesTests`.
- RN: 04, 13, 19.

### Cuentas por cobrar (E4) y por pagar (E5)
- Controllers: `CxCController` (`api/cxc`: `facturas`, `facturas/{id}/retencion-iva-sugerida`, `pagos`), `CxPController` (`api/cxp`: `facturas`, `facturas/retencion-previa`, `facturas/{id}/revision-retencion`, `pagos`). Base compartida: `FacturaControllerBase`.
- Servicios: `FacturaService`, `FacturaFiscalService`, `PerfilFactura`, `SaldoAlCorteService`. Dominio: `FacturaRules`, `PagoRules`, `PagoMonedaRules`, `AsientoFacturaBuilder`, `SaldoAntiguedadRules`. Modelos: `API/Models/FacturaDetalle`, `LineaFacturaDetalle`, `PagoMonedaModels`.
- Entidades: DocumentoCxC/CxP (base `DocumentoFacturaBase`), LineaDocumentoCxC/CxP, ReciboPagoCliente, PagoProveedorCabecera, AplicacionPago* (base `AplicacionPagoBase`), SaldoDocumentoCxC/CxP.
- SQL: tablas `documentocxc|cxp`, `lineadocumentocxc|cxp`, `recibopagocliente`, `pagoproveedorcabecera`, `aplicacionpagocliente|proveedor` (01); `fn_limite_pago_cxc|cxp` (vigente 16; también 03/13/15), `fn_nota_credito_valida_cxc|cxp` (vigente 16 cxc, 16 cxp; también 13/15), `fn_linea_impuesto_valido` (13), `vw_saldodocumentocxc|cxp` (vigente 16).
- Frontend: `FC/CxC/CuentasPorCobrar`, `FC/CxP/CuentasPorPagar`, `FC/CuentasPorCobrarPagar/` (formulario, tablas, `PanelLiquidacion`, `PanelRetencionIsr`, `RetencionIvaCobro`, hooks `use*`); `FU/liquidacion.js`, `FU/fiscal.js`, `FU/cambio.js`. api.js: `cxcApi`, `cxpApi`.
- Tests: TI `CxCControllerTests`, `CxPControllerTests`, `FacturasCxCFiscalTests`, `FacturasCxPFiscalTests`, `FacturasCxPRetencionTests`, `FacturasMonedaExtranjeraTests`, `CobrosCxCRetencionTests`, `PagosMonedaExtranjeraTests`; TU `FacturaRulesTests`, `PagoRulesTests`, `PagoRulesNotaCreditoTests`, `PagoMonedaRulesTests`, `AsientoFacturaBuilderTests`, `SaldoAntiguedadRulesTests`.
- RN: 04, 05, 12, 14, 15, 16, 21, 24, 26.

### Tesorería y conciliación bancaria (E6)
- Controllers: `CuentasBancariasController` (`api/cuentas-bancarias`), `MovimientosTesoreriaController` (`api/movimientos-tesoreria`, `POST transferencias`), `ConciliacionesController` (`api/conciliaciones`: `{id}/movimientos`, `{id}/cancelar`, `{id}/finalizar`).
- Servicio: `TesoreriaService`. Dominio: `TesoreriaRules`, `AsientoTesoreriaBuilder`. Modelos: `TesoreriaModels`. Entidades: CuentaBancaria, MovimientoTesoreria, ConciliacionBancaria, DetalleConciliacion, ConciliacionResumen, SaldoCuentaBancaria.
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
- SQL: todo en `SQL/13_fiscal_iva.sql` (`impuesto`, `configuracionfiscal`, `fn_impuesto_sin_traslape`, `fn_impuesto_inmutable_si_usado`, `fn_configuracionfiscal_no_eliminable`, triggers de línea/nota de crédito).
- Frontend: `FC/Catalogo/` (`Impuestos`, `FormularioImpuesto`, `ConfiguracionFiscal`), `FC/CuentasPorCobrarPagar/DatosDte`, `FC/Libros/LibroFiscal`; `FU/etiquetasFiscales.js`, `FU/formularioImpuesto.js`, `FU/libroFiscal.js`. api.js: `configuracionFiscalApi`, `impuestosApi`, `librosFiscalesApi`.
- Tests: TI `ImpuestosControllerTests`, `ConfiguracionFiscalControllerTests`, `LibroFiscalControllerTests`, `FiscalEsquemaTests`; TU `MotorFiscalTests`, `ImpuestoRulesTests`, `ConfiguracionFiscalRulesTests`, `DteRulesTests`.
- RN: 06, 07, 12, 13, 14, 16.

### Fiscal: retenciones ISR/IVA (E10-F2)
- Controllers: `ParametrosRetencionController` (`api/retenciones/parametros`; escritura solo admin), `ConstanciasRetencionController` (`api/retenciones/{id}/constancia`, `constancias/{id}/anular`), `ReporteRetencionesController` (`api/retenciones/reporte`).
- Servicios: `RetencionService`, `ConstanciaRetencionService`, `ReporteRetencionService`. Dominio: `RetencionRules`, `ParametroRetencionRules`. Modelos: `RetencionModels`. Entidades: ParametroRetencion, Retencion, ConstanciaRetencion, EntradasRetencion, VwRetencionReporte.
- SQL: `SQL/15_retenciones.sql` (`parametroretencion`, `retencion`, `constanciaretencion`, `vw_retencion_reporte`, triggers sin traslape/inmutable).
- Frontend: `FC/Retenciones/` (parámetros, constancia, reporte, tablas); `FC/CuentasPorCobrarPagar/PanelRetencionIsr`, `RetencionIvaCobro`; `FU/retenciones.js`, `FU/parametrosRetencion.js`. api.js: `parametrosRetencionApi`, `retencionesApi`.
- Tests: TI `ParametrosRetencionControllerTests`, `ConstanciasRetencionTests`, `ReporteRetencionesTests`, `RetencionesEsquemaTests`; TU `RetencionRulesTests`, `ParametroRetencionRulesTests`, `RetencionAsientosTests`, `ErroresPostgresRetencionTests`.
- RN: 17 a 23.

### Multimoneda, tipo de cambio y revaluación (E10-F3)
- Controllers: `MonedasController` (`api/monedas`), `TiposCambioController` (`api/tipos-cambio`: `historial`, `manual`, `sustituir-por-banguat`, `{id}/baja`), `RevaluacionesController` (`api/revaluaciones`: `vista-previa`, `{id}/anular`).
- Servicios: `TipoCambioService`, `RevaluacionService`, `ConsultasRevaluacion`, `SaldoAlCorteService`; cliente Banguat SOAP: `IBanguatClient`, `BanguatSoapClient`, `BanguatXml`, `BanguatOptions`. Dominio: `CambioRules`, `DiferencialCambiarioRules`, `RevaluacionRules`, `PagoMonedaRules`. Modelos: `TipoCambioModels`, `RevaluacionModels`. Entidades: Moneda, HistorialTipoCambio, Revaluacion.
- SQL: `SQL/16_multimoneda.sql` (`revaluacion`, `revaluacionpartida`, `trg_00_multimoneda_completar`, `fn_historialtipocambio_*`, `fn_documento_moneda_*`; redefine funciones/vistas de CxC/CxP/tesorería); tablas base `moneda`, `historialtipocambio` en 01.
- Frontend: `FC/Revaluaciones/`, `FC/SelectMoneda`, `FC/Reportes/SaldosMoneda`, ruta `/catalogo/tipos-cambio`; `FU/cambio.js`, `FU/revaluacion.js`. api.js: `monedasApi`, `tiposCambioApi`, `revaluacionesApi`.
- Tests: TI `TiposCambioControllerTests`, `RevaluacionesControllerTests`, `MultimonedaEsquemaTests`; TU `CambioRulesTests`, `DiferencialCambiarioRulesTests`, `RevaluacionRulesTests`, `BanguatSoapClientTests`, `BanguatXmlTests`, `MotorFiscalMonedaTests`. Doble de prueba: `FakeBanguatClient`.
- RN: 06, 24 a 28.

### Seguridad, roles y auditoría (E8)
- Ver sección 2 (auth). Auditoría: `API/Services/AuditoriaService`, `API/Controllers/GuardadoAuditado`; SQL `sp_registrar_auditoria` (04), `bitacoraauditoria` (01), `fn_bitacora_inmutable` (03). Perfiles: `FU/perfiles.js`; `FC/Login.jsx`.
- Tests: TI `AuthControllerTests`, `PermisosApiTests`; front `FC/Login.test.jsx`, `FU/perfiles.test.js`.
- RN: 08, 09.

### Carga inicial (E9, RN-11)
- Sin controller dedicado. Datos: `SQL/06_seed_dev.sql`, `SQL/demo/*.sql`; funciones de soporte en `SQL/02_functions.sql` (contenido no verificado en detalle).

## 2. Transversal

| Tema | Dónde |
|---|---|
| JWT / auth | `API/Program.cs` (AddJwtBearer, CORS vía `Cors:AllowedOrigins`, registro de servicios), `API/Auth/{JwtOptions,TokenService,Roles,ControllerExtensions}.cs`, `AuthController` (`POST api/auth/login`), sección `Jwt` en `API/appsettings.json` |
| Roles | constantes en `API/Auth/Roles.cs`; se usan como `[Authorize(Roles = Roles.X)]` por acción; frontend filtra por perfil en `frontend/src/App.jsx` y `FU/perfiles.js` |
| DbContext | `INF/Data/DeltaErpDbContext.cs` (único mapeo EF); excluido de coverage en Sonar |
| Errores Postgres | `API/Services/ErroresPostgres.cs` (SQLSTATE -> HTTP, restricciones únicas -> mensaje, `TraducirProcedimientoAsync`), `ErrorValidacion.cs`, `ControllerExtensions.Fallo`; tests TU `ErroresPostgresTests` |
| Guardado con auditoría | `GuardadoAuditado.GuardarAuditadoAsync` (usado por controllers de catálogo) |
| Frontend base | `frontend/src/App.jsx` (rutas), `main.jsx`, `services/api.js` (un objeto `*Api` por recurso), `tema.js`, `styles/{theme,components}.css`, `test/setup.js`, `test/reportesFixtures.js`; componentes compartidos `FC/CampoFormulario`, `FC/OpcionesSelect`, `FC/FormularioMotivo` |
| Rutas frontend | `/asientos`, `/catalogo/{cuentas,centros-costo,periodos,tipos-cambio,configuracion-fiscal,impuestos,parametros-retencion}`, `/libros/{balance,diario,mayor,fiscal,retenciones}`, `/contrapartes`, `/cxc`, `/cxp`, `/tesoreria`, `/revaluaciones`, `/reportes/{balance-general,estado-resultados,saldos-moneda}` |
| Migraciones | `SQL/NN_descripcion.sql` en orden numérico (01..16); `SQL/run_migrations.sh` aplica los pendientes, uno por transacción, registro en `schema_migrations`. Nunca se edita un script aplicado: corrección = archivo NN+1. Sin migraciones down. Los scripts 07/08/12/16 redefinen objetos de 03/04/05: la versión vigente es la del número mayor. |
| Permisos de BD | `SQL/14_rol_api.sql` (rol `delta_api`): toda tabla/vista/rutina nueva usada por la API declara su GRANT ahí |
| Datos demo | `SQL/06_seed_dev.sql`, `SQL/demo/` (no son migraciones) |
| Fixtures de pruebas | `backend/tests/DeltaERP.Tests/Support/`: `PostgresFixture` (Postgres de CI, aplica migraciones), `ApiFactory`, `IntegrationCollection`, `TestAuthHandler`, `TestData`, `FakeBanguatClient`, `FacturaPayloads`, `RetencionPayloads`, `JsonExtensions` |
| Docs vivas | `docs/{architecture,business-rules,data-dictionary,backlog-features-historias,plan-de-pruebas-inicial,azure-devops-checklist}.md`; `docs/entregado/` no se toca |

### CI y calidad

| Archivo | Qué hace |
|---|---|
| `.github/workflows/backend-build-check.yml` | push a `dev`: `dotnet test` (Postgres como service container) + `npm run test:ci` + gate `diff-cover` (80% líneas cambiadas vs main) + gate `jscpd` (min-tokens 80, falla con duplicados) |
| `.github/workflows/sonarcloud-analysis.yml` | PR y push a `main`: tests con cobertura + dotnet-sonarscanner + análisis SonarCloud; exclusiones de coverage (`Program.cs`, `DeltaErpDbContext.cs`, `Entities/**`, `main.jsx`) y de CPD (`backend/tests/**`, `*.test.*`, `frontend/src/test/**`) en el propio workflow |
| `.github/workflows/backend-deploy.yml` | push a `main`/manual: aplica migraciones pendientes, construye y publica imagen (`devops/Dockerfile.backend`) en GHCR, actualiza Azure Container App |
| `.github/workflows/frontend-alias.yml` | push a `main`/manual: espera el deploy de Vercel y re-apunta el alias `delta-erp-contable.vercel.app` |
| `.github/requirements-ci.txt` | dependencias Python de CI (diff-cover) |
| `frontend/package.json`, `frontend/vite.config.js` | script `test:ci` (vitest + coverage v8, salida cobertura/lcov/junit); `frontend/vercel.json` despliegue |
| `devops/recreate-containerapp.sh` | recrea el Container App de Azure |

## 3. Índice RN-xx -> dónde se aplica

Fuente de verdad del texto: `docs/business-rules.md`. Marcas `RN-xx` literales solo existen en algunos archivos; el resto se infiere por módulo (marcado con ~).

| RN | Tema | Archivos |
|---|---|---|
| 01 | Partida doble | `SQL/03_triggers.sql`+`07` (`fn_validar_partida_doble`), `DOM/Rules/PartidaDobleValidator.cs`, `AsientoFacturaBuilder.cs`, `AsientoTesoreriaBuilder.cs`, `RevaluacionRules.cs`, `AsientosController` |
| 02 | Periodo cerrado bloquea líneas | `SQL/03_triggers.sql` (`fn_bloquear_periodo_cerrado`), `AsientosController` |
| 03 | Reversa, sin borrado físico | `SQL/12_reversa_asientos.sql`, `16` (`sp_reversar_asiento`), `API/Services/ReversaAsiento.cs`, `AsientosController`, `03_triggers.sql` |
| 04 | Factura requiere contraparte | `API/Controllers/FacturaControllerBase.cs`, `API/Services/ValidacionContable.cs` |
| 05 | Pago no excede saldo | `SQL/16_multimoneda.sql` (`fn_limite_pago_*`; antes 03/13/15), `DOM/Rules/PagoRules.cs` |
| 06 | Moneda funcional GTQ | ~ `DOM/Rules/CambioRules.cs`, `MotorFiscal.cs`, `SQL/16` |
| 07 | IVA desde catálogo `impuesto` | ~ `DOM/Rules/MotorFiscal.cs`, `ImpuestoRules.cs`, `API/Services/FacturaFiscalService.cs`, `SQL/13` |
| 08 | Bitácora de auditoría | `API/Services/AuditoriaService.cs`, `SQL/04_procedures.sql` (`sp_registrar_auditoria`), `AsientosController` |
| 09 | Acceso por perfil | `API/Auth/Roles.cs`, atributos `[Authorize]` en controllers, `AsientosController` |
| 10 | Conciliación bancaria | `SQL/09_tesoreria.sql`, `ConciliacionesController` |
| 11 | Carga inicial | ~ `SQL/02_functions.sql` (sin controller) |
| 12, 13 | DTE (FEL) y NIT | ~ `DOM/Rules/DteRules.cs`, `NitRules.cs`, `ContraparteRules.cs`, `SQL/13` |
| 14 | Nota de crédito | ~ `SQL/16` (`fn_nota_credito_valida_*`; antes 13/15), `PagoRules.cs` |
| 15 | Fecha dentro de periodo abierto | ~ `API/Services/ValidacionContable.cs`, `SQL/03` |
| 16 | Cálculo legado | ~ `SQL/13_fiscal_iva.sql`, `MotorFiscal.cs`, `ConfiguracionFiscalRules.cs` |
| 17 a 20, 22, 23 | Retenciones, constancias, reporte | ~ `DOM/Rules/RetencionRules.cs`, `ParametroRetencionRules.cs`, `API/Services/{Retencion,ConstanciaRetencion,ReporteRetencion}Service.cs`, `SQL/15` |
| 21 | IVA retenido por cliente en cobro | ~ `RetencionService.cs`, `CxCController` (`retencion-iva-sugerida`), `AsientoFacturaBuilder.cs` |
| 24, 25 | Documento multimoneda, tipo de cambio único | ~ `DOM/Rules/CambioRules.cs`, `API/Services/TipoCambioService.cs`, `SQL/16` |
| 26 | Diferencial cambiario realizado | `DOM/Rules/DiferencialCambiarioRules.cs`, `AsientoTesoreriaBuilder.cs`, `SQL/16` |
| 27 | Moneda de cuenta bancaria | `API/Services/TesoreriaService.cs`, `AsientoTesoreriaBuilder.cs`, `PagoMonedaRules.cs`, `SQL/16` |
| 28 | Revaluación fin de mes | ~ `DOM/Rules/RevaluacionRules.cs`, `API/Services/RevaluacionService.cs`, `SQL/16` |
