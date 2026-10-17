import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import TablaRetenciones from "./TablaRetenciones";

function fila(cambios) {
  return {
    retencionId: 7,
    sentido: "EFECTUADA",
    concepto: "ISR_ROS",
    nit: "1234567-8",
    nombre: "Proveedor Uno",
    documentoNumero: "F-001",
    fechaDocumento: "2026-10-05",
    fechaOperacion: "2026-10-05",
    base: 40000,
    tasa: 5,
    tasaEfectiva: 5.5,
    monto: 2200,
    constanciaId: null,
    constanciaNumero: null,
    constanciaFecha: null,
    estadoConstancia: "PENDIENTE",
    periodoContableDistinto: false,
    ...cambios,
  };
}

const filas = [
  fila(),
  fila({ retencionId: 8, nombre: "Proveedor Dos", documentoNumero: "F-002", estadoConstancia: "VENCIDA" }),
  fila({ retencionId: 9, sentido: "RECIBIDA", concepto: "IVA_CLIENTE", nombre: "Cliente Uno", documentoNumero: "F-003", tasa: 25, tasaEfectiva: undefined, constanciaId: 3, constanciaNumero: "A-123", constanciaFecha: "2026-11-02", estadoConstancia: "EMITIDA", periodoContableDistinto: true }),
  fila({ retencionId: 10, nombre: "Proveedor Tres", documentoNumero: "F-004", estadoConstancia: "ANULADA" }),
];

describe("TablaRetenciones", () => {
  it("muestra sentido, impuesto, importes, constancia y estado de cada retención", () => {
    render(<TablaRetenciones filas={filas} onAccion={vi.fn()} />);

    const filaIsr = within(screen.getByText("Proveedor Uno").closest("tr"));
    expect(filaIsr.getByText("Efectuada")).toBeInTheDocument();
    expect(filaIsr.getByText("ISR a proveedores")).toBeInTheDocument();
    expect(filaIsr.getByText(formatoMoneda.format(40000))).toBeInTheDocument();
    expect(filaIsr.getByText("5.5")).toBeInTheDocument();
    expect(filaIsr.getByText(formatoMoneda.format(2200))).toBeInTheDocument();
    expect(filaIsr.getByText("Pendiente")).toHaveClass("badge-warning");

    const filaIva = within(screen.getByText("Cliente Uno").closest("tr"));
    expect(filaIva.getByText("Recibida")).toBeInTheDocument();
    expect(filaIva.getByText("25")).toBeInTheDocument();
    expect(filaIva.getByText("A-123")).toBeInTheDocument();
    expect(filaIva.getByText("Cobro en otro mes")).toBeInTheDocument();
    expect(filaIva.getByText("Emitida")).toHaveClass("badge-ok");
  });

  it("resalta las filas pendientes y vencidas, no las emitidas ni las anuladas", () => {
    render(<TablaRetenciones filas={filas} onAccion={vi.fn()} />);

    expect(screen.getByText("Proveedor Uno").closest("tr")).toHaveClass("row-alerta");
    expect(screen.getByText("Proveedor Dos").closest("tr")).toHaveClass("row-alerta");
    expect(within(screen.getByText("Proveedor Dos").closest("tr")).getByText("Vencida")).toBeInTheDocument();
    expect(screen.getByText("Cliente Uno").closest("tr")).not.toHaveClass("row-alerta");
    expect(screen.getByText("Proveedor Tres").closest("tr")).not.toHaveClass("row-alerta");
  });

  it("ofrece registrar la constancia salvo en las emitidas, que se anulan", () => {
    const onAccion = vi.fn();
    render(<TablaRetenciones filas={filas} onAccion={onAccion} />);

    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia de F-001" }));
    fireEvent.click(screen.getByRole("button", { name: "Anular constancia de F-003" }));
    expect(screen.getByRole("button", { name: "Registrar constancia de F-004" })).toBeInTheDocument();

    expect(onAccion).toHaveBeenNthCalledWith(1, { tipo: "registrar", fila: filas[0] });
    expect(onAccion).toHaveBeenNthCalledWith(2, { tipo: "anular", fila: filas[2] });
  });

  it("muestra el país y el ajuste pendiente con su acción", () => {
    const onAccion = vi.fn();
    render(<TablaRetenciones filas={[fila({ pais: "US", ajustePendiente: true })]} onAccion={onAccion} />);

    expect(within(screen.getByText("Proveedor Uno").closest("tr")).getByText("US")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Resolver ajuste de F-001" }));

    expect(onAccion).toHaveBeenCalledWith({ tipo: "ajuste", fila: expect.objectContaining({ retencionId: 7 }) });
  });

  it("muestra un mensaje cuando no hay retenciones", () => {
    render(<TablaRetenciones filas={[]} onAccion={vi.fn()} />);

    expect(screen.getByText("No hay retenciones en el período seleccionado.")).toBeInTheDocument();
  });

  it("muestra tipo de renta, monto de origen y tipo de cambio, y no ofrece constancia a los no residentes", () => {
    const noResidente = fila({
      retencionId: 11,
      concepto: "ISR_NORES",
      nombre: "Proveedor Ext",
      documentoNumero: "F-005",
      tipoRentaNombre: "Servicios",
      montoOrigen: 1000,
      monedaCodigo: "USD",
      tipoCambio: 7.8,
      tipoCambioFecha: "2026-10-05",
      estadoConstancia: "NO_APLICA",
    });
    render(<TablaRetenciones filas={[noResidente]} onAccion={vi.fn()} />);

    const filaNr = within(screen.getByText("Proveedor Ext").closest("tr"));
    expect(filaNr.getByText("ISR a no residentes")).toBeInTheDocument();
    expect(filaNr.getByText("Servicios")).toBeInTheDocument();
    expect(filaNr.getByText(`${formatoMoneda.format(1000)} USD`)).toBeInTheDocument();
    expect(filaNr.getByText("7.8 (2026-10-05)")).toBeInTheDocument();
    expect(filaNr.getByText("No aplica")).toBeInTheDocument();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("muestra guiones en tipo de renta, monto de origen y tipo de cambio cuando no aplican", () => {
    render(<TablaRetenciones filas={[filas[0]]} onAccion={vi.fn()} />);

    expect(within(screen.getByText("Proveedor Uno").closest("tr")).getAllByText("—")).toHaveLength(5);
  });
});
