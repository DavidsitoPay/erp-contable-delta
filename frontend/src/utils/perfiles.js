const ADMINISTRADOR = "Administrador del sistema";
const PERFILES_TESORERIA = new Set([ADMINISTRADOR, "Contador"]);

export function puedeGestionarTesoreria(perfil) {
  return PERFILES_TESORERIA.has(perfil);
}

export function puedeGestionarFiscal(perfil) {
  return PERFILES_TESORERIA.has(perfil);
}

export function puedeAdministrarConfiguracionFiscal(perfil) {
  return perfil === ADMINISTRADOR;
}

export function puedeReversarAsientos(perfil) {
  return perfil === ADMINISTRADOR;
}

export function perfilActual() {
  try {
    return JSON.parse(localStorage.getItem("delta_usuario"))?.perfil ?? null;
  } catch {
    return null;
  }
}
