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

function renderApp(perfil, ruta = "/") {
  if (perfil) localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
  return render(<MemoryRouter initialEntries={[ruta]}><App /></MemoryRouter>);
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
    expect(screen.getAllByRole("link")).toHaveLength(13);
    expect(screen.getByRole("link", { name: "Registrar asiento" })).toBeInTheDocument();
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve la pestaña Tesorería y abre su módulo", async (perfil) => {
    renderApp(perfil, "/tesoreria");

    expect(screen.getByRole("link", { name: "Tesorería" })).toBeInTheDocument();
    expect(screen.getByText("Módulo de tesorería")).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Contador", "Administrador del sistema"])("%s ve las pestañas de reportes y abre el balance general", async (perfil) => {
    renderApp(perfil, "/reportes/balance-general");

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

    expect(screen.queryByRole("link", { name: "Balance general" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Estado de resultados" })).toBeNull();
    expect(screen.getByText(`No autorizado: tu perfil (${perfil}) no puede acceder a Balance general.`)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it.each(["Vendedor", "Técnico"])("%s no ve Tesorería y la ruta queda bloqueada", async (perfil) => {
    renderApp(perfil, "/tesoreria");

    expect(screen.queryByRole("link", { name: "Tesorería" })).toBeNull();
    expect(screen.getByText(new RegExp(`No autorizado: tu perfil \\(${perfil}\\) no puede acceder a Tesorería`))).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("un vendedor no ve Registrar asiento y la ruta queda bloqueada", async () => {
    renderApp("Vendedor", "/asientos");

    expect(screen.queryByRole("link", { name: "Registrar asiento" })).toBeNull();
    expect(screen.getByText(/No autorizado: tu perfil \(Vendedor\)/)).toBeInTheDocument();
    await screen.findByText("Conectado a Delta ERP Contable API");
  });

  it("marca como activa la pestaña de la ruta actual", async () => {
    renderApp("Administrador del sistema", "/asientos");

    expect(screen.getByText("Formulario de asiento")).toBeInTheDocument();
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
});
