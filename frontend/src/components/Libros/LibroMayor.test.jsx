import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { cuentasApi, librosApi, periodosApi } from "../../services/api";
import LibroMayor from "./LibroMayor";

vi.mock("../../services/api", () => ({
  librosApi: { mayor: vi.fn() },
  periodosApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
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

const CUENTAS = [
  { id: 1, codigo: "1.1", nombre: "Caja" },
  { id: 2, codigo: "2.1", nombre: "Proveedores" },
];
const PERIODOS = [{ id: 7, nombre: "Enero 2025" }];
const RESULTADO = {
  cuenta: { id: 1, codigo: "1.1", nombre: "Caja", naturaleza: "Deudora", esHoja: true, subcuentas: 0 },
  movimientos: [
    { asientoId: 1, fecha: "2025-01-05", asientoNumero: "AS-001", debito: 100.5, credito: 0, saldoAcumulado: 100.5, cuentaId: 1, cuentaCodigo: "1.1", cuentaNombre: "Caja" },
    { asientoId: 2, fecha: "2025-01-06", asientoNumero: "AS-002", debito: 0, credito: 40, saldoAcumulado: 60.5, cuentaId: 1, cuentaCodigo: "1.1", cuentaNombre: "Caja" },
  ],
};
const CONSOLIDADO = {
  cuenta: { id: 5, codigo: "1.1", nombre: "Caja", naturaleza: "Deudora", esHoja: false, subcuentas: 2 },
  movimientos: [
    { asientoId: 1, fecha: "2025-01-05", asientoNumero: "AS-001", debito: 100.5, credito: 0, saldoAcumulado: 100.5, cuentaId: 6, cuentaCodigo: "1.1.1", cuentaNombre: "Efectivo" },
  ],
};

beforeEach(() => {
  [librosApi, periodosApi, cuentasApi].forEach((api) => Object.values(api).forEach((fn) => fn.mockReset()));
  cuentasApi.listar.mockResolvedValue({ data: CUENTAS });
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  librosApi.mayor.mockResolvedValue({ data: RESULTADO });
});

afterEach(() => {
  vi.restoreAllMocks();
});

async function seleccionarCuenta() {
  render(<LibroMayor />);
  await screen.findByRole("option", { name: "1.1 - Caja" });
  fireEvent.change(screen.getByLabelText("Cuenta"), { target: { value: "1" } });
}

describe("LibroMayor", () => {
  it("carga cuentas incluyendo inactivas y periodos", async () => {
    render(<LibroMayor />);
    await screen.findByRole("option", { name: "1.1 - Caja" });
    await screen.findByRole("option", { name: "Enero 2025" });
    expect(cuentasApi.listar).toHaveBeenCalledWith(true);
    expect(librosApi.mayor).not.toHaveBeenCalled();
  });

  it("muestra los movimientos de la cuenta sin periodo", async () => {
    await seleccionarCuenta();
    await screen.findByText("AS-001");
    expect(librosApi.mayor).toHaveBeenCalledWith(1, undefined);
    expect(screen.getByText("1.1 - Caja", { selector: "strong" })).toBeInTheDocument();
    expect(screen.getByText(/\(Deudora\)/)).toBeInTheDocument();
    const filas = screen.getAllByRole("row");
    expect(filas).toHaveLength(3);
    expect(within(filas[1]).getByText("2025-01-05")).toBeInTheDocument();
    expect(within(filas[1]).getAllByText("100.50")).toHaveLength(2);
    expect(within(filas[2]).getByText("40.00")).toBeInTheDocument();
    expect(within(filas[2]).getByText("60.50")).toBeInTheDocument();
  });

  it("consolidado: muestra la columna Cuenta y la nota de subcuentas", async () => {
    librosApi.mayor.mockResolvedValue({ data: CONSOLIDADO });
    await seleccionarCuenta();
    await screen.findByText("AS-001");
    expect(screen.getByRole("columnheader", { name: "Cuenta" })).toBeInTheDocument();
    expect(screen.getByText("1.1.1 - Efectivo")).toBeInTheDocument();
    expect(screen.getByText("Consolidado de 2 subcuentas")).toBeInTheDocument();
  });

  it("cuenta hoja: sin columna Cuenta ni nota", async () => {
    await seleccionarCuenta();
    await screen.findByText("AS-001");
    expect(screen.queryByRole("columnheader", { name: "Cuenta" })).toBeNull();
    expect(screen.queryByText(/Consolidado de/)).toBeNull();
  });

  it("consulta con el periodo elegido", async () => {
    await seleccionarCuenta();
    await screen.findByText("AS-001");
    await screen.findByRole("option", { name: "Enero 2025" });
    fireEvent.change(screen.getByLabelText("Periodo (opcional)"), { target: { value: "7" } });
    await waitFor(() => expect(librosApi.mayor).toHaveBeenLastCalledWith(1, 7));
  });

  it("avisa cuando la cuenta no tiene movimientos", async () => {
    librosApi.mayor.mockResolvedValue({ data: { cuenta: RESULTADO.cuenta, movimientos: [] } });
    await seleccionarCuenta();
    expect(await screen.findByText("Esta cuenta no tiene movimientos confirmados.")).toBeInTheDocument();
  });

  it("aclara el periodo en el aviso de sin movimientos", async () => {
    librosApi.mayor.mockResolvedValue({ data: { cuenta: RESULTADO.cuenta, movimientos: [] } });
    await seleccionarCuenta();
    expect(await screen.findByText("Esta cuenta no tiene movimientos confirmados.")).toBeInTheDocument();
    await screen.findByRole("option", { name: "Enero 2025" });
    fireEvent.change(screen.getByLabelText("Periodo (opcional)"), { target: { value: "7" } });
    expect(await screen.findByText("Esta cuenta no tiene movimientos confirmados en el periodo seleccionado.")).toBeInTheDocument();
  });

  it("muestra Cargando mientras llega el mayor", async () => {
    const d = deferred();
    librosApi.mayor.mockReturnValue(d.promise);
    await seleccionarCuenta();
    await screen.findByText("Cargando...");
    await act(async () => {
      d.resolve({ data: RESULTADO });
    });
    await screen.findByText("AS-001");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("muestra el error del servidor", async () => {
    librosApi.mayor.mockRejectedValue({ response: { data: { error: "Cuenta inexistente" } } });
    await seleccionarCuenta();
    expect(await screen.findByText("Cuenta inexistente")).toBeInTheDocument();
  });

  it("usa el mensaje por defecto si el error no trae respuesta", async () => {
    librosApi.mayor.mockRejectedValue(new Error("red"));
    await seleccionarCuenta();
    expect(await screen.findByText("No se pudo cargar el libro mayor.")).toBeInTheDocument();
  });

  it("limpia el resultado al deseleccionar la cuenta", async () => {
    await seleccionarCuenta();
    await screen.findByText("AS-001");
    fireEvent.change(screen.getByLabelText("Cuenta"), { target: { value: "" } });
    await waitFor(() => expect(screen.queryByText("AS-001")).toBeNull());
    expect(librosApi.mayor).toHaveBeenCalledTimes(1);
  });
});
