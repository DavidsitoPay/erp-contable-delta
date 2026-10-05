import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import TablaMovimientos from "./TablaMovimientos";

const cuentasPorId = { 7: { banco: "BAC", numero: "123" } };
const base = { cuentaBancariaId: 7, fecha: "2025-03-01", tipo: "Ingreso", monto: 500, descripcion: "Depósito", referencia: "R-1", origen: "Manual", asientoId: 11 };

describe("TablaMovimientos", () => {
  it("muestra los datos del movimiento y la cuenta, sin columna Conciliado ni acción", () => {
    render(<TablaMovimientos movimientos={[{ id: 1, ...base }]} cuentasPorId={cuentasPorId} />);

    expect(screen.getByText("BAC 123")).toBeInTheDocument();
    expect(screen.getByText("Depósito")).toBeInTheDocument();
    expect(screen.getByText(/500[.,]00/)).toBeInTheDocument();
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

    expect(screen.getByText("Conciliado")).toBeInTheDocument();
    expect(screen.getByText("Sí")).toBeInTheDocument();
    expect(screen.getByText("No")).toBeInTheDocument();
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
});
