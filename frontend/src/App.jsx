import { useEffect, useState } from "react";
import { NavLink, Route, Routes } from "react-router-dom";
import { checkHealth } from "./services/api";
import Login from "./components/Login";
import LogoMark from "./components/LogoMark";
import ToggleTema from "./components/ToggleTema";
import CuentasContables from "./components/Catalogo/CuentasContables";
import CentrosCosto from "./components/Catalogo/CentrosCosto";
import PeriodosContables from "./components/Catalogo/PeriodosContables";
import RegistrarAsiento from "./components/Asientos/RegistrarAsiento";
import BalanceSaldos from "./components/Libros/BalanceSaldos";
import LibroDiario from "./components/Libros/LibroDiario";
import LibroMayor from "./components/Libros/LibroMayor";
import Contrapartes from "./components/Contrapartes/Contrapartes";
import CuentasPorCobrar from "./components/CxC/CuentasPorCobrar";
import CuentasPorPagar from "./components/CxP/CuentasPorPagar";
import Tesoreria from "./components/Tesoreria/Tesoreria";
import BalanceGeneral from "./components/Reportes/BalanceGeneral";
import EstadoResultados from "./components/Reportes/EstadoResultados";
import { puedeGestionarTesoreria } from "./utils/perfiles";

function TabLink({ to, children }) {
  return (
    <NavLink className={({ isActive }) => "tab-link" + (isActive ? " active" : "")} to={to}>
      {children}
    </NavLink>
  );
}

function Restringida({ permitido, perfil, seccion, children }) {
  return permitido ? children : <p>No autorizado: tu perfil ({perfil}) no puede acceder a {seccion}.</p>;
}

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

  const esAdminOContador = puedeGestionarTesoreria(usuario.perfil);

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
          <ToggleTema />
          <div className="user-chip">
            <span>
              <strong>{usuario.nombre}</strong> <span className="user-role">({usuario.perfil})</span>
            </span>
            <button className="btn btn-outline btn-sm" onClick={handleLogout}>Cerrar sesión</button>
          </div>
        </div>
      </header>

      <nav className="tab-nav">
        {esAdminOContador && <TabLink to="/asientos">Registrar asiento</TabLink>}
        <TabLink to="/catalogo/cuentas">Cuentas contables</TabLink>
        <TabLink to="/catalogo/centros-costo">Centros de costo</TabLink>
        <TabLink to="/catalogo/periodos">Periodos</TabLink>
        <TabLink to="/libros/balance">Balance de saldos</TabLink>
        <TabLink to="/libros/diario">Libro diario</TabLink>
        <TabLink to="/libros/mayor">Libro mayor</TabLink>
        <TabLink to="/contrapartes">Clientes y proveedores</TabLink>
        <TabLink to="/cxc">Cuentas por cobrar</TabLink>
        <TabLink to="/cxp">Cuentas por pagar</TabLink>
        {esAdminOContador && <TabLink to="/tesoreria">Tesorería</TabLink>}
        {esAdminOContador && <TabLink to="/reportes/balance-general">Balance general</TabLink>}
        {esAdminOContador && <TabLink to="/reportes/estado-resultados">Estado de resultados</TabLink>}
      </nav>

      <main className="content">
        <Routes>
          <Route path="/" element={<p>Selecciona una sección del catálogo arriba.</p>} />
          <Route
            path="/asientos"
            element={
              esAdminOContador ? (
                <RegistrarAsiento />
              ) : (
                <p>No autorizado: tu perfil ({usuario.perfil}) no puede registrar asientos contables.</p>
              )
            }
          />
          <Route path="/catalogo/cuentas" element={<CuentasContables />} />
          <Route path="/catalogo/centros-costo" element={<CentrosCosto />} />
          <Route path="/catalogo/periodos" element={<PeriodosContables />} />
          <Route path="/libros/balance" element={<BalanceSaldos />} />
          <Route path="/libros/diario" element={<LibroDiario />} />
          <Route path="/libros/mayor" element={<LibroMayor />} />
          <Route path="/contrapartes" element={<Contrapartes />} />
          <Route path="/cxc" element={<CuentasPorCobrar />} />
          <Route path="/cxp" element={<CuentasPorPagar />} />
          <Route
            path="/tesoreria"
            element={
              esAdminOContador ? (
                <Tesoreria />
              ) : (
                <p>No autorizado: tu perfil ({usuario.perfil}) no puede acceder a Tesorería.</p>
              )
            }
          />
          <Route
            path="/reportes/balance-general"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Balance general">
                <BalanceGeneral />
              </Restringida>
            }
          />
          <Route
            path="/reportes/estado-resultados"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Estado de resultados">
                <EstadoResultados />
              </Restringida>
            }
          />
        </Routes>
      </main>
    </div>
  );
}

export default App;
