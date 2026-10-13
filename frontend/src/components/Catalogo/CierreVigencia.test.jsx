import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import CierreVigencia from "./CierreVigencia";

function renderCierre(cambios = {}) {
  const props = {
    id: "x-cierre",
    cierre: { item: { nombre: "IVA general", vigenteDesde: "2026-01-01" }, fecha: "2026-06-30" },
    cargando: false,
    onCambio: vi.fn(),
    onConfirmar: vi.fn((e) => e.preventDefault()),
    ...cambios,
  };
  render(<CierreVigencia {...props} />);
  return props;
}

describe("CierreVigencia", () => {
  it("pide la fecha final con la fecha inicial como mínimo y reporta los cambios", () => {
    const props = renderCierre();
    const campo = screen.getByLabelText("Fecha final de vigencia de IVA general");

    expect(campo).toHaveAttribute("min", "2026-01-01");
    fireEvent.change(campo, { target: { value: "2026-07-01" } });

    expect(props.onCambio).toHaveBeenCalledWith({ ...props.cierre, fecha: "2026-07-01" });
  });

  it("confirma y cancela con sus botones", () => {
    const props = renderCierre();

    fireEvent.click(screen.getByRole("button", { name: "Confirmar cierre" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar cierre" }));

    expect(props.onConfirmar).toHaveBeenCalledTimes(1);
    expect(props.onCambio).toHaveBeenCalledWith(null);
  });

  it("deshabilita la confirmación mientras guarda", () => {
    renderCierre({ cargando: true });

    expect(screen.getByRole("button", { name: "Confirmar cierre" })).toBeDisabled();
  });
});
