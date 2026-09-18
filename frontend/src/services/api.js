import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "http://localhost:5000/api",
});

// M8 Seguridad: adjunta el JWT emitido por /auth/login a cada llamada.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem("delta_token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export function checkHealth() {
  return axios.get((import.meta.env.VITE_API_URL || "http://localhost:5000/api").replace("/api", "/health"));
}

export function login(email, password) {
  return api.post("/auth/login", { email, password });
}

// M1 Catálogo: cuentas contables.
export const cuentasApi = {
  listar: (incluirInactivas = false) => api.get("/cuentas", { params: { incluirInactivas } }),
  crear: (cuenta) => api.post("/cuentas", cuenta),
  actualizar: (id, cambios) => api.put(`/cuentas/${id}`, cambios),
  desactivar: (id) => api.delete(`/cuentas/${id}`),
};

// M1 Catálogo: centros de costo.
export const centrosCostoApi = {
  listar: (incluirInactivos = false) => api.get("/centros-costo", { params: { incluirInactivos } }),
  crear: (centro) => api.post("/centros-costo", centro),
  actualizar: (id, cambios) => api.put(`/centros-costo/${id}`, cambios),
  desactivar: (id) => api.delete(`/centros-costo/${id}`),
};

// M1 Catálogo: periodos contables.
export const periodosApi = {
  listar: () => api.get("/periodos"),
  crear: (periodo) => api.post("/periodos", periodo),
  actualizar: (id, cambios) => api.put(`/periodos/${id}`, cambios),
  cerrar: (id) => api.post(`/periodos/${id}/cerrar`),
  reabrir: (id) => api.post(`/periodos/${id}/reabrir`),
};

// M2: asientos contables. Solo existe POST en el backend (sin GET/listar todavía).
export const asientosApi = {
  crear: (asiento) => api.post("/asientos", asiento),
};

export default api;
