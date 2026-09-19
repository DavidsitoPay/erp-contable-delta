/**
 * Delta ERP Contable — gestión de tema (claro/oscuro)
 *
 * Lógica centralizada para:
 * 1. Leer tema actual desde localStorage o preferencia del SO
 * 2. Aplicarlo a document.documentElement.dataset.theme
 * 3. Persistir la elección del usuario
 * 4. Sincronizar entre pestañas y con el SO
 *
 * NOTA DE SINCRONIZACIÓN: La lógica de resolución del tema está escrita dos veces:
 * aquí en src/tema.js y en el script inline de index.html.
 * AMBOS usan la clave "delta_tema" y los valores "claro"/"oscuro".
 * Si cambias esta lógica, mantén ambos archivos sincronizados.
 * La duplicación es intencional: el script inline no puede importar módulos ES.
 */

const CLAVE_TEMA = "delta_tema";
const TEMA_CLARO = "claro";
const TEMA_OSCURO = "oscuro";

/**
 * Resuelve el tema que debe usarse:
 * - Lee localStorage.getItem(CLAVE_TEMA)
 * - Si no hay valor guardado, usa la preferencia del SO (prefers-color-scheme: dark)
 * - Fallback: "claro" (por si falla matchMedia)
 *
 * Nota: usa try/catch porque localStorage puede lanzar en modo privado.
 */
export function resolverTemaInicial() {
  try {
    const guardado = localStorage.getItem(CLAVE_TEMA);
    if (guardado === TEMA_CLARO || guardado === TEMA_OSCURO) {
      return guardado;
    }
  } catch (e) {
    // localStorage no disponible (modo privado, etc.)
  }

  // Usa preferencia del SO
  try {
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? TEMA_OSCURO
      : TEMA_CLARO;
  } catch (e) {
    // matchMedia no disponible: fallback a claro
    return TEMA_CLARO;
  }
}

/**
 * Aplica el tema al elemento <html> y despacha un evento personalizado
 */
export function aplicarTema(tema) {
  document.documentElement.dataset.theme = tema;
  window.dispatchEvent(new CustomEvent("delta:tema-cambiado", { detail: tema }));
}

/**
 * Persiste el tema en localStorage
 */
export function guardarTema(tema) {
  try {
    localStorage.setItem(CLAVE_TEMA, tema);
  } catch (e) {
    // localStorage no disponible (modo privado, etc.): no hagas nada
  }
}

/**
 * Alterna entre claro y oscuro
 */
export function alternarTema() {
  const actual = document.documentElement.dataset.theme || TEMA_CLARO;
  const siguiente = actual === TEMA_CLARO ? TEMA_OSCURO : TEMA_CLARO;
  aplicarTema(siguiente);
  guardarTema(siguiente);
  return siguiente;
}

/**
 * Obtiene el tema actual
 */
export function obtenerTemaActual() {
  return document.documentElement.dataset.theme || TEMA_CLARO;
}

/**
 * Sincroniza el tema entre pestañas y con la preferencia del SO.
 * Registra dos listeners:
 * 1. "storage": si otra pestaña cambió el tema en localStorage, lo aplica
 * 2. "change" en matchMedia: si el SO cambió su preferencia (dark/light),
 *    lo aplica SOLO si el usuario no tiene una elección guardada
 *
 * Debe llamarse una sola vez desde main.jsx, antes de createRoot.
 */
export function iniciarSincronizacionTema() {
  // Listener de almacenamiento compartido (sincronización entre pestañas)
  window.addEventListener("storage", (e) => {
    if (e.key === CLAVE_TEMA && (e.newValue === TEMA_CLARO || e.newValue === TEMA_OSCURO)) {
      aplicarTema(e.newValue);
    }
  });

  // Listener de cambio del SO, solo si el usuario no ha elegido explícitamente
  try {
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQuery.addEventListener("change", (e) => {
      try {
        const guardado = localStorage.getItem(CLAVE_TEMA);
        // Solo cambia si el usuario no tiene una preferencia guardada
        if (!guardado || (guardado !== TEMA_CLARO && guardado !== TEMA_OSCURO)) {
          const nuevoTema = e.matches ? TEMA_OSCURO : TEMA_CLARO;
          aplicarTema(nuevoTema);
        }
      } catch (err) {
        // localStorage no disponible (modo privado)
      }
    });
  } catch (err) {
    // matchMedia no disponible
  }
}
