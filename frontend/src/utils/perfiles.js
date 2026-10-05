const PERFILES_TESORERIA = ["Administrador del sistema", "Contador"];

export function puedeGestionarTesoreria(perfil) {
  return PERFILES_TESORERIA.includes(perfil);
}

export function perfilActual() {
  try {
    return JSON.parse(localStorage.getItem("delta_usuario"))?.perfil ?? null;
  } catch {
    return null;
  }
}
