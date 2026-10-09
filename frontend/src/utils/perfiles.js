const PERFILES_TESORERIA = new Set(["Administrador del sistema", "Contador"]);

export function puedeGestionarTesoreria(perfil) {
  return PERFILES_TESORERIA.has(perfil);
}

export function puedeReversarAsientos(perfil) {
  return perfil === "Administrador del sistema";
}

export function perfilActual() {
  try {
    return JSON.parse(localStorage.getItem("delta_usuario"))?.perfil ?? null;
  } catch {
    return null;
  }
}
