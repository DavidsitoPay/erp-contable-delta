import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoDiferencial, formatoImporte, formatoMoneda, formatoQuetzales } from "../../utils/formato";
import TablaPagos from "./TablaPagos";

const pagos = [
  { id: 1, clienteNombre: "ACME", fecha: "2026-10-20", montoTotal: 11200, montoNeto: 10900, metodoPago: "Transferencia", aplicaciones: [{ numero: "F-1", montoAplicado: 11200, retencionIva: 300 }] },
  { id: 2, clienteNombre: "Otra", fecha: "2026-10-21", montoTotal: 50, metodoPago: "Efectivo", aplicaciones: [{ numero: "F-2", montoAplicado: 50 }] },
];

function renderTabla(conNeto) {
  render(<TablaPagos pagos={pagos} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" conNeto={conNeto} />);
}

describe("TablaPagos", () => {
  it("muestra la contraparte, el monto, el método y las aplicaciones con el IVA retenido", () => {
    renderTabla(false);

    const fila = within(screen.getByText("ACME").closest("tr"));
    expect(fila.getByText("Transferencia")).toBeInTheDocument();
    expect(fila.getByText(`F-1 (${formatoMoneda.format(11200)}, IVA retenido ${formatoMoneda.format(300)})`)).toBeInTheDocument();
    expect(within(screen.getByText("Otra").closest("tr")).getByText(`F-2 (${formatoMoneda.format(50)})`)).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Neto cobrado" })).toBeNull();
  });

  it("con conNeto agrega la columna del neto cobrado y usa el monto total si no hay neto", () => {
    renderTabla(true);

    expect(screen.getByRole("columnheader", { name: "Neto cobrado" })).toBeInTheDocument();
    expect(within(screen.getByText("ACME").closest("tr")).getByText(formatoMoneda.format(10900))).toBeInTheDocument();
    expect(within(screen.getByText("Otra").closest("tr")).getAllByText(formatoMoneda.format(50)).length).toBeGreaterThanOrEqual(2);
  });

  it("muestra un mensaje cuando no hay pagos", () => {
    render(<TablaPagos pagos={[]} etiquetaContraparte="Proveedor" campoContraparteNombre="proveedorNombre" />);

    expect(screen.getByText("No hay pagos registrados todavía.")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Proveedor" })).toBeInTheDocument();
  });

  describe("pagos en moneda extranjera", () => {
    const pagoUsd = { id: 5, clienteNombre: "Exportadora", fecha: "2026-11-10", moneda: "USD", montoTotal: 400, montoTotalGtq: 3080, metodoPago: "Transferencia", aplicaciones: [{ numero: "F-USD", montoAplicado: 400, montoAplicadoGtq: 3040, valorGtq: 3080, diferencialGtq: 40 }] };
    const pagoUsdPerdida = { ...pagoUsd, id: 6, aplicaciones: [{ ...pagoUsd.aplicaciones[0], diferencialGtq: -72 }] };

    it("muestra moneda, valor en quetzales, diferencial y la aplicación en moneda original", () => {
      render(<TablaPagos pagos={[pagoUsd]} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      const fila = within(screen.getByText("Exportadora").closest("tr"));
      expect(screen.getByRole("columnheader", { name: "Diferencial" })).toBeInTheDocument();
      expect(fila.getByText("USD")).toBeInTheDocument();
      expect(fila.getByText(formatoImporte(400, "USD"))).toBeInTheDocument();
      expect(fila.getByText(formatoQuetzales(3080))).toBeInTheDocument();
      expect(fila.getByText(formatoDiferencial(40))).toBeInTheDocument();
      expect(fila.getByText(`F-USD (${formatoImporte(400, "USD")})`)).toBeInTheDocument();
    });

    it("muestra la pérdida con signo negativo", () => {
      render(<TablaPagos pagos={[pagoUsdPerdida]} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      expect(screen.getByText("-Q72.00")).toBeInTheDocument();
    });

    it("con solo pagos en quetzales no muestra las columnas de moneda", () => {
      render(<TablaPagos pagos={pagos} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      expect(screen.queryByRole("columnheader", { name: "Diferencial" })).toBeNull();
      expect(screen.queryByRole("columnheader", { name: "Valor en Q" })).toBeNull();
    });
  });
});
