import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import TablaAplicaciones from "./TablaAplicaciones";

const facturas = [
  { id: 1, numero: "F-1", saldoPendiente: 11200 },
  { id: 2, numero: "F-2", saldoPendiente: 500 },
];

describe("TablaAplicaciones", () => {
  it("lista las facturas con su saldo y notifica el monto a aplicar", () => {
    const onMonto = vi.fn();
    render(<TablaAplicaciones facturas={facturas} montosAplicados={{}} onMonto={onMonto} retencionIva={null} />);

    expect(screen.getByText(formatoMoneda.format(11200))).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Monto a aplicar a F-2"), { target: { value: "40" } });

    expect(onMonto).toHaveBeenCalledWith(2, "40");
    expect(screen.queryByLabelText("El cliente retuvo IVA en F-1")).toBeNull();
  });

  it("muestra un mensaje cuando no hay facturas con saldo", () => {
    render(<TablaAplicaciones facturas={[]} montosAplicados={{}} onMonto={vi.fn()} retencionIva={null} />);

    expect(screen.getByText("No hay facturas con saldo pendiente para esta selección.")).toBeInTheDocument();
  });

  it("con retención de IVA ofrece la captura solo en las filas con monto aplicado y reenvía los cambios con el id", () => {
    const retencionIva = { valores: {}, sugerencias: {}, clienteAgente: true, onCambio: vi.fn() };
    render(<TablaAplicaciones facturas={facturas} montosAplicados={{ 1: "100" }} onMonto={vi.fn()} retencionIva={retencionIva} />);

    expect(screen.getByLabelText("El cliente retuvo IVA en F-1")).toBeChecked();
    expect(screen.queryByLabelText("El cliente retuvo IVA en F-2")).toBeNull();

    fireEvent.click(screen.getByLabelText("El cliente retuvo IVA en F-1"));

    expect(retencionIva.onCambio).toHaveBeenCalledWith(1, { activa: false });
  });
});
