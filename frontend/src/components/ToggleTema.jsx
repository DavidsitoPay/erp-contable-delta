import { useState, useEffect } from "react";
import { alternarTema, obtenerTemaActual } from "../tema";

/**
 * Botón para alternar entre modo claro y oscuro.
 *
 * - Lee el tema actual desde el DOM (data-theme en <html>)
 * - Al hacer clic, alterna y persiste la elección
 * - Actualiza el aria-pressed y aria-label para a11y
 * - Escucha el evento "delta:tema-cambiado" para sincronizar si el tema cambia
 *   por otros listeners (cambio de SO, sincronización entre pestañas)
 * - Icono SVG inline: sol (claro) / luna (oscuro), sin gradientes ni efectos
 */
function ToggleTema() {
  const [temaOscuro, setTemaOscuro] = useState(() => obtenerTemaActual() === "oscuro");

  // Escucha cambios de tema disparados por otros listeners (SO, almacenamiento compartido)
  useEffect(() => {
    function handleTemaChanged(e) {
      setTemaOscuro(e.detail === "oscuro");
    }

    window.addEventListener("delta:tema-cambiado", handleTemaChanged);
    return () => {
      window.removeEventListener("delta:tema-cambiado", handleTemaChanged);
    };
  }, []);

  function handleClick() {
    const nuevoTema = alternarTema();
    setTemaOscuro(nuevoTema === "oscuro");
  }

  const ariaLabel = temaOscuro
    ? "Cambiar a modo claro"
    : "Cambiar a modo oscuro";
  const title = ariaLabel;

  return (
    <button
      type="button"
      className="btn btn-outline btn-sm toggle-tema"
      onClick={handleClick}
      aria-pressed={temaOscuro}
      aria-label={ariaLabel}
      title={title}
    >
      {/* Icono: sol (claro) o luna (oscuro) */}
      {!temaOscuro ? (
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          {/* Sol: círculo + rayos */}
          <circle cx="12" cy="12" r="5" />
          <line x1="12" y1="1" x2="12" y2="3" />
          <line x1="12" y1="21" x2="12" y2="23" />
          <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
          <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
          <line x1="1" y1="12" x2="3" y2="12" />
          <line x1="21" y1="12" x2="23" y2="12" />
          <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
          <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
        </svg>
      ) : (
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          {/* Luna: media luna estilizada */}
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
        </svg>
      )}
    </button>
  );
}

export default ToggleTema;
