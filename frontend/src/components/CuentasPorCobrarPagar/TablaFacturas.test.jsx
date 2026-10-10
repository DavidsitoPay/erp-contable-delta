import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import TablaFacturas from "./TablaFacturas";

const facturas = [
  { id: 1, numero: "F-1", clienteNombre: "ACME", fecha: "2026-10-03", fechaVencimiento: "2026-11-03", montoBase: 178.57, montoIva: 21.43, montoTotal: 200, saldoPendiente: 150, dteSerie: "A1B2", dteNumero: "123", calculoLegado: false, estado: "Vigente" },
  { id: 2, numero: "F-0", clienteNombre: "Otra", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoBase: 100, montoIva: 12, montoTotal: 112, saldoPendiente: 0, dteSerie: null, dteNumero: null, calculoLegado: true, estado: "Vigente" },
];

describe("TablaFacturas", () => {
  it("muestra base, IVA, total, saldo y el DTE como serie-número", () => {
    render(<TablaFacturas facturas={facturas} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

    const fila = within(screen.getByText("F-1").closest("tr"));
    expect(fila.getByText("ACME")).toBeInTheDocument();
    expect(fila.getByText(/^178[.,]57$/)).toBeInTheDocument();
    expect(fila.getByText(/^21[.,]43$/)).toBeInTheDocument();
    expect(fila.getByText(/^200[.,]00$/)).toBeInTheDocument();
    expect(fila.getByText(/^150[.,]00$/)).toBeInTheDocument();
    expect(fila.getByText("A1B2-123")).toBeInTheDocument();
    expect(fila.queryByText("(legado)")).toBeNull();
    expect(screen.getByRole("columnheader", { name: "Cliente" })).toBeInTheDocument();
  });

  it("marca como (legado) los documentos con cálculo anterior a la configuración fiscal y muestra guion sin DTE", () => {
    render(<TablaFacturas facturas={facturas} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

    const fila = within(screen.getByText("F-0").closest("tr"));
    expect(fila.getByText("(legado)")).toBeInTheDocument();
    expect(fila.getByText("—")).toBeInTheDocument();
  });

  it("muestra un mensaje cuando no hay facturas", () => {
    render(<TablaFacturas facturas={[]} etiquetaContraparte="Proveedor" campoContraparteNombre="proveedorNombre" />);

    expect(screen.getByText("No hay facturas registradas todavía.")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Proveedor" })).toBeInTheDocument();
  });
});
