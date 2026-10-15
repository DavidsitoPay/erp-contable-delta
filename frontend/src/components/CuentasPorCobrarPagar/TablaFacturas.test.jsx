import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoImporte, formatoMoneda, formatoQuetzales } from "../../utils/formato";
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

const facturasCxp = [
  { id: 11, numero: "P-1", proveedorNombre: "ACME", fecha: "2026-10-03", fechaVencimiento: "2026-11-03", montoBase: 40000, montoIva: 4800, montoTotal: 44800, montoRetencionIsr: 2200, saldoPendiente: 42600, dteSerie: null, dteNumero: null, calculoLegado: false, estado: "Vigente", revisionRetencion: "NINGUNA", revisionMotivo: null },
  { id: 12, numero: "P-2", proveedorNombre: "Sin régimen", fecha: "2026-10-04", fechaVencimiento: "2026-11-04", montoBase: 100, montoIva: 12, montoTotal: 112, montoRetencionIsr: 0, saldoPendiente: 112, dteSerie: null, dteNumero: null, calculoLegado: false, estado: "Vigente", revisionRetencion: "PENDIENTE", revisionMotivo: null },
  { id: 13, numero: "P-3", proveedorNombre: "Resuelta SA", fecha: "2026-10-05", fechaVencimiento: "2026-11-05", montoBase: 100, montoIva: 12, montoTotal: 112, montoRetencionIsr: 0, saldoPendiente: 112, dteSerie: null, dteNumero: null, calculoLegado: false, estado: "Vigente", revisionRetencion: "RESUELTA", revisionMotivo: "Régimen confirmado" },
];

describe("TablaFacturas con retención de ISR", () => {
  function renderCxp(onResolverRevision = vi.fn()) {
    render(<TablaFacturas facturas={facturasCxp} etiquetaContraparte="Proveedor" campoContraparteNombre="proveedorNombre" conRetencion onResolverRevision={onResolverRevision} />);
    return onResolverRevision;
  }

  it("agrega las columnas de retención y revisión y muestra la retención de cada factura", () => {
    renderCxp();

    expect(screen.getByRole("columnheader", { name: "Retención ISR" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Revisión" })).toBeInTheDocument();
    expect(within(screen.getByText("P-1").closest("tr")).getByText(/^2[.,]200[.,]00$/)).toBeInTheDocument();
  });

  it("marca la revisión pendiente con el botón para resolverla y muestra el motivo de la resuelta", () => {
    const onResolver = renderCxp();

    expect(within(screen.getByText("P-2").closest("tr")).getByText("Por revisar")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Resolver revisión de P-2" }));
    expect(onResolver).toHaveBeenCalledWith(facturasCxp[1]);
    expect(within(screen.getByText("P-3").closest("tr")).getByText("Resuelta: Régimen confirmado")).toBeInTheDocument();
    expect(within(screen.getByText("P-1").closest("tr")).getAllByText("—")).toHaveLength(2);
  });

  it("sin conRetencion no muestra esas columnas", () => {
    render(<TablaFacturas facturas={facturasCxp} etiquetaContraparte="Proveedor" campoContraparteNombre="proveedorNombre" />);

    expect(screen.queryByRole("columnheader", { name: "Retención ISR" })).toBeNull();
    expect(screen.queryByText("Por revisar")).toBeNull();
  });

  describe("facturas en moneda extranjera", () => {
    const facturaUsd = { id: 30, numero: "F-USD", clienteNombre: "Exportadora", fecha: "2026-10-12", fechaVencimiento: "2026-11-12", moneda: "USD", montoTotalOriginal: 1120, tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat", tipoCambioArrastrado: false, montoBase: 7600, montoIva: 912, montoTotal: 8512, saldoPendiente: 720, saldoPendienteGtq: 5472, dteSerie: null, dteNumero: null, calculoLegado: false, estado: "Vigente" };

    it("muestra la moneda, la tasa, el total original, el saldo y su equivalente en libros", () => {
      render(<TablaFacturas facturas={[facturaUsd]} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      const fila = within(screen.getByText("F-USD").closest("tr"));
      expect(fila.getByText("USD")).toBeInTheDocument();
      expect(fila.getByText("7.600000 (Banguat)")).toBeInTheDocument();
      expect(fila.getByText(formatoImporte(1120, "USD"))).toBeInTheDocument();
      expect(fila.getByText(formatoImporte(720, "USD"), { exact: false })).toBeInTheDocument();
      expect(fila.getByText(`${formatoQuetzales(5472)} en libros`)).toBeInTheDocument();
      expect(fila.getByText(formatoMoneda.format(8512))).toBeInTheDocument();
    });

    it("indica el arrastre de la tasa", () => {
      render(<TablaFacturas facturas={[{ ...facturaUsd, tipoCambioArrastrado: true }]} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      expect(screen.getByText("7.600000 (Banguat (arrastrado))")).toBeInTheDocument();
    });

    it("una factura en GTQ no muestra equivalente en libros ni total original", () => {
      render(<TablaFacturas facturas={facturas} etiquetaContraparte="Cliente" campoContraparteNombre="clienteNombre" />);

      expect(screen.queryByText(/en libros/)).toBeNull();
      expect(within(screen.getByText("F-1").closest("tr")).getAllByText("GTQ")).toHaveLength(1);
    });
  });
});
