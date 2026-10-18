import { useEffect, useRef, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";

export default function MenuPrincipal({ grupos }) {
  const [abierto, setAbierto] = useState(null);
  const { pathname } = useLocation();
  const ref = useRef(null);

  useEffect(() => setAbierto(null), [pathname]);

  useEffect(() => {
    function handleMouseDown(e) {
      if (ref.current && !ref.current.contains(e.target)) {
        setAbierto(null);
      }
    }
    document.addEventListener("mousedown", handleMouseDown);
    return () => document.removeEventListener("mousedown", handleMouseDown);
  }, []);

  function handleKeyDown(e) {
    if (e.key === "Escape" && abierto) {
      const boton = ref.current.querySelector('[aria-expanded="true"]');
      setAbierto(null);
      boton?.focus();
    }
  }

  return (
    <nav className="menu-nav" aria-label="Principal" ref={ref} onKeyDown={handleKeyDown}>
      {grupos.map((g) => {
        const activo = g.items.some((i) => pathname === i.to || pathname.startsWith(i.to + "/"));
        return (
          <div className="menu-grupo" key={g.id}>
            <button
              type="button"
              className={"menu-boton" + (activo ? " active" : "")}
              aria-expanded={abierto === g.id}
              aria-controls={abierto === g.id ? "menu-" + g.id : undefined}
              aria-current={activo ? "true" : undefined}
              onClick={() => setAbierto(abierto === g.id ? null : g.id)}
            >
              {g.titulo}
            </button>
            {abierto === g.id && (
              <ul id={"menu-" + g.id} className="menu-panel">
                {g.items.map((i) => (
                  <li key={i.to}>
                    <NavLink to={i.to} className={({ isActive }) => "menu-item" + (isActive ? " active" : "")}>
                      {i.texto}
                    </NavLink>
                  </li>
                ))}
              </ul>
            )}
          </div>
        );
      })}
    </nav>
  );
}
