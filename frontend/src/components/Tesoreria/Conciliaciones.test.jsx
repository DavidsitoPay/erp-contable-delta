import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { conciliacionesApi } from "../../services/api";
import Conciliaciones from "./Conciliaciones";

vi.mock("../../services/api", () => ({ conciliacionesApi: { listar: vi.fn(), crear: vi.fn() } }));
vi.mock("./ConciliacionDetalle", () => ({ default: ({ id }) => <p>Detalle abierto {id}</p> }));

const cuentasBancarias = [{ id: 7, banco: "BAC", numero: "123", activa: true }];
const lista = [{ id: 4, cuentaBancariaId: 7, fecha: "2025-03-31", saldoExtracto: 1000, estado: "Pendiente" }];

function renderConciliaciones() {
  render(<Conciliaciones cuentasBancarias={cuentasBancarias} cuentasContables={[]} />);
}

function llenarFormulario() {
  fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
  fireEvent.change(screen.getByLabelText("Fecha de corte"), { target: { value: "2025-04-30" } });
  fireEvent.change(screen.getByLabelText("Saldo del extracto"), { target: { value: "1500.5" } });
}

describe("Conciliaciones", () => {
  beforeEach(() => {
    Object.values(conciliacionesApi).forEach((fn) => fn.mockReset());
    conciliacionesApi.listar.mockResolvedValue({ data: lista });
    conciliacionesApi.crear.mockResolvedValue({ data: { id: 31 } });
  });

  it("lista las conciliaciones con cuenta, fecha de corte y estado", async () => {
    renderConciliaciones();

    expect(await screen.findByText("Pendiente")).toBeInTheDocument();
    expect(screen.getByText("BAC 123")).toBeInTheDocument();
    expect(screen.getByText("2025-03-31")).toBeInTheDocument();
    expect(screen.queryByText(/Detalle abierto/)).toBeNull();
  });

  it("abre el detalle de la conciliación elegida", async () => {
    renderConciliaciones();

    fireEvent.click(await screen.findByRole("button", { name: "Ver detalle de conciliación 4" }));

    expect(screen.getByText("Detalle abierto 4")).toBeInTheDocument();
  });

  it("crea una conciliación, abre su detalle y recarga la lista", async () => {
    renderConciliaciones();
    await screen.findByText("Pendiente");
    llenarFormulario();
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    await waitFor(() => expect(conciliacionesApi.crear).toHaveBeenCalledWith({ cuentaBancariaId: 7, fecha: "2025-04-30", saldoExtracto: 1500.5 }));
    expect(await screen.findByText("Detalle abierto 31")).toBeInTheDocument();
    await waitFor(() => expect(conciliacionesApi.listar).toHaveBeenCalledTimes(2));
  });

  it("muestra el error del servidor y el mensaje por defecto al crear", async () => {
    conciliacionesApi.crear.mockRejectedValueOnce({ response: { data: { error: "Ya existe una conciliación pendiente para esta cuenta bancaria." } } });
    renderConciliaciones();
    await screen.findByText("Pendiente");
    llenarFormulario();
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    expect(await screen.findByText("Ya existe una conciliación pendiente para esta cuenta bancaria.")).toBeInTheDocument();

    conciliacionesApi.crear.mockRejectedValueOnce(new Error("red"));
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    expect(await screen.findByText("No se pudo crear la conciliación.")).toBeInTheDocument();
  });
});
