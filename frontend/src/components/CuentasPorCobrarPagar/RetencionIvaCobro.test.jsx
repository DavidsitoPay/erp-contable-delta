import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import { RETENCION_VACIA } from "../../utils/retenciones";
import RetencionIvaCobro from "./RetencionIvaCobro";

const sugerenciaBase = { aplica: true, motivo: null, ivaRetenible: 1200, sugerido: 300, yaRetenida: false };

function renderRetencion({ valor = RETENCION_VACIA, sugerencia = sugerenciaBase, clienteAgente = true, montoAplicado = 11200 } = {}) {
  const onCambio = vi.fn();
  render(
    <RetencionIvaCobro numeroFactura="F-1" montoAplicado={montoAplicado} valor={valor} sugerencia={sugerencia} clienteAgente={clienteAgente} onCambio={onCambio} />
  );
  return onCambio;
}

describe("RetencionIvaCobro", () => {
  it("si el cliente es agente la casilla inicia marcada y muestra monto, tope y constancia", () => {
    renderRetencion();

    expect(screen.getByLabelText("El cliente retuvo IVA en F-1")).toBeChecked();
    expect(screen.getByLabelText("IVA retenido en F-1")).toBeInTheDocument();
    expect(screen.getByText(`IVA retenible de la factura: ${formatoMoneda.format(1200)}`)).toBeInTheDocument();
    expect(screen.getByLabelText("Número de constancia de F-1")).toBeInTheDocument();
    expect(screen.queryByLabelText("Confirmo la retención de IVA en F-1 de un cliente no agente")).toBeNull();
  });

  it("si el cliente no es agente la casilla inicia sin marcar y al marcarla pide confirmación", () => {
    const onCambio = renderRetencion({ clienteAgente: false });
    expect(screen.getByLabelText("El cliente retuvo IVA en F-1")).not.toBeChecked();
    expect(screen.queryByLabelText("IVA retenido en F-1")).toBeNull();

    fireEvent.click(screen.getByLabelText("El cliente retuvo IVA en F-1"));
    expect(onCambio).toHaveBeenCalledWith({ activa: true });
  });

  it("un cliente no agente con la retención activa ve la casilla de confirmación y la notifica", () => {
    const onCambio = renderRetencion({ clienteAgente: false, valor: { ...RETENCION_VACIA, activa: true } });

    fireEvent.click(screen.getByLabelText("Confirmo la retención de IVA en F-1 de un cliente no agente"));

    expect(onCambio).toHaveBeenCalledWith({ confirma: true });
  });

  it("Sugerir completa el monto con el sugerido", () => {
    const onCambio = renderRetencion();
    fireEvent.click(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" }));
    expect(onCambio).toHaveBeenCalledWith({ monto: "300" });
  });

  it("sin sugerencia cargada el botón Sugerir está deshabilitado", () => {
    renderRetencion({ sugerencia: null });

    expect(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" })).toBeDisabled();
  });

  it("avisa cuando el monto digitado difiere del sugerido y no cuando coincide", () => {
    const { unmount } = render(
      <RetencionIvaCobro numeroFactura="F-1" montoAplicado={11200} valor={{ ...RETENCION_VACIA, monto: "250" }} sugerencia={sugerenciaBase} clienteAgente onCambio={vi.fn()} />
    );
    expect(screen.getByText(`El monto difiere del sugerido (${formatoMoneda.format(300)}); se registra el monto digitado.`)).toBeInTheDocument();
    unmount();

    renderRetencion({ valor: { ...RETENCION_VACIA, monto: "300" } });
    expect(screen.queryByText(/El monto difiere del sugerido/)).toBeNull();
  });

  it("muestra el error de validación cuando la retención excede el monto aplicado", () => {
    renderRetencion({ valor: { ...RETENCION_VACIA, monto: "20000" } });

    expect(screen.getByText("El IVA retenido no puede exceder el monto aplicado a la factura.")).toBeInTheDocument();
  });

  it("si la factura ya tiene IVA retenido lo informa y no ofrece captura", () => {
    renderRetencion({ sugerencia: { ...sugerenciaBase, yaRetenida: true } });

    expect(screen.getByText("La factura ya tiene IVA retenido registrado.")).toBeInTheDocument();
    expect(screen.queryByLabelText("IVA retenido en F-1")).toBeNull();
  });

  it("muestra el error de la consulta de la sugerencia y el motivo cuando no aplica", () => {
    const { unmount } = render(
      <RetencionIvaCobro numeroFactura="F-1" montoAplicado={100} valor={RETENCION_VACIA} sugerencia={{ error: "Factura no encontrada." }} clienteAgente onCambio={vi.fn()} />
    );
    expect(screen.getByText("Factura no encontrada.")).toBeInTheDocument();
    unmount();

    renderRetencion({ sugerencia: { ...sugerenciaBase, aplica: false, motivo: "El total de la factura no alcanza el umbral.", sugerido: 0 } });
    expect(screen.getByText("El total de la factura no alcanza el umbral.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" })).toBeDisabled();
  });

  it("notifica los cambios de monto y de constancia", () => {
    const onCambio = renderRetencion();

    fireEvent.change(screen.getByLabelText("IVA retenido en F-1"), { target: { value: "120" } });
    fireEvent.change(screen.getByLabelText("Número de constancia de F-1"), { target: { value: "A-1" } });
    fireEvent.change(screen.getByLabelText("Fecha de constancia de F-1"), { target: { value: "2026-10-20" } });

    expect(onCambio).toHaveBeenNthCalledWith(1, { monto: "120" });
    expect(onCambio).toHaveBeenNthCalledWith(2, { constanciaNumero: "A-1" });
    expect(onCambio).toHaveBeenNthCalledWith(3, { constanciaFecha: "2026-10-20" });
  });
});
