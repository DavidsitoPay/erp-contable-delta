import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import ResumenRetenciones from "./ResumenRetenciones";

const totales = [
  { sentido: "EFECTUADA", concepto: "ISR_ROS", cantidad: 2, base: 80000, monto: 4400, sinConstancia: 2 },
  { sentido: "RECIBIDA", concepto: "IVA_CLIENTE", cantidad: 1, base: 1200, monto: 300, sinConstancia: 0 },
];
const conciliacionCuadrada = [{ concepto: "ISR_ROS", cuentaId: 90, cuentaCodigo: "2.1.5", totalReporte: 4400, movimientoContable: 4400, diferencia: 0 }];
const conciliacionConDiferencia = [{ concepto: "IVA_CLIENTE", cuentaId: 91, cuentaCodigo: "1.1.9", totalReporte: 300, movimientoContable: 0, diferencia: 300 }];

describe("ResumenRetenciones", () => {
  it("muestra los totales por impuesto y la conciliación sin avisos cuando cuadra", () => {
    render(<ResumenRetenciones reporte={{ filas: [], totales, conciliacion: conciliacionCuadrada }} />);

    const fila = within(screen.getAllByText("ISR a proveedores", { selector: "td" })[0].closest("tr"));
    expect(fila.getByText(formatoMoneda.format(80000))).toBeInTheDocument();
    expect(fila.getByText(formatoMoneda.format(4400))).toBeInTheDocument();
    expect(screen.getByText("2.1.5")).toBeInTheDocument();
    expect(screen.queryByText(/La diferencia se explica/)).toBeNull();
    expect(screen.queryByText(/no se explican/)).toBeNull();
  });

  it("explica la diferencia cuando hay cobros en otro mes", () => {
    render(<ResumenRetenciones reporte={{ filas: [{ periodoContableDistinto: true }], totales, conciliacion: conciliacionConDiferencia }} />);

    expect(screen.getByText("La diferencia se explica por cobros registrados en un mes distinto al de la factura.")).toBeInTheDocument();
  });

  it("advierte cuando la diferencia no se explica por cobros en otro mes", () => {
    render(<ResumenRetenciones reporte={{ filas: [{ periodoContableDistinto: false }], totales, conciliacion: conciliacionConDiferencia }} />);

    expect(screen.getByText(/no se explican por cobros en otro mes/)).toBeInTheDocument();
  });

  it("muestra los totales por tipo de renta de los no residentes", () => {
    const totalesTipoRenta = [{ tipoRentaCodigo: "SERV", tipoRentaNombre: "Servicios", cantidad: 2, base: 50000, monto: 7500 }];
    render(<ResumenRetenciones reporte={{ filas: [], totales, conciliacion: conciliacionCuadrada, totalesTipoRenta }} />);

    expect(screen.getByText("Totales por tipo de renta")).toBeInTheDocument();
    const fila = within(screen.getByText("Servicios").closest("tr"));
    expect(fila.getByText("2")).toBeInTheDocument();
    expect(fila.getByText(formatoMoneda.format(50000))).toBeInTheDocument();
    expect(fila.getByText(formatoMoneda.format(7500))).toBeInTheDocument();
  });

  it("omite los totales por tipo de renta cuando no hay", () => {
    render(<ResumenRetenciones reporte={{ filas: [], totales, conciliacion: conciliacionCuadrada, totalesTipoRenta: [] }} />);

    expect(screen.queryByText("Totales por tipo de renta")).toBeNull();
  });

  it("muestra el saldo por enterar de cada período", () => {
    const saldosEnterar = [
      { periodo: "2026-10-01", retenido: 2200, enterado: 1000, saldoPorEnterar: 1234 },
      { periodo: "2026-09-01", retenido: 500, enterado: 0, saldoPorEnterar: 500 },
      { periodo: "2026-08-01", retenido: 987, enterado: 987, saldoPorEnterar: 0 },
    ];
    render(<ResumenRetenciones reporte={{ filas: [], totales, conciliacion: conciliacionCuadrada, saldosEnterar }} />);

    expect(screen.getByText("Saldo por enterar por período")).toBeInTheDocument();
    expect(screen.getByText("2026-10")).toBeInTheDocument();
    expect(screen.getByText(formatoMoneda.format(1234))).toBeInTheDocument();
    expect(screen.getAllByText(formatoMoneda.format(987))).toHaveLength(2);
  });

  it("omite el saldo por enterar cuando el reporte no lo trae", () => {
    render(<ResumenRetenciones reporte={{ filas: [], totales, conciliacion: conciliacionCuadrada }} />);

    expect(screen.queryByText("Saldo por enterar por período")).toBeNull();
  });
});
