/**
 * NOTA: La lógica de resolución de tema está duplicada a propósito en el
 * script inline de index.html (no puede importar módulos ES). Ambos usan la
 * clave "delta_tema" y los valores "claro"/"oscuro" — si cambias algo aquí,
 * actualiza también index.html.
 */

const CLAVE_TEMA = "delta_tema";
const TEMA_CLARO = "claro";
const TEMA_OSCURO = "oscuro";

// localStorage puede lanzar en modo privado; de ahí los try/catch de abajo.
export function resolverTemaInicial() {
  try {
    const guardado = localStorage.getItem(CLAVE_TEMA);
    if (guardado === TEMA_CLARO || guardado === TEMA_OSCURO) {
      return guardado;
    }
  } catch (e) {
  }

  try {
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? TEMA_OSCURO
      : TEMA_CLARO;
  } catch (e) {
    return TEMA_CLARO;
  }
}

export function aplicarTema(tema) {
  document.documentElement.dataset.theme = tema;
  window.dispatchEvent(new CustomEvent("delta:tema-cambiado", { detail: tema }));
}

export function guardarTema(tema) {
  try {
    localStorage.setItem(CLAVE_TEMA, tema);
  } catch (e) {
  }
}

export function alternarTema() {
  const actual = document.documentElement.dataset.theme || TEMA_CLARO;
  const siguiente = actual === TEMA_CLARO ? TEMA_OSCURO : TEMA_CLARO;
  aplicarTema(siguiente);
  guardarTema(siguiente);
  return siguiente;
}

export function obtenerTemaActual() {
  return document.documentElement.dataset.theme || TEMA_CLARO;
}

// Debe llamarse una sola vez desde main.jsx, antes de createRoot.
export function iniciarSincronizacionTema() {
  window.addEventListener("storage", (e) => {
    if (e.key === CLAVE_TEMA && (e.newValue === TEMA_CLARO || e.newValue === TEMA_OSCURO)) {
      aplicarTema(e.newValue);
    }
  });

  // Solo sincroniza con el SO si el usuario no tiene una preferencia guardada.
  try {
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQuery.addEventListener("change", (e) => {
      try {
        const guardado = localStorage.getItem(CLAVE_TEMA);
        if (!guardado || (guardado !== TEMA_CLARO && guardado !== TEMA_OSCURO)) {
          const nuevoTema = e.matches ? TEMA_OSCURO : TEMA_CLARO;
          aplicarTema(nuevoTema);
        }
      } catch (err) {
      }
    });
  } catch (err) {
  }
}
