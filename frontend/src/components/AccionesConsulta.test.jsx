import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BotonesConsulta, EstadoConsulta } from "./AccionesConsulta";

describe("BotonesConsulta", () => {
  it("deshabilita Consultar mientras carga", () => {
    render(<BotonesConsulta cargando mostrarExportar={false} onExportar={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Consultar" })).toBeDisabled();
  });

  it("muestra Exportar CSV solo cuando mostrarExportar es verdadero", () => {
    const { rerender } = render(<BotonesConsulta cargando={false} mostrarExportar={false} onExportar={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Exportar CSV" })).toBeNull();

    rerender(<BotonesConsulta cargando={false} mostrarExportar onExportar={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Exportar CSV" })).toBeInTheDocument();
  });

  it("llama onExportar al hacer clic en Exportar CSV", () => {
    const onExportar = vi.fn();
    render(<BotonesConsulta cargando={false} mostrarExportar onExportar={onExportar} />);

    fireEvent.click(screen.getByRole("button", { name: "Exportar CSV" }));

    expect(onExportar).toHaveBeenCalledTimes(1);
  });
});

describe("EstadoConsulta", () => {
  it("muestra el error recibido", () => {
    render(<EstadoConsulta error="Fallo al consultar" cargando={false} />);

    expect(screen.getByText("Fallo al consultar")).toHaveClass("error-chip");
  });

  it("muestra Cargando mientras la consulta está en curso", () => {
    render(<EstadoConsulta error="" cargando />);

    expect(screen.getByText("Cargando...")).toBeInTheDocument();
  });
});
