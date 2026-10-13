import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import CamposVigencia from "./CamposVigencia";

const form = { vigenteDesde: "2026-01-01", vigenteHasta: "" };

describe("CamposVigencia", () => {
  it("usa el prefijo en los ids y fija como mínimo de la fecha final la fecha inicial", () => {
    render(<CamposVigencia prefijo="par" form={form} cambiar={() => vi.fn()} />);

    expect(screen.getByLabelText("Vigente desde")).toHaveAttribute("id", "par-desde");
    expect(screen.getByLabelText("Vigente desde")).toBeEnabled();
    expect(screen.getByLabelText("Vigente hasta (opcional)")).toHaveAttribute("min", "2026-01-01");
  });

  it("bloquea la fecha inicial cuando se pide y notifica el cambio por campo", () => {
    const alCambiar = vi.fn();
    const cambiar = vi.fn(() => alCambiar);
    render(<CamposVigencia prefijo="imp" form={form} cambiar={cambiar} desdeBloqueado />);

    expect(screen.getByLabelText("Vigente desde")).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Vigente hasta (opcional)"), { target: { value: "2026-12-31" } });

    expect(cambiar).toHaveBeenCalledWith("vigenteHasta");
    expect(alCambiar).toHaveBeenCalledTimes(1);
  });
});
