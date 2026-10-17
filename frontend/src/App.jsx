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
import ConfiguracionFiscal from "./components/Catalogo/ConfiguracionFiscal";
import Impuestos from "./components/Catalogo/Impuestos";
import ParametrosRetencion from "./components/Retenciones/ParametrosRetencion";
import ReporteRetenciones from "./components/Retenciones/ReporteRetenciones";
import Enteramiento from "./components/Retenciones/Enteramiento";
import Feriados from "./components/Retenciones/Feriados";
import LibroFiscal from "./components/Libros/LibroFiscal";
import TiposCambio from "./components/TiposCambio/TiposCambio";
import Revaluaciones from "./components/Revaluaciones/Revaluaciones";
import SaldosMoneda from "./components/Reportes/SaldosMoneda";
import { puedeAdministrarConfiguracionFiscal, puedeGestionarFiscal, puedeGestionarTesoreria } from "./utils/perfiles";

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
  const esAdmin = puedeAdministrarConfiguracionFiscal(usuario.perfil);
  const gestionaFiscal = puedeGestionarFiscal(usuario.perfil);

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
        {esAdminOContador && <TabLink to="/catalogo/tipos-cambio">Tipos de cambio</TabLink>}
        {esAdmin && <TabLink to="/catalogo/configuracion-fiscal">Configuración fiscal</TabLink>}
        {gestionaFiscal && <TabLink to="/catalogo/impuestos">Impuestos</TabLink>}
        {gestionaFiscal && <TabLink to="/catalogo/parametros-retencion">Parámetros de retención</TabLink>}
        <TabLink to="/libros/balance">Balance de saldos</TabLink>
        <TabLink to="/libros/diario">Libro diario</TabLink>
        <TabLink to="/libros/mayor">Libro mayor</TabLink>
        {gestionaFiscal && <TabLink to="/libros/fiscal">Compras y ventas</TabLink>}
        {gestionaFiscal && <TabLink to="/libros/retenciones">Retenciones</TabLink>}
        {gestionaFiscal && <TabLink to="/retenciones/enteramiento">Enteramiento ISR</TabLink>}
        {gestionaFiscal && <TabLink to="/catalogo/feriados">Feriados</TabLink>}
        <TabLink to="/contrapartes">Clientes y proveedores</TabLink>
        <TabLink to="/cxc">Cuentas por cobrar</TabLink>
        {esAdminOContador && <TabLink to="/cxp">Cuentas por pagar</TabLink>}
        {esAdminOContador && <TabLink to="/tesoreria">Tesorería</TabLink>}
        {esAdminOContador && <TabLink to="/revaluaciones">Revaluación</TabLink>}
        {esAdminOContador && <TabLink to="/reportes/balance-general">Balance general</TabLink>}
        {esAdminOContador && <TabLink to="/reportes/estado-resultados">Estado de resultados</TabLink>}
        {esAdminOContador && <TabLink to="/reportes/saldos-moneda">Saldos en moneda</TabLink>}
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
          <Route
            path="/catalogo/tipos-cambio"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Tipos de cambio">
                <TiposCambio />
              </Restringida>
            }
          />
          <Route
            path="/reportes/saldos-moneda"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Saldos en moneda">
                <SaldosMoneda />
              </Restringida>
            }
          />
          <Route
            path="/revaluaciones"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Revaluación">
                <Revaluaciones />
              </Restringida>
            }
          />
          <Route
            path="/catalogo/configuracion-fiscal"
            element={
              <Restringida permitido={esAdmin} perfil={usuario.perfil} seccion="Configuración fiscal">
                <ConfiguracionFiscal />
              </Restringida>
            }
          />
          <Route
            path="/catalogo/impuestos"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Impuestos">
                <Impuestos />
              </Restringida>
            }
          />
          <Route
            path="/catalogo/parametros-retencion"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Parámetros de retención">
                <ParametrosRetencion />
              </Restringida>
            }
          />
          <Route path="/libros/balance" element={<BalanceSaldos />} />
          <Route path="/libros/diario" element={<LibroDiario />} />
          <Route path="/libros/mayor" element={<LibroMayor />} />
          <Route
            path="/libros/fiscal"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Compras y ventas">
                <LibroFiscal />
              </Restringida>
            }
          />
          <Route
            path="/libros/retenciones"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Retenciones">
                <ReporteRetenciones />
              </Restringida>
            }
          />
          <Route
            path="/retenciones/enteramiento"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Enteramiento ISR">
                <Enteramiento />
              </Restringida>
            }
          />
          <Route
            path="/catalogo/feriados"
            element={
              <Restringida permitido={gestionaFiscal} perfil={usuario.perfil} seccion="Feriados">
                <Feriados />
              </Restringida>
            }
          />
          <Route path="/contrapartes" element={<Contrapartes />} />
          <Route path="/cxc" element={<CuentasPorCobrar />} />
          <Route
            path="/cxp"
            element={
              <Restringida permitido={esAdminOContador} perfil={usuario.perfil} seccion="Cuentas por pagar">
                <CuentasPorPagar />
              </Restringida>
            }
          />
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
