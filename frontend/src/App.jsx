import { useEffect, useState } from "react";
import { NavLink, Route, Routes } from "react-router-dom";
import { checkHealth } from "./services/api";
import Login from "./components/Login";
import LogoMark from "./components/LogoMark";
import CuentasContables from "./components/Catalogo/CuentasContables";
import CentrosCosto from "./components/Catalogo/CentrosCosto";
import PeriodosContables from "./components/Catalogo/PeriodosContables";
import RegistrarAsiento from "./components/Asientos/RegistrarAsiento";

function App() {
  const [status, setStatus] = useState("Verificando conexión con la API...");
  const [statusVariant, setStatusVariant] = useState("pending");
  const [usuario, setUsuario] = useState(() => {
    const guardado = localStorage.getItem("delta_usuario");
    return guardado ? JSON.parse(guardado) : null;
  });

  useEffect(() => {
    checkHealth()
      .then(() => {
        setStatus("Conectado a Delta ERP Contable API");
        setStatusVariant("ok");
      })
      .catch(() => {
        setStatus("No se pudo conectar con la API (¿está corriendo el backend?)");
        setStatusVariant("error");
      });
  }, []);

  function handleLogout() {
    localStorage.removeItem("delta_token");
    localStorage.removeItem("delta_usuario");
    setUsuario(null);
  }

  if (!usuario) {
    return <Login onLoginExitoso={setUsuario} />;
  }

  // Solo Administrador del sistema y Contador pueden registrar asientos
  // (el backend ya lo exige en POST /api/asientos); se oculta la entrada de
  // navegación y se bloquea la ruta para no dejar que un Vendedor/Técnico
  // llene todo el formulario para toparse con un 403 al final.
  const puedeRegistrarAsientos =
    usuario.perfil === "Administrador del sistema" || usuario.perfil === "Contador";

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <div className="logo-badge">
            <LogoMark size={36} />
          </div>
          <span className="wordmark">Delta</span>
        </div>

        <div className="topbar-right">
          <span className="status-pill" data-status={statusVariant}>{status}</span>
          <div className="user-chip">
            <span>
              <strong>{usuario.nombre}</strong> <span className="user-role">({usuario.perfil})</span>
            </span>
            <button className="btn btn-outline btn-sm" onClick={handleLogout}>Cerrar sesión</button>
          </div>
        </div>
      </header>

      <nav className="tab-nav">
        {puedeRegistrarAsientos && (
          <NavLink className={({ isActive }) => "tab-link" + (isActive ? " active" : "")} to="/asientos">
            Registrar asiento
          </NavLink>
        )}
        <NavLink className={({ isActive }) => "tab-link" + (isActive ? " active" : "")} to="/catalogo/cuentas">
          Cuentas contables
        </NavLink>
        <NavLink className={({ isActive }) => "tab-link" + (isActive ? " active" : "")} to="/catalogo/centros-costo">
          Centros de costo
        </NavLink>
        <NavLink className={({ isActive }) => "tab-link" + (isActive ? " active" : "")} to="/catalogo/periodos">
          Periodos
        </NavLink>
      </nav>

      <main className="content">
        <Routes>
          <Route path="/" element={<p>Selecciona una sección del catálogo arriba.</p>} />
          <Route
            path="/asientos"
            element={
              puedeRegistrarAsientos ? (
                <RegistrarAsiento />
              ) : (
                <p>No autorizado: tu perfil ({usuario.perfil}) no puede registrar asientos contables.</p>
              )
            }
          />
          <Route path="/catalogo/cuentas" element={<CuentasContables />} />
          <Route path="/catalogo/centros-costo" element={<CentrosCosto />} />
          <Route path="/catalogo/periodos" element={<PeriodosContables />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
