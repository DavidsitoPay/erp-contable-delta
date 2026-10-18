import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { checkHealth } from "./services/api";
import App from "./App";

vi.mock("./services/api", async (importOriginal) => ({ ...(await importOriginal()), checkHealth: vi.fn() }));
vi.mock("./components/Asientos/RegistrarAsiento", () => ({ default: () => <p>Formulario de asiento</p> }));
vi.mock("./components/Tesoreria/Tesoreria", () => ({ default: () => <p>Módulo de tesorería</p> }));
vi.mock("./components/Reportes/BalanceGeneral", () => ({ default: () => <p>Reporte balance general</p> }));
vi.mock("./components/Reportes/EstadoResultados", () => ({ default: () => <p>Reporte estado de resultados</p> }));
vi.mock("./components/Catalogo/ConfiguracionFiscal", () => ({ default: () => <p>Pantalla configuración fiscal</p> }));
vi.mock("./components/Catalogo/Impuestos", () => ({ default: () => <p>Catálogo de impuestos</p> }));
vi.mock("./components/Libros/LibroFiscal", () => ({ default: () => <p>Libro fiscal</p> }));
vi.mock("./components/TiposCambio/TiposCambio", () => ({ default: () => <p>Pantalla tipos de cambio</p> }));
vi.mock("./components/Revaluaciones/Revaluaciones", () => ({ default: () => <p>Pantalla revaluación</p> }));
vi.mock("./components/Reportes/SaldosMoneda", () => ({ default: () => <p>Reporte saldos en moneda</p> }));
vi.mock("./components/Retenciones/ParametrosRetencion", () => ({ default: () => <p>Pantalla parámetros de retención</p> }));
vi.mock("./components/Retenciones/ReporteRetenciones", () => ({ default: () => <p>Reporte de retenciones</p> }));
vi.mock("./components/Retenciones/Enteramiento", () => ({ default: () => <p>Pantalla enteramiento ISR</p> }));
vi.mock("./components/Retenciones/Feriados", () => ({ default: () => <p>Pantalla feriados</p> }));

function renderApp(perfil, ruta = "/") {
  if (perfil) localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
  return render(<MemoryRouter initialEntries={[ruta]}><App /></MemoryRouter>);
}

function abrir(nombre) {
  fireEvent.click(screen.getByRole("button", { name: nombre }));
}

