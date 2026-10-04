import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { centrosCostoApi, librosApi, periodosApi } from "../../services/api";
import LibroDiario from "./LibroDiario";

vi.mock("../../services/api", () => ({
  librosApi: { diario: vi.fn() },
  periodosApi: { listar: vi.fn() },
  centrosCostoApi: { listar: vi.fn() },
}));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const PERIODOS = [{ id: 1, nombre: "Enero 2025" }, { id: 2, nombre: "Febrero 2025" }];
const ASIENTOS = [
  {
    id: 1,
    fecha: "2025-01-05",
    numero: "AS-001",
    lineas: [
      { cuentaCodigo: "1.1", cuentaNombre: "Caja", centroCostoId: 10, debito: 150.5, credito: 0 },
      { cuentaCodigo: "4.1", cuentaNombre: "Ingresos", centroCostoId: null, debito: 0, credito: 150.5 },
    ],
  },
];

beforeEach(() => {
  [librosApi, periodosApi, centrosCostoApi].forEach((api) => Object.values(api).forEach((fn) => fn.mockReset()));
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  centrosCostoApi.listar.mockResolvedValue({ data: [{ id: 10, nombre: "Ventas" }] });
  librosApi.diario.mockResolvedValue({ data: ASIENTOS });
});

afterEach(() => {
  vi.restoreAllMocks();
});

async function seleccionarPeriodo(valor = "1") {
  render(<LibroDiario />);
  await screen.findByRole("option", { name: "Enero 2025" });
  fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: valor } });
}

describe("LibroDiario", () => {
  it("carga periodos y centros de costo incluyendo inactivos", async () => {
    render(<LibroDiario />);
    await screen.findByRole("option", { name: "Febrero 2025" });
    expect(centrosCostoApi.listar).toHaveBeenCalledWith(true);
    expect(screen.getByRole("heading", { name: "Libro diario" })).toBeInTheDocument();
  });

  it("no consulta el diario sin periodo seleccionado", async () => {
    render(<LibroDiario />);
    await screen.findByRole("option", { name: "Enero 2025" });
    expect(librosApi.diario).not.toHaveBeenCalled();
    expect(screen.queryByText("No hay asientos confirmados en este periodo.")).toBeNull();
  });

  it("muestra los asientos del periodo con centro de costo", async () => {
    await seleccionarPeriodo();
    await screen.findByText("Ventas");
    await screen.findByText("AS-001");
    expect(librosApi.diario).toHaveBeenCalledWith(1);
    const filas = screen.getAllByRole("row");
    expect(filas).toHaveLength(3);
    expect(within(filas[1]).getByText("2025-01-05")).toBeInTheDocument();
    expect(within(filas[1]).getByText("AS-001")).toBeInTheDocument();
    expect(within(filas[1]).getByText("1.1 - Caja")).toBeInTheDocument();
    expect(within(filas[1]).getByText("Ventas")).toBeInTheDocument();
    expect(within(filas[1]).getByText("150.50")).toBeInTheDocument();
    expect(within(filas[2]).queryByText("AS-001")).toBeNull();
    expect(within(filas[2]).getByText("4.1 - Ingresos")).toBeInTheDocument();
    expect(within(filas[2]).getByText("—")).toBeInTheDocument();
    expect(within(filas[2]).getByText("150.50")).toBeInTheDocument();
  });

  it("muestra Cargando mientras llega el diario", async () => {
    const d = deferred();
    librosApi.diario.mockReturnValue(d.promise);
    await seleccionarPeriodo();
    await screen.findByText("Cargando...");
    await act(async () => {
      d.resolve({ data: ASIENTOS });
    });
    await screen.findByText("AS-001");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("avisa cuando el periodo no tiene asientos", async () => {
    librosApi.diario.mockResolvedValue({ data: [] });
    await seleccionarPeriodo();
    expect(await screen.findByText("No hay asientos confirmados en este periodo.")).toBeInTheDocument();
  });

  it("muestra el error del servidor y oculta el aviso de vacio", async () => {
    librosApi.diario.mockRejectedValue({ response: { data: { error: "Periodo cerrado" } } });
    await seleccionarPeriodo();
    await screen.findByText("Periodo cerrado");
    expect(screen.queryByText("No hay asientos confirmados en este periodo.")).toBeNull();
  });

  it("usa el mensaje por defecto si el error no trae respuesta", async () => {
    librosApi.diario.mockRejectedValue(new Error("red"));
    await seleccionarPeriodo();
    expect(await screen.findByText("No se pudo cargar el libro diario.")).toBeInTheDocument();
  });

  it("limpia los asientos al deseleccionar el periodo", async () => {
    await seleccionarPeriodo();
    await screen.findByText("AS-001");
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "" } });
    await waitFor(() => expect(screen.queryByText("AS-001")).toBeNull());
    expect(librosApi.diario).toHaveBeenCalledTimes(1);
  });
});
