import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "http://localhost:5000/api",
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("delta_token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

const idRuta = (id) => encodeURIComponent(Number(id));

export function checkHealth() {
  return axios.get((import.meta.env.VITE_API_URL || "http://localhost:5000/api").replace("/api", "/health"));
}

export function login(email, password) {
  return api.post("/auth/login", { email, password });
}

export const cuentasApi = {
  listar: (incluirInactivas = false) => api.get("/cuentas", { params: { incluirInactivas } }),
  crear: (cuenta) => api.post("/cuentas", cuenta),
  actualizar: (id, cambios) => api.put(`/cuentas/${idRuta(id)}`, cambios),
  desactivar: (id) => api.delete(`/cuentas/${idRuta(id)}`),
};

export const centrosCostoApi = {
  listar: (incluirInactivos = false) => api.get("/centros-costo", { params: { incluirInactivos } }),
  crear: (centro) => api.post("/centros-costo", centro),
  actualizar: (id, cambios) => api.put(`/centros-costo/${idRuta(id)}`, cambios),
  desactivar: (id) => api.delete(`/centros-costo/${idRuta(id)}`),
};

export const periodosApi = {
  listar: () => api.get("/periodos"),
  crear: (periodo) => api.post("/periodos", periodo),
  actualizar: (id, cambios) => api.put(`/periodos/${idRuta(id)}`, cambios),
  cerrar: (id) => api.post(`/periodos/${idRuta(id)}/cerrar`),
  reabrir: (id) => api.post(`/periodos/${idRuta(id)}/reabrir`),
};

export const asientosApi = {
  crear: (asiento) => api.post("/asientos", asiento),
  reversar: (id, motivo) => api.post(`/asientos/${idRuta(id)}/reversar`, { motivo }),
};

export const librosApi = {
  balanceSaldos: () => api.get("/libros/balance-saldos"),
  diario: (periodoId) => api.get("/libros/diario", { params: { periodoId } }),
  mayor: (cuentaId, periodoId) =>
    api.get("/libros/mayor", { params: { cuentaId, periodoId: periodoId || undefined } }),
};

export const contrapartesApi = {
  listar: (tipo) => api.get("/contrapartes", { params: tipo ? { tipo } : {} }),
  crear: (contraparte) => api.post("/contrapartes", contraparte),
  actualizar: (id, cambios) => api.put(`/contrapartes/${idRuta(id)}`, cambios),
};

export const cxcApi = {
  listarFacturas: () => api.get("/cxc/facturas"),
  obtenerFactura: (id) => api.get(`/cxc/facturas/${idRuta(id)}`),
  crearFactura: (factura) => api.post("/cxc/facturas", factura),
  listarPagos: () => api.get("/cxc/pagos"),
  crearPago: (pago) => api.post("/cxc/pagos", pago),
  retencionIvaSugerida: (id) => api.get(`/cxc/facturas/${idRuta(id)}/retencion-iva-sugerida`),
};

export const cxpApi = {
  listarFacturas: () => api.get("/cxp/facturas"),
  obtenerFactura: (id) => api.get(`/cxp/facturas/${idRuta(id)}`),
  crearFactura: (factura) => api.post("/cxp/facturas", factura),
  listarPagos: () => api.get("/cxp/pagos"),
  crearPago: (pago) => api.post("/cxp/pagos", pago),
  retencionPrevia: (cuerpo) => api.post("/cxp/facturas/retencion-previa", cuerpo),
  retencionPreviaPago: (cuerpo) => api.post("/cxp/pagos/retencion-previa", cuerpo),
  resolverRevisionRetencion: (id, motivo) => api.post(`/cxp/facturas/${idRuta(id)}/revision-retencion`, { motivo }),
};

export const cuentasBancariasApi = {
  listar: (incluirInactivas = false) => api.get("/cuentas-bancarias", { params: { incluirInactivas } }),
  obtener: (id) => api.get(`/cuentas-bancarias/${idRuta(id)}`),
  crear: (cuenta) => api.post("/cuentas-bancarias", cuenta),
  actualizar: (id, cambios) => api.put(`/cuentas-bancarias/${idRuta(id)}`, cambios),
  desactivar: (id) => api.delete(`/cuentas-bancarias/${idRuta(id)}`),
};

export const movimientosTesoreriaApi = {
  listar: (filtros = {}) => api.get("/movimientos-tesoreria", { params: filtros }),
  crear: (movimiento) => api.post("/movimientos-tesoreria", movimiento),
  transferir: (transferencia) => api.post("/movimientos-tesoreria/transferencias", transferencia),
};

export const conciliacionesApi = {
  listar: (filtros = {}) => api.get("/conciliaciones", { params: filtros }),
  obtener: (id) => api.get(`/conciliaciones/${idRuta(id)}`),
  crear: (conciliacion) => api.post("/conciliaciones", conciliacion),
  actualizar: (id, cambios) => api.put(`/conciliaciones/${idRuta(id)}`, cambios),
  cancelar: (id) => api.post(`/conciliaciones/${idRuta(id)}/cancelar`),
  marcar: (conciliacionId, movimientoId) => api.post(`/conciliaciones/${idRuta(conciliacionId)}/movimientos`, { movimientoId: Number(movimientoId) }),
  desmarcar: (conciliacionId, movimientoId) => api.delete(`/conciliaciones/${idRuta(conciliacionId)}/movimientos/${idRuta(movimientoId)}`),
  finalizar: (id) => api.post(`/conciliaciones/${idRuta(id)}/finalizar`),
};

export const reportesApi = {
  balanceGeneral: (periodoId) => api.get("/reportes/balance-general", { params: { periodoId } }),
  estadoResultados: (periodoId) => api.get("/reportes/estado-resultados", { params: { periodoId } }),
};

export const monedasApi = {
  listar: () => api.get("/monedas"),
};

export const tiposCambioApi = {
  consultar: (moneda, fecha) => api.get("/tipos-cambio", { params: { moneda, fecha } }),
  historial: (filtros = {}) => api.get("/tipos-cambio/historial", { params: filtros }),
  capturarManual: (cuerpo) => api.post("/tipos-cambio/manual", cuerpo),
  sustituirPorBanguat: (cuerpo) => api.post("/tipos-cambio/sustituir-por-banguat", cuerpo),
  darDeBaja: (id, motivo) => api.post(`/tipos-cambio/${idRuta(id)}/baja`, { motivo }),
};

export const revaluacionesApi = {
  vistaPrevia: (cuerpo) => api.post("/revaluaciones/vista-previa", cuerpo),
  ejecutar: (cuerpo) => api.post("/revaluaciones", cuerpo),
  listar: (filtros = {}) => api.get("/revaluaciones", { params: filtros }),
  obtener: (id) => api.get(`/revaluaciones/${idRuta(id)}`),
  anular: (id, motivo) => api.post(`/revaluaciones/${idRuta(id)}/anular`, { motivo }),
};

export const reportesSaldosApi = {
  saldosAntiguedad: (filtros = {}) => api.get("/reportes/saldos-antiguedad", { params: filtros }),
};

export const configuracionFiscalApi = {
  obtener: () => api.get("/configuracion-fiscal"),
  actualizar: (configuracion) => api.put("/configuracion-fiscal", configuracion),
};

export const impuestosApi = {
  listar: (filtros = {}) => api.get("/impuestos", { params: filtros }),
  obtener: (id) => api.get(`/impuestos/${idRuta(id)}`),
  crear: (impuesto) => api.post("/impuestos", impuesto),
  actualizar: (id, cambios) => api.put(`/impuestos/${idRuta(id)}`, cambios),
  desactivar: (id) => api.delete(`/impuestos/${idRuta(id)}`),
};

const consultaLibroFiscal = (tipo) => (anio, mes) => api.get(`/libros/fiscal/${tipo}`, { params: { anio, mes } });

export const librosFiscalesApi = {
  ventas: consultaLibroFiscal("ventas"),
  compras: consultaLibroFiscal("compras"),
};

export const parametrosRetencionApi = {
  listar: (filtros = {}) => api.get("/retenciones/parametros", { params: filtros }),
  crear: (parametro) => api.post("/retenciones/parametros", parametro),
  actualizar: (id, cambios) => api.put(`/retenciones/parametros/${idRuta(id)}`, cambios),
};

export const retencionesApi = {
  reporte: (filtros = {}) => api.get("/retenciones/reporte", { params: filtros }),
  registrarConstancia: (retencionId, constancia) => api.post(`/retenciones/${idRuta(retencionId)}/constancia`, constancia),
  anularConstancia: (id, motivo) => api.post(`/retenciones/constancias/${idRuta(id)}/anular`, { motivo }),
};

export default api;
