import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import TablaTiposCambio from "./TablaTiposCambio";

const filaUsada = { id: 1, fecha: "2026-10-12", monedaCodigo: "USD", tasa: 7.6, fuente: "Banguat", arrastrado: false, motivo: null, usuarioNombre: null, usada: true, activa: true };
const filaManual = { id: 2, fecha: "2026-10-10", monedaCodigo: "USD", tasa: 7.64136, fuente: "Manual", arrastrado: true, motivo: "Banguat caído", usuarioNombre: "Ana", usada: false, activa: true };
const filaInactiva = { id: 3, fecha: "2026-10-08", monedaCodigo: "USD", tasa: 7.6, fuente: "Manual", arrastrado: false, motivo: "Error", usuarioNombre: "Ana", usada: false, activa: false };

function renderTabla(filas) {
  const onBaja = vi.fn();
  const onSustituir = vi.fn();
  render(<TablaTiposCambio filas={filas} onBaja={onBaja} onSustituir={onSustituir} />);
  return { onBaja, onSustituir };
}

describe("TablaTiposCambio", () => {
  it("muestra la tasa a seis decimales, el motivo, el usuario y el chip de arrastre", () => {
    renderTabla([filaUsada, filaManual]);

    expect(screen.getByText("7.600000")).toBeInTheDocument();
    expect(screen.getByText("7.641360")).toBeInTheDocument();
    expect(screen.getByText("Banguat caído")).toBeInTheDocument();
    expect(screen.getByText("Ana")).toBeInTheDocument();
    expect(screen.getAllByText("Arrastrado")).toHaveLength(1);
  });

  it("una tasa usada no se puede dar de baja ni sustituir", () => {
    renderTabla([filaUsada]);

    expect(screen.getByRole("button", { name: "Dar de baja la tasa del 2026-10-12" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: /Sustituir por Banguat/ })).toBeNull();
    expect(screen.getByText("En uso")).toBeInTheDocument();
  });

  it("una tasa manual activa permite dar de baja y sustituir", () => {
    const { onBaja, onSustituir } = renderTabla([filaManual]);

    fireEvent.click(screen.getByRole("button", { name: "Dar de baja la tasa del 2026-10-10" }));
    fireEvent.click(screen.getByRole("button", { name: "Sustituir por Banguat la tasa del 2026-10-10" }));

    expect(onBaja).toHaveBeenCalledWith(filaManual);
    expect(onSustituir).toHaveBeenCalledWith(filaManual);
  });

  it("una tasa inactiva no permite acciones", () => {
    renderTabla([filaInactiva]);

    expect(screen.getByRole("button", { name: "Dar de baja la tasa del 2026-10-08" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: /Sustituir por Banguat/ })).toBeNull();
    expect(screen.getByText("Inactiva")).toBeInTheDocument();
  });

  it("sin filas muestra el mensaje de rango vacío", () => {
    renderTabla([]);

    expect(screen.getByText("No hay tipos de cambio en el rango seleccionado.")).toBeInTheDocument();
  });
});
