import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "http://localhost:5000/api",
});

// TODO Etapa 3/4: interceptor para adjuntar el token JWT (M8 Seguridad)
// api.interceptors.request.use((config) => { ... });

export function checkHealth() {
  return axios.get((import.meta.env.VITE_API_URL || "http://localhost:5000/api").replace("/api", "/health"));
}

export default api;
