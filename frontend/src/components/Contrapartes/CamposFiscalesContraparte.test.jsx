import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formularioContraparteVacio } from "../../utils/formularioContraparte";
import CamposFiscalesContraparte from "./CamposFiscalesContraparte";

function renderCampos(form) {
  const onCambio = vi.fn();
  render(<CamposFiscalesContraparte form={form} onCambio={onCambio} />);
  return onCambio;
}

describe("CamposFiscalesContraparte", () => {
  it("el proveedor ve el régimen de ISR de cinco valores, la ayuda sin régimen y no ve el agente de IVA", () => {
    renderCampos(formularioContraparteVacio("Proveedor"));

    expect(screen.getByLabelText("Régimen de ISR")).toHaveValue("NO_DEFINIDO");
    expect(screen.getAllByRole("option").filter((o) => o.parentElement.getAttribute("aria-label") === "Régimen de ISR")).toHaveLength(5);
    expect(screen.getByText(/Sin régimen definido, las facturas se registran sin retención/)).toBeInTheDocument();
    expect(screen.queryByLabelText("Agente de retención de IVA")).toBeNull();
    expect(screen.queryByLabelText("Fundamento o número de resolución")).toBeNull();
  });

  it.each(["EXENTO", "PAGO_DIRECTO"])("el proveedor %s ve el fundamento obligatorio y no la ayuda", (regimenIsr) => {
    renderCampos({ ...formularioContraparteVacio("Proveedor"), regimenIsr });

    expect(screen.getByLabelText("Fundamento o número de resolución")).toBeRequired();
    expect(screen.queryByText(/Sin régimen definido/)).toBeNull();
  });

  it("el cliente no ve régimen de ISR y su tipo de agente aparece solo con la casilla marcada", () => {
    const cliente = formularioContraparteVacio("Cliente");
    const { rerender } = render(<CamposFiscalesContraparte form={cliente} onCambio={vi.fn()} />);

    expect(screen.queryByLabelText("Régimen de ISR")).toBeNull();
    expect(screen.queryByLabelText("Tipo de agente de IVA")).toBeNull();

    rerender(<CamposFiscalesContraparte form={{ ...cliente, esAgenteRetencionIva: true }} onCambio={vi.fn()} />);

    expect(screen.getByLabelText("Tipo de agente de IVA")).toBeRequired();
    expect(screen.getByRole("option", { name: "Agente privado" })).toBeInTheDocument();
  });

  it("notifica el formulario completo con el campo modificado", () => {
    const form = formularioContraparteVacio("Proveedor");
    const onCambio = renderCampos(form);

    fireEvent.change(screen.getByLabelText("Régimen de ISR"), { target: { value: "SIMPLIFICADO" } });
    fireEvent.click(screen.getByLabelText("Residente en Guatemala"));

    expect(onCambio).toHaveBeenNthCalledWith(1, { ...form, regimenIsr: "SIMPLIFICADO" });
    expect(onCambio).toHaveBeenNthCalledWith(2, { ...form, esResidente: false });
  });
});
