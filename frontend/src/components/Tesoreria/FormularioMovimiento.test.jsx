import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { movimientosTesoreriaApi } from "../../services/api";
import FormularioMovimiento from "./FormularioMovimiento";

vi.mock("../../services/api", () => ({ movimientosTesoreriaApi: { crear: vi.fn(), transferir: vi.fn() } }));

const cuentasBancarias = [
  { id: 7, banco: "BAC", numero: "123", activa: true },
  { id: 8, banco: "Industrial", numero: "9", activa: true },
  { id: 9, banco: "Viejo", numero: "0", activa: false },
];
const cuentasContables = [
  { id: 20, codigo: "1", nombre: "Activos", tipo: "Activo", activa: true, cuentaPadreId: null },
  { id: 1, codigo: "1101", nombre: "Banco", tipo: "Activo", activa: true, cuentaPadreId: 20 },
  { id: 5, codigo: "5101", nombre: "Gastos bancarios", tipo: "Gasto", activa: true, cuentaPadreId: null },
  { id: 6, codigo: "4201", nombre: "Intereses", tipo: "Ingreso", activa: true, cuentaPadreId: null },
];

function renderFormulario(extra = {}) {
  const onRegistrado = vi.fn().mockResolvedValue(undefined);
  render(<FormularioMovimiento cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onRegistrado={onRegistrado} {...extra} />);
  return onRegistrado;
}

function llenarComunes() {
  fireEvent.change(screen.getByLabelText("Fecha del movimiento"), { target: { value: "2025-03-01" } });
  fireEvent.change(screen.getByLabelText("Monto del movimiento"), { target: { value: "200" } });
  fireEvent.change(screen.getByLabelText("Descripción del movimiento"), { target: { value: " Comisión " } });
}

describe("FormularioMovimiento", () => {
  beforeEach(() => {
    movimientosTesoreriaApi.crear.mockReset();
    movimientosTesoreriaApi.transferir.mockReset();
    movimientosTesoreriaApi.crear.mockResolvedValue({ data: {} });
    movimientosTesoreriaApi.transferir.mockResolvedValue({ data: {} });
  });

  it("ofrece como contrapartida hojas de cualquier tipo y solo cuentas bancarias activas", () => {
    renderFormulario();

    const contrapartida = screen.getByLabelText("Cuenta de contrapartida");
    expect(within(contrapartida).getByRole("option", { name: "5101 - Gastos bancarios" })).toBeInTheDocument();
    expect(within(contrapartida).getByRole("option", { name: "4201 - Intereses" })).toBeInTheDocument();
    expect(within(contrapartida).getByRole("option", { name: "1101 - Banco" })).toBeInTheDocument();
    expect(within(contrapartida).queryByRole("option", { name: "1 - Activos" })).toBeNull();
    expect(within(screen.getByLabelText("Cuenta bancaria")).queryByRole("option", { name: "Viejo 0" })).toBeNull();
  });

  it("registra un egreso con contrapartida, limpia el formulario y avisa", async () => {
    const onRegistrado = renderFormulario();
    fireEvent.change(screen.getByLabelText("Operación"), { target: { value: "Egreso" } });
    fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
    llenarComunes();
    fireEvent.change(screen.getByLabelText("Cuenta de contrapartida"), { target: { value: "5" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(onRegistrado).toHaveBeenCalledTimes(1));
    expect(movimientosTesoreriaApi.crear).toHaveBeenCalledWith({
      fecha: "2025-03-01",
      monto: 200,
      descripcion: "Comisión",
      referencia: null,
      cuentaBancariaId: 7,
      tipo: "Egreso",
      cuentaContrapartidaId: 5,
    });
    expect(screen.getByLabelText("Descripción del movimiento")).toHaveValue("");
    expect(screen.getByLabelText("Operación")).toHaveValue("Ingreso");
  });

  it("cambia a transferencia: muestra destino, oculta la contrapartida y llama a transferir", async () => {
    const onRegistrado = renderFormulario();
    fireEvent.change(screen.getByLabelText("Operación"), { target: { value: "Transferencia" } });

    expect(screen.queryByLabelText("Cuenta de contrapartida")).toBeNull();
    fireEvent.change(screen.getByLabelText("Cuenta de origen"), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText("Cuenta de destino"), { target: { value: "8" } });
    llenarComunes();
    fireEvent.change(screen.getByLabelText("Referencia del movimiento"), { target: { value: "REF-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(onRegistrado).toHaveBeenCalledTimes(1));
    expect(movimientosTesoreriaApi.transferir).toHaveBeenCalledWith({
      fecha: "2025-03-01",
      monto: 200,
      descripcion: "Comisión",
      referencia: "REF-1",
      cuentaOrigenId: 7,
      cuentaDestinoId: 8,
    });
    expect(movimientosTesoreriaApi.crear).not.toHaveBeenCalled();
  });

  it("pre-llena cuenta y fecha con el valor inicial", () => {
    renderFormulario({ inicial: { cuentaBancariaId: 8, fecha: "2025-02-15" } });

    expect(screen.getByLabelText("Cuenta bancaria")).toHaveValue("8");
    expect(screen.getByLabelText("Fecha del movimiento")).toHaveValue("2025-02-15");
  });

  it("muestra el error del servidor y no avisa como registrado", async () => {
    movimientosTesoreriaApi.crear.mockRejectedValue({ response: { data: { error: "Periodo cerrado" } } });
    const onRegistrado = renderFormulario();
    fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
    llenarComunes();
    fireEvent.change(screen.getByLabelText("Cuenta de contrapartida"), { target: { value: "6" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("Periodo cerrado")).toBeInTheDocument();
    expect(onRegistrado).not.toHaveBeenCalled();
  });

  it("usa un mensaje por defecto cuando el error no trae cuerpo", async () => {
    movimientosTesoreriaApi.crear.mockRejectedValue(new Error("red"));
    renderFormulario();
    fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
    llenarComunes();
    fireEvent.change(screen.getByLabelText("Cuenta de contrapartida"), { target: { value: "6" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("No se pudo registrar el movimiento.")).toBeInTheDocument();
  });
});
