import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { movimientosTesoreriaApi } from "../../services/api";
import Movimientos from "./Movimientos";

vi.mock("../../services/api", () => ({ movimientosTesoreriaApi: { listar: vi.fn(), crear: vi.fn(), transferir: vi.fn() } }));

const cuentasBancarias = [
  { id: 7, banco: "BAC", numero: "123", activa: true },
  { id: 8, banco: "Viejo", numero: "000", activa: false },
];
const cuentasContables = [{ id: 5, codigo: "5101", nombre: "Gastos bancarios", tipo: "Gasto", activa: true, cuentaPadreId: null }];
const movimientos = [
  { id: 1, cuentaBancariaId: 7, fecha: "2025-03-01", tipo: "Ingreso", monto: 500, descripcion: "Depósito inicial", referencia: null, origen: "Manual", asientoId: 11, conciliado: false },
];

describe("Movimientos", () => {
  beforeEach(() => {
    Object.values(movimientosTesoreriaApi).forEach((fn) => fn.mockReset());
    movimientosTesoreriaApi.listar.mockResolvedValue({ data: movimientos });
    movimientosTesoreriaApi.crear.mockResolvedValue({ data: {} });
  });

  function renderMovimientos() {
    render(<Movimientos cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} />);
  }

  it("lista los movimientos con el indicador Conciliado y el aviso de corrección", async () => {
    renderMovimientos();

    expect(await screen.findByText("Depósito inicial")).toBeInTheDocument();
    expect(movimientosTesoreriaApi.listar).toHaveBeenCalledWith({});
    expect(screen.getByText("Conciliado")).toBeInTheDocument();
    expect(screen.getByText(/registra uno inverso/)).toBeInTheDocument();
  });

  it("envía solo los filtros con valor al listar", async () => {
    renderMovimientos();
    await screen.findByText("Depósito inicial");

    fireEvent.change(screen.getByLabelText("Filtrar por cuenta"), { target: { value: "8" } });
    await waitFor(() => expect(movimientosTesoreriaApi.listar).toHaveBeenLastCalledWith({ cuentaBancariaId: "8" }));

    fireEvent.change(screen.getByLabelText("Desde"), { target: { value: "2025-01-01" } });
    fireEvent.change(screen.getByLabelText("Hasta"), { target: { value: "2025-03-31" } });
    fireEvent.change(screen.getByLabelText("Filtrar por origen"), { target: { value: "CxC" } });

    await waitFor(() =>
      expect(movimientosTesoreriaApi.listar).toHaveBeenLastCalledWith({ cuentaBancariaId: "8", desde: "2025-01-01", hasta: "2025-03-31", origen: "CxC" })
    );
  });

  it("registra un movimiento y recarga la lista", async () => {
    renderMovimientos();
    await screen.findByText("Depósito inicial");

    fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText("Fecha del movimiento"), { target: { value: "2025-03-02" } });
    fireEvent.change(screen.getByLabelText("Monto del movimiento"), { target: { value: "75.5" } });
    fireEvent.change(screen.getByLabelText("Descripción del movimiento"), { target: { value: "Comisión" } });
    fireEvent.change(screen.getByLabelText("Cuenta de contrapartida"), { target: { value: "5" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(movimientosTesoreriaApi.crear).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(movimientosTesoreriaApi.listar).toHaveBeenCalledTimes(2));
  });

  it("muestra el error del servidor al listar", async () => {
    movimientosTesoreriaApi.listar.mockRejectedValue({ response: { data: { error: "La fecha inicial no puede ser posterior a la final." } } });
    renderMovimientos();

    expect(await screen.findByText("La fecha inicial no puede ser posterior a la final.")).toBeInTheDocument();
  });

  it("usa un mensaje por defecto si la carga falla sin cuerpo", async () => {
    movimientosTesoreriaApi.listar.mockRejectedValue(new Error("red"));
    renderMovimientos();

    expect(await screen.findByText("No se pudieron cargar los movimientos.")).toBeInTheDocument();
  });
});
