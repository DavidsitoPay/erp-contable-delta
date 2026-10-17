import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoImporte, formatoQuetzales } from "../../utils/formato";
import TablaMovimientos from "./TablaMovimientos";

const cuentasPorId = { 7: { banco: "BAC", numero: "123" } };
const base = { cuentaBancariaId: 7, fecha: "2025-03-01", tipo: "Ingreso", monto: 500, descripcion: "Depósito", referencia: "R-1", origen: "Manual", asientoId: 11 };

describe("TablaMovimientos", () => {
  it("muestra los datos del movimiento y la cuenta, sin columna Conciliado ni acción", () => {
    render(<TablaMovimientos movimientos={[{ id: 1, ...base }]} cuentasPorId={cuentasPorId} />);

    const table = screen.getByRole("table");
    expect(within(table).getByText("BAC 123")).toBeInTheDocument();
    expect(within(table).getByText("Depósito")).toBeInTheDocument();
    expect(within(table).getByText(/500[.,]00/)).toBeInTheDocument();
    expect(screen.queryByText("Conciliado")).toBeNull();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("muestra la columna Conciliado con Sí y No cuando los movimientos traen el campo", () => {
    render(
      <TablaMovimientos
        movimientos={[{ id: 1, ...base, conciliado: true }, { id: 2, ...base, descripcion: "Otro", conciliado: false }]}
        cuentasPorId={cuentasPorId}
      />
    );

    const table = screen.getByRole("table");
    expect(within(table).getByText("Conciliado")).toBeInTheDocument();
    expect(within(table).getByText("Sí")).toBeInTheDocument();
    expect(within(table).getByText("No")).toBeInTheDocument();
  });

  it("renderiza el botón de acción por fila y entrega el movimiento al hacer click", () => {
    const onClick = vi.fn();
    const movimiento = { id: 3, ...base };
    render(<TablaMovimientos movimientos={[movimiento]} cuentasPorId={cuentasPorId} accion={{ etiqueta: "Marcar", onClick }} />);

    fireEvent.click(screen.getByRole("button", { name: "Marcar movimiento 3" }));

    expect(onClick).toHaveBeenCalledWith(movimiento);
  });

  it("indica cuando no hay movimientos", () => {
    render(<TablaMovimientos movimientos={[]} cuentasPorId={cuentasPorId} />);

    expect(screen.getByText("No hay movimientos.")).toBeInTheDocument();
  });

  describe("cuentas en moneda extranjera", () => {
    const cuentasPorId = { 10: { id: 10, banco: "BAC", numero: "USD-1", monedaCodigo: "USD" }, 7: { id: 7, banco: "BAC", numero: "123" } };
    const movimientoUsd = { id: 1, cuentaBancariaId: 10, fecha: "2026-11-10", tipo: "Ingreso", monto: 400, montoGtq: 3080, tipoCambio: 7.7, descripcion: "Cobro", referencia: null, origen: "Manual", asientoId: 3 };
    const movimientoGtq = { id: 2, cuentaBancariaId: 7, fecha: "2026-11-11", tipo: "Egreso", monto: 100, descripcion: "Comisión", referencia: null, origen: "Manual", asientoId: 4 };

    it("muestra el monto original, el valor en quetzales y el tipo de cambio", () => {
      render(<TablaMovimientos movimientos={[movimientoUsd]} cuentasPorId={cuentasPorId} />);

      expect(screen.getByRole("columnheader", { name: "Valor en Q" })).toBeInTheDocument();
      expect(screen.getByRole("columnheader", { name: "Tipo de cambio" })).toBeInTheDocument();
      expect(screen.getByText(formatoImporte(400, "USD"))).toBeInTheDocument();
      expect(screen.getByText(formatoQuetzales(3080))).toBeInTheDocument();
      expect(screen.getByText("7.700000")).toBeInTheDocument();
      expect(screen.getByText("BAC USD-1 (USD)")).toBeInTheDocument();
    });

    it("con cuentas solo en quetzales no muestra las columnas de moneda", () => {
      render(<TablaMovimientos movimientos={[movimientoGtq]} cuentasPorId={cuentasPorId} />);

      expect(screen.queryByRole("columnheader", { name: "Valor en Q" })).toBeNull();
    });

    it("en una lista mixta deja vacías las celdas de moneda de la cuenta en quetzales", () => {
      render(<TablaMovimientos movimientos={[movimientoUsd, movimientoGtq]} cuentasPorId={cuentasPorId} />);

      const filaGtq = within(screen.getByText("Comisión").closest("tr"));
      expect(filaGtq.queryByText(/^Q/)).toBeNull();
      expect(screen.getAllByText("7.700000")).toHaveLength(1);
    });
  });

});
