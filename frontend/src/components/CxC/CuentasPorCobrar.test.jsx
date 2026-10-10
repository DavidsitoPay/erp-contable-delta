import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi, contrapartesApi, cuentasApi, impuestosApi, periodosApi, cxcApi } from "../../services/api";
import CuentasPorCobrar from "./CuentasPorCobrar";

vi.mock("../../services/api", () => ({
  centrosCostoApi: { listar: vi.fn() },
  contrapartesApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  impuestosApi: { listar: vi.fn() },
  periodosApi: { listar: vi.fn() },
  cxcApi: { listarFacturas: vi.fn(), listarPagos: vi.fn() },
}));

const cuentas = [
  { id: 1, codigo: "1101", nombre: "Clientes", tipo: "Activo", naturaleza: "Deudora", activa: true },
];
const contrapartes = [{ id: 5, nombre: "ACME" }];
const periodos = [{ id: 10, nombre: "Enero 2025", estado: "Abierto" }];

describe("CuentasPorCobrar", () => {
  beforeEach(() => {
    contrapartesApi.listar.mockReset();
    cuentasApi.listar.mockReset();
    centrosCostoApi.listar.mockReset();
    periodosApi.listar.mockReset();
    cxcApi.listarFacturas.mockReset();
    cxcApi.listarPagos.mockReset();
    contrapartesApi.listar.mockResolvedValue({ data: contrapartes });
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
    centrosCostoApi.listar.mockResolvedValue({ data: [] });
    periodosApi.listar.mockResolvedValue({ data: periodos });
    impuestosApi.listar.mockResolvedValue({ data: [] });
    cxcApi.listarFacturas.mockResolvedValue({ data: [] });
    cxcApi.listarPagos.mockResolvedValue({ data: [] });
  });

  it("renderiza con título 'Cuentas por cobrar'", async () => {
    render(<CuentasPorCobrar />);
    expect(await screen.findByText("Cuentas por cobrar")).toBeInTheDocument();
  });

  it("renderiza etiqueta 'Cliente' y no 'Proveedor'", async () => {
    render(<CuentasPorCobrar />);
    expect(await screen.findByLabelText("Cliente")).toBeInTheDocument();
    expect(screen.queryByLabelText("Proveedor")).toBeNull();
  });

  it("carga contrapartes como clientes via contrapartesApi", async () => {
    render(<CuentasPorCobrar />);
    await waitFor(() => expect(contrapartesApi.listar).toHaveBeenCalledWith("Cliente"));
  });

  it("consulta los impuestos de ventas y pide los datos del DTE como obligatorios", async () => {
    render(<CuentasPorCobrar />);

    expect(await screen.findByText("Datos del DTE")).toBeInTheDocument();
    await waitFor(() => expect(impuestosApi.listar).toHaveBeenCalledWith(expect.objectContaining({ aplicaA: "VENTAS" })));
    expect(screen.getByLabelText("Serie")).toBeRequired();
  });
});
