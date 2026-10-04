import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, render, screen } from "@testing-library/react";
import { librosApi } from "../../services/api";
import BalanceSaldos from "./BalanceSaldos";

vi.mock("../../services/api", () => ({ librosApi: { balanceSaldos: vi.fn() } }));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const FILAS = [
  { cuentaId: 1, codigo: "1.1", nombre: "Caja", naturaleza: "Deudora", totalDebito: 300, totalCredito: 100, saldo: 200 },
  { cuentaId: 2, codigo: "2.1", nombre: "Proveedores", naturaleza: "Acreedora", totalDebito: 20, totalCredito: 70, saldo: 50 },
];

beforeEach(() => {
  librosApi.balanceSaldos.mockReset();
  librosApi.balanceSaldos.mockResolvedValue({ data: FILAS });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("BalanceSaldos", () => {
  it("muestra Cargando mientras espera y luego la tabla", async () => {
    const d = deferred();
    librosApi.balanceSaldos.mockReturnValue(d.promise);
    render(<BalanceSaldos />);
    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    await act(async () => {
      d.resolve({ data: FILAS });
    });
    await screen.findByText("Caja");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("lista las cuentas con montos formateados", async () => {
    render(<BalanceSaldos />);
    await screen.findByText("Caja");
    expect(screen.getByText("1.1")).toBeInTheDocument();
    expect(screen.getByText("Proveedores")).toBeInTheDocument();
    expect(screen.getByText("2.1")).toBeInTheDocument();
    expect(screen.getByText("Deudora")).toBeInTheDocument();
    expect(screen.getByText("Acreedora")).toBeInTheDocument();
    expect(screen.getByText("300.00")).toBeInTheDocument();
    expect(screen.getByText("100.00")).toBeInTheDocument();
    expect(screen.getByText("200.00")).toBeInTheDocument();
    expect(screen.getByText("20.00")).toBeInTheDocument();
    expect(screen.getByText("70.00")).toBeInTheDocument();
    expect(screen.getByText("50.00")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Balance de saldos" })).toBeInTheDocument();
  });

  it("muestra 0.00 para una cuenta sin movimiento", async () => {
    librosApi.balanceSaldos.mockResolvedValue({
      data: [
        { cuentaId: 3, codigo: "3.1", nombre: "Capital", naturaleza: "Acreedora", totalDebito: 0, totalCredito: 0, saldo: 0 },
      ],
    });
    render(<BalanceSaldos />);
    await screen.findByText("Capital");
    expect(screen.getAllByText("0.00")).toHaveLength(3);
  });

  it("muestra el error del servidor", async () => {
    librosApi.balanceSaldos.mockRejectedValue({ response: { data: { error: "Sin permisos" } } });
    render(<BalanceSaldos />);
    await screen.findByText("Sin permisos");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("muestra el mensaje por defecto si el error no trae respuesta", async () => {
    librosApi.balanceSaldos.mockRejectedValue(new Error("red"));
    render(<BalanceSaldos />);
    expect(await screen.findByText("No se pudo cargar el balance de saldos.")).toBeInTheDocument();
  });
});
