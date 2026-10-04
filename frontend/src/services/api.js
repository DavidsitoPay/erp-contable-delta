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

// Solo existe POST; el backend aún no expone un GET para listar asientos.
export const asientosApi = {
  crear: (asiento) => api.post("/asientos", asiento),
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
};

export const cxpApi = {
  listarFacturas: () => api.get("/cxp/facturas"),
  obtenerFactura: (id) => api.get(`/cxp/facturas/${idRuta(id)}`),
  crearFactura: (factura) => api.post("/cxp/facturas", factura),
  listarPagos: () => api.get("/cxp/pagos"),
  crearPago: (pago) => api.post("/cxp/pagos", pago),
};

export default api;
