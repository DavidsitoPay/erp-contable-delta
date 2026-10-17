import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoImporte, formatoMoneda, formatoQuetzales } from "../../utils/formato";
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

  describe("facturas en moneda extranjera", () => {
    const facturaUsd = { id: 3, numero: "F-USD", moneda: "USD", saldoPendiente: 720 };
    const facturaGtq = { id: 4, numero: "F-GTQ", saldoPendiente: 300 };
    const retencionVacia = { valores: {}, sugerencias: {}, clienteAgente: true, onCambio: vi.fn() };

    it("muestra el saldo en la moneda de la factura", () => {
      render(<TablaAplicaciones facturas={[facturaUsd]} montosAplicados={{}} onMonto={vi.fn()} />);

      expect(screen.getByText(formatoImporte(720, "USD"))).toBeInTheDocument();
    });

    it("pide el efectivo en quetzales solo para la factura extranjera", () => {
      const onEfectivo = vi.fn();
      const liquidacion = { pideEfectivo: true, efectivos: { 3: "5400" }, onEfectivo, tasaPago: null };
      render(<TablaAplicaciones facturas={[facturaUsd, facturaGtq]} montosAplicados={{ 3: "400" }} onMonto={vi.fn()} liquidacion={liquidacion} />);

      expect(screen.getByRole("columnheader", { name: "Efectivo en Q" })).toBeInTheDocument();
      const efectivo = screen.getByLabelText("Efectivo en Q de F-USD");
      expect(efectivo).toHaveValue(5400);
      expect(screen.queryByLabelText("Efectivo en Q de F-GTQ")).toBeNull();

      fireEvent.change(efectivo, { target: { value: "5000" } });

      expect(onEfectivo).toHaveBeenCalledWith(3, "5000");
    });

    it("muestra el valor en quetzales con la tasa del pago", () => {
      const liquidacion = { pideEfectivo: false, efectivos: {}, onEfectivo: vi.fn(), tasaPago: 7.7 };
      render(<TablaAplicaciones facturas={[facturaUsd]} montosAplicados={{ 3: "400" }} onMonto={vi.fn()} liquidacion={liquidacion} />);

      expect(screen.getByRole("columnheader", { name: "Valor en Q" })).toBeInTheDocument();
      expect(screen.getByText(formatoQuetzales(3080))).toBeInTheDocument();
    });

    it("no ofrece la sugerencia de IVA retenido para una factura en moneda extranjera", () => {
      render(<TablaAplicaciones facturas={[facturaUsd]} montosAplicados={{ 3: "400" }} onMonto={vi.fn()} retencionIva={retencionVacia} />);

      expect(screen.queryByText(/retuvo IVA en F-USD/)).toBeNull();
    });
  });
});
