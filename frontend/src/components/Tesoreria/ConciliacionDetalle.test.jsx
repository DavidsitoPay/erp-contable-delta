import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { conciliacionesApi, movimientosTesoreriaApi } from "../../services/api";
import ConciliacionDetalle from "./ConciliacionDetalle";

vi.mock("../../services/api", () => ({
  conciliacionesApi: { obtener: vi.fn(), marcar: vi.fn(), desmarcar: vi.fn(), finalizar: vi.fn(), actualizar: vi.fn(), cancelar: vi.fn() },
  movimientosTesoreriaApi: { crear: vi.fn(), transferir: vi.fn() },
}));

const cuentasBancarias = [{ id: 7, banco: "BAC", numero: "123", activa: true }];
const cuentasContables = [{ id: 5, codigo: "5101", nombre: "Gastos bancarios", tipo: "Gasto", activa: true, cuentaPadreId: null }];

const movimientoBase = { cuentaBancariaId: 7, fecha: "2025-03-01", referencia: null, origen: "Manual", asientoId: 11 };
const marcados = [{ id: 1, tipo: "Ingreso", monto: 500, descripcion: "Depósito", ...movimientoBase }];
const disponibles = [{ id: 2, tipo: "Egreso", monto: 200, descripcion: "Cheque", ...movimientoBase }];

function detalle(resumen = {}, extra = {}) {
  return {
    data: {
      resumen: {
        id: 3,
        cuentaBancariaId: 7,
        periodoId: 1,
        fecha: "2025-03-31",
        estado: "Pendiente",
        saldoExtracto: 1300,
        saldoInicial: 1000,
        totalMarcado: 300,
        saldoConciliado: 1300,
        diferencia: 0,
        cantidadMovimientos: 1,
        ...resumen,
      },
      marcados,
      disponibles,
      ...extra,
    },
  };
}

async function renderDetalle() {
  const onCambio = vi.fn();
  render(<ConciliacionDetalle id={3} cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onCambio={onCambio} />);
  await screen.findByText(/Saldo inicial/);
  return onCambio;
}