describe("App", () => {
  beforeEach(() => {
    checkHealth.mockReset();
    checkHealth.mockResolvedValue({});
  });

  it("muestra el login sin usuario guardado", () => {
    renderApp(null);
    expect(screen.getByRole("button", { name: "Ingresar" })).toBeInTheDocument();
  });

  it("un contador ve todas las pestañas y el estado de la API", async () => {
    renderApp("Contador");

    expect(await screen.findByText("Conectado a Delta ERP Contable API")).toBeInTheDocument();
    const cantidades = { Catálogo: 4, Operaciones: 6, Libros: 3, Fiscal: 6, Reportes: 3 };
    Object.entries(cantidades).forEach(([grupo, cantidad]) => {
      abrir(grupo);
      expect(screen.getAllByRole("link")).toHaveLength(cantidad);
    });
    abrir("Operaciones");
    expect(screen.getByRole("link", { name: "Registrar asiento" })).toBeInTheDocument();
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve la pestaña Tesorería y abre su módulo", async (perfil) => {
    renderApp(perfil, "/tesoreria");

    abrir("Operaciones");
    expect(screen.getByRole("link", { name: "Tesorería" })).toBeInTheDocument();
    expect(screen.getByText("Módulo de tesorería")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve las pestañas de reportes y abre el balance general", async (perfil) => {
    renderApp(perfil, "/reportes/balance-general");

    abrir("Reportes");
    expect(screen.getByRole("link", { name: "Balance general" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Estado de resultados" })).toBeInTheDocument();
    expect(screen.getByText("Reporte balance general")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("abre el estado de resultados desde su ruta", async () => {
    renderApp("Contador", "/reportes/estado-resultados");

    expect(screen.getByText("Reporte estado de resultados")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve las pestañas de reportes y las rutas quedan bloqueadas", async (perfil) => {
    renderApp(perfil, "/reportes/balance-general");

    expect(screen.queryByRole("button", { name: "Reportes" })).toBeNull();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Balance general.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve Tesorería y la ruta queda bloqueada", async (perfil) => {
    renderApp(perfil, "/tesoreria");

    abrir("Operaciones");
    expect(screen.queryByRole("link", { name: "Tesorería" })).toBeNull();
    expect(screen.getByText(new RegExp(`No autorizado: tu perfil \\(${perfil}\\) no puede acceder a Tesorería`))).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("un vendedor no ve Registrar asiento y la ruta queda bloqueada", async () => {
    renderApp("Vendedor", "/asientos");

    abrir("Operaciones");
    expect(screen.queryByRole("link", { name: "Registrar asiento" })).toBeNull();
    expect(screen.getByText(/No autorizado: tu perfil \(Vendedor\)/)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("un administrador ve las pantallas fiscales y abre la configuración fiscal", async () => {
    renderApp("Administrador del sistema", "/catalogo/configuracion-fiscal");

    abrir("Fiscal");
    expect(screen.getAllByRole("link")).toHaveLength(7);
    ["Configuración fiscal", "Impuestos", "Parámetros de retención", "Compras y ventas", "Retenciones"].forEach((nombre) =>
      expect(screen.getByRole("link", { name: nombre })).toBeInTheDocument()
    );
    expect(screen.getByText("Pantalla configuración fiscal")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each([
    ["/catalogo/impuestos", "Catálogo de impuestos"],
    ["/libros/fiscal", "Libro fiscal"],
    ["/catalogo/parametros-retencion", "Pantalla parámetros de retención"],
    ["/libros/retenciones", "Reporte de retenciones"],
    ["/retenciones/enteramiento", "Pantalla enteramiento ISR"],
    ["/catalogo/feriados", "Pantalla feriados"],
  ])("un contador abre %s", async (ruta, contenido) => {
    renderApp("Contador", ruta);

    abrir("Fiscal");
    expect(screen.getByRole("link", { name: "Impuestos" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Compras y ventas" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Parámetros de retención" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Retenciones" })).toBeInTheDocument();
    expect(screen.getByText(contenido)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("un contador no ve la configuración fiscal y la ruta queda bloqueada", async () => {
    renderApp("Contador", "/catalogo/configuracion-fiscal");

    abrir("Fiscal");
    expect(screen.queryByRole("link", { name: "Configuración fiscal" })).toBeNull();
    expect(screen.getByText("No autorizado: tu perfil (Contador) no puede acceder a Configuración fiscal.")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve Cuentas por pagar y la ruta queda bloqueada", async (perfil) => {
    renderApp(perfil, "/cxp");

    abrir("Operaciones");
    expect(screen.queryByRole("link", { name: "Cuentas por pagar" })).toBeNull();
    expect(screen.getByRole("link", { name: "Cuentas por cobrar" })).toBeInTheDocument();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Cuentas por pagar.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each([
    ["Vendedor", "/catalogo/configuracion-fiscal", "Configuración fiscal"],
    ["Vendedor", "/catalogo/impuestos", "Impuestos"],
    ["Vendedor", "/libros/fiscal", "Compras y ventas"],
    ["Vendedor", "/catalogo/parametros-retencion", "Parámetros de retención"],
    ["Vendedor", "/libros/retenciones", "Retenciones"],
    ["Vendedor", "/retenciones/enteramiento", "Enteramiento ISR"],
    ["Vendedor", "/catalogo/feriados", "Feriados"],
    ["Técnico", "/catalogo/configuracion-fiscal", "Configuración fiscal"],
    ["Técnico", "/catalogo/impuestos", "Impuestos"],
    ["Técnico", "/libros/fiscal", "Compras y ventas"],
    ["Técnico", "/catalogo/parametros-retencion", "Parámetros de retención"],
    ["Técnico", "/libros/retenciones", "Retenciones"],
    ["Técnico", "/retenciones/enteramiento", "Enteramiento ISR"],
    ["Técnico", "/catalogo/feriados", "Feriados"],
  ])("%s no ve las pestañas fiscales y %s queda bloqueada", async (perfil, ruta, seccion) => {
    renderApp(perfil, ruta);

    expect(screen.queryByRole("button", { name: "Fiscal" })).toBeNull();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a ${seccion}.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("marca como activa la pestaña de la ruta actual", async () => {
    renderApp("Administrador del sistema", "/asientos");

    expect(screen.getByText("Formulario de asiento")).toBeInTheDocument();
    abrir("Operaciones");
    expect(screen.getByRole("link", { name: "Registrar asiento" })).toHaveClass("active");
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("informa cuando la API no responde", async () => {
    checkHealth.mockRejectedValue(new Error("down"));
    renderApp("Contador");

    expect(await screen.findByText(/No se pudo conectar con la API/)).toBeInTheDocument();
  });

  it("cierra sesion y vuelve al login", async () => {
    renderApp("Contador");
    await screen.findByText("Conectado a Delta ERP Contable API");

    fireEvent.click(screen.getByRole("button", { name: "Cerrar sesión" }));

    expect(screen.getByRole("button", { name: "Ingresar" })).toBeInTheDocument();
    expect(localStorage.getItem("delta_usuario")).toBeNull();
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve Tipos de cambio y Revaluación y abre cada pantalla", async (perfil) => {
    renderApp(perfil, "/catalogo/tipos-cambio");

    abrir("Catálogo");
    expect(screen.getByRole("link", { name: "Tipos de cambio" })).toBeInTheDocument();
    expect(screen.getByText("Pantalla tipos de cambio")).toBeInTheDocument();
    abrir("Operaciones");
    expect(screen.getByRole("link", { name: "Revaluación" })).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Contador", "Administrador del sistema"])("%s abre la pantalla de revaluación", async (perfil) => {
    renderApp(perfil, "/revaluaciones");

    expect(screen.getByText("Pantalla revaluación")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve Tipos de cambio ni Revaluación y las rutas quedan bloqueadas", async (perfil) => {
    renderApp(perfil, "/catalogo/tipos-cambio");

    abrir("Catálogo");
    expect(screen.queryByRole("link", { name: "Tipos de cambio" })).toBeNull();
    abrir("Operaciones");
    expect(screen.queryByRole("link", { name: "Revaluación" })).toBeNull();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Tipos de cambio.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no puede abrir la revaluación", async (perfil) => {
    renderApp(perfil, "/revaluaciones");

    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Revaluación.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve Saldos en moneda y abre el reporte", async (perfil) => {
    renderApp(perfil, "/reportes/saldos-moneda");

    abrir("Reportes");
    expect(screen.getByRole("link", { name: "Saldos en moneda" })).toBeInTheDocument();
    expect(screen.getByText("Reporte saldos en moneda")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve Saldos en moneda y la ruta queda bloqueada", async (perfil) => {
    renderApp(perfil, "/reportes/saldos-moneda");

    expect(screen.queryByRole("button", { name: "Reportes" })).toBeNull();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Saldos en moneda.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });
});
