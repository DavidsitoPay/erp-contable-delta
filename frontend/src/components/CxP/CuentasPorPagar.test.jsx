import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi, contrapartesApi, cuentasApi, periodosApi, cxpApi } from "../../services/api";
import CuentasPorPagar from "./CuentasPorPagar";

vi.mock("../../services/api", () => ({
  centrosCostoApi: { listar: vi.fn() },
  contrapartesApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  periodosApi: { listar: vi.fn() },
  cxpApi: { listarFacturas: vi.fn(), listarPagos: vi.fn() },
}));

const cuentas = [
  { id: 2, codigo: "2101", nombre: "Proveedores", tipo: "Pasivo", naturaleza: "Acreedora", activa: true },
];
const contrapartes = [{ id: 7, nombre: "Proveedor A" }];
const periodos = [{ id: 10, nombre: "Enero 2025", estado: "Abierto" }];

describe("CuentasPorPagar", () => {
  beforeEach(() => {
    contrapartesApi.listar.mockReset();
    cuentasApi.listar.mockReset();
    centrosCostoApi.listar.mockReset();
    periodosApi.listar.mockReset();
    cxpApi.listarFacturas.mockReset();
    cxpApi.listarPagos.mockReset();
    contrapartesApi.listar.mockResolvedValue({ data: contrapartes });
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
    centrosCostoApi.listar.mockResolvedValue({ data: [] });
    periodosApi.listar.mockResolvedValue({ data: periodos });
    cxpApi.listarFacturas.mockResolvedValue({ data: [] });
    cxpApi.listarPagos.mockResolvedValue({ data: [] });
  });

  it("renderiza con título 'Cuentas por pagar'", async () => {
    render(<CuentasPorPagar />);
    expect(await screen.findByText("Cuentas por pagar")).toBeInTheDocument();
  });

  it("renderiza etiqueta 'Proveedor' y no 'Cliente'", async () => {
    render(<CuentasPorPagar />);
    expect(await screen.findByLabelText("Proveedor")).toBeInTheDocument();
    expect(screen.queryByLabelText("Cliente")).toBeNull();
  });

  it("carga contrapartes como proveedores via contrapartesApi", async () => {
    render(<CuentasPorPagar />);
    await waitFor(() => expect(contrapartesApi.listar).toHaveBeenCalledWith("Proveedor"));
  });
});