describe("ConciliacionDetalle", () => {
  beforeEach(() => {
    Object.values(conciliacionesApi).forEach((fn) => fn.mockReset());
    Object.values(movimientosTesoreriaApi).forEach((fn) => fn.mockReset());
    conciliacionesApi.obtener.mockResolvedValue(detalle());
    [conciliacionesApi.marcar, conciliacionesApi.desmarcar, conciliacionesApi.finalizar, conciliacionesApi.actualizar, conciliacionesApi.cancelar].forEach((fn) =>
      fn.mockResolvedValue({ data: {} })
    );
    movimientosTesoreriaApi.crear.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("muestra el resumen, la diferencia en verde y los movimientos marcados y disponibles", async () => {
    await renderDetalle();

    expect(conciliacionesApi.obtener).toHaveBeenCalledWith(3);
    expect(screen.getByText(/Saldo inicial: 1[,.\s ]?000[.,]00/)).toBeInTheDocument();
    expect(screen.getByText(/Total marcado: 300[.,]00/)).toBeInTheDocument();
    expect(screen.getByText(/Saldo conciliado: 1[,.\s ]?300[.,]00/)).toBeInTheDocument();
    expect(screen.getByText(/Saldo del extracto: 1[,.\s ]?300[.,]00/)).toBeInTheDocument();
    expect(screen.getByText(/Diferencia: 0[.,]00/)).toHaveAttribute("data-balance", "ok");
    expect(screen.getByText("Depósito")).toBeInTheDocument();
    expect(screen.getByText("Cheque")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Finalizar conciliación" })).toBeEnabled();
  });

  it("deshabilita Finalizar y marca la diferencia en rojo cuando no es cero", async () => {
    conciliacionesApi.obtener.mockResolvedValue(detalle({ diferencia: 50 }));
    await renderDetalle();

    expect(screen.getByText(/Diferencia: 50[.,]00/)).toHaveAttribute("data-balance", "off");
    expect(screen.getByRole("button", { name: "Finalizar conciliación" })).toBeDisabled();
  });

  it("marca un movimiento disponible y refresca", async () => {
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Marcar movimiento 2" }));

    await waitFor(() => expect(conciliacionesApi.marcar).toHaveBeenCalledWith(3, 2));
    await waitFor(() => expect(conciliacionesApi.obtener).toHaveBeenCalledTimes(2));
  });

  it("desmarca un movimiento marcado y refresca", async () => {
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Desmarcar movimiento 1" }));

    await waitFor(() => expect(conciliacionesApi.desmarcar).toHaveBeenCalledWith(3, 1));
    await waitFor(() => expect(conciliacionesApi.obtener).toHaveBeenCalledTimes(2));
  });

  it("muestra el error 409 del servidor al marcar y el mensaje por defecto al desmarcar", async () => {
    conciliacionesApi.marcar.mockRejectedValue({ response: { data: { error: "El movimiento ya está incluido en una conciliación." } } });
    conciliacionesApi.desmarcar.mockRejectedValue(new Error("red"));
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Marcar movimiento 2" }));
    expect(await screen.findByText("El movimiento ya está incluido en una conciliación.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Desmarcar movimiento 1" }));
    expect(await screen.findByText("No se pudo desmarcar el movimiento.")).toBeInTheDocument();
  });

  it("finaliza la conciliación, refresca y avisa al padre", async () => {
    const onCambio = await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Finalizar conciliación" }));

    await waitFor(() => expect(conciliacionesApi.finalizar).toHaveBeenCalledWith(3));
    await waitFor(() => expect(onCambio).toHaveBeenCalledTimes(1));
    expect(conciliacionesApi.obtener).toHaveBeenCalledTimes(2);
  });

  it("muestra el 403 y el mensaje por defecto al finalizar", async () => {
    conciliacionesApi.finalizar.mockRejectedValueOnce({ response: { data: { error: "El usuario 4 no tiene perfil autorizado para finalizar una conciliación." } } });
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Finalizar conciliación" }));
    expect(await screen.findByText("El usuario 4 no tiene perfil autorizado para finalizar una conciliación.")).toBeInTheDocument();

    conciliacionesApi.finalizar.mockRejectedValueOnce(new Error("red"));
    fireEvent.click(screen.getByRole("button", { name: "Finalizar conciliación" }));
    expect(await screen.findByText("No se pudo finalizar la conciliación.")).toBeInTheDocument();
  });

  it.each(["Conciliado", "Cancelada"])("en estado %s es solo lectura", async (estado) => {
    conciliacionesApi.obtener.mockResolvedValue(detalle({ estado }, { disponibles: [] }));
    await renderDetalle();

    expect(screen.getByRole("button", { name: "Finalizar conciliación" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Registrar movimiento" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Cancelar conciliación" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Guardar cambios" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Desmarcar movimiento 1" })).toBeNull();
    expect(screen.queryByText("Movimientos disponibles")).toBeNull();
    expect(screen.getByText("Depósito")).toBeInTheDocument();
  });

  it("edita fecha de corte y saldo del extracto con los valores actuales como punto de partida", async () => {
    const onCambio = await renderDetalle();
    expect(screen.getByLabelText("Nueva fecha de corte")).toHaveValue("2025-03-31");
    expect(screen.getByLabelText("Nuevo saldo del extracto")).toHaveValue(1300);

    fireEvent.change(screen.getByLabelText("Nueva fecha de corte"), { target: { value: "2025-04-15" } });
    fireEvent.change(screen.getByLabelText("Nuevo saldo del extracto"), { target: { value: "1250.5" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));

    await waitFor(() => expect(conciliacionesApi.actualizar).toHaveBeenCalledWith(3, { fecha: "2025-04-15", saldoExtracto: 1250.5 }));
    await waitFor(() => expect(onCambio).toHaveBeenCalledTimes(1));
    expect(conciliacionesApi.obtener).toHaveBeenCalledTimes(2);
  });

  it("muestra el error al editar", async () => {
    conciliacionesApi.actualizar.mockRejectedValue({ response: { data: { error: "Hay movimientos marcados posteriores a la nueva fecha de corte; desmárcalos primero." } } });
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(await screen.findByText(/Hay movimientos marcados posteriores/)).toBeInTheDocument();
  });

  it("cancela la conciliación solo si se confirma y avisa al padre", async () => {
    vi.stubGlobal("confirm", vi.fn(() => false));
    const onCambio = await renderDetalle();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar conciliación" }));
    expect(conciliacionesApi.cancelar).not.toHaveBeenCalled();

    vi.stubGlobal("confirm", vi.fn(() => true));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar conciliación" }));

    await waitFor(() => expect(conciliacionesApi.cancelar).toHaveBeenCalledWith(3));
    await waitFor(() => expect(onCambio).toHaveBeenCalledTimes(1));
  });

  it("muestra el mensaje por defecto cuando falla la cancelación", async () => {
    vi.stubGlobal("confirm", vi.fn(() => true));
    conciliacionesApi.cancelar.mockRejectedValue(new Error("red"));
    await renderDetalle();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar conciliación" }));

    expect(await screen.findByText("No se pudo cancelar la conciliación.")).toBeInTheDocument();
  });

  it("Registrar movimiento abre el formulario pre-llenado, registra y recarga el detalle; se puede ocultar", async () => {
    await renderDetalle();
    expect(screen.queryByLabelText("Cuenta bancaria")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Registrar movimiento" }));

    expect(screen.getByLabelText("Cuenta bancaria")).toHaveValue("7");
    expect(screen.getByLabelText("Fecha del movimiento")).toHaveValue("2025-03-31");

    fireEvent.change(screen.getByLabelText("Monto del movimiento"), { target: { value: "15" } });
    fireEvent.change(screen.getByLabelText("Descripción del movimiento"), { target: { value: "Comisión" } });
    fireEvent.change(screen.getByLabelText("Cuenta de contrapartida"), { target: { value: "5" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(movimientosTesoreriaApi.crear).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(conciliacionesApi.obtener).toHaveBeenCalledTimes(2));

    fireEvent.click(screen.getByRole("button", { name: "Ocultar formulario de movimiento" }));
    expect(screen.queryByLabelText("Cuenta bancaria")).toBeNull();
  });

  it("muestra el error cuando la conciliación no carga", async () => {
    conciliacionesApi.obtener.mockRejectedValue({ response: { data: { error: "No existe." } } });
    render(<ConciliacionDetalle id={3} cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onCambio={vi.fn()} />);

    expect(await screen.findByText("No existe.")).toBeInTheDocument();
  });

  it("muestra el mensaje por defecto y el estado de carga cuando falla sin cuerpo", async () => {
    conciliacionesApi.obtener.mockRejectedValue(new Error("red"));
    render(<ConciliacionDetalle id={3} cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onCambio={vi.fn()} />);

    expect(screen.getByText("Cargando conciliación...")).toBeInTheDocument();
    expect(await screen.findByText("No se pudo cargar la conciliación.")).toBeInTheDocument();
  });
});
