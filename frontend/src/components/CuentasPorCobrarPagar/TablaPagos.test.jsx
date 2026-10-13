import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoMoneda } from "../../utils/formato";
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
});
