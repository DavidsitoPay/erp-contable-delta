import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import CasillaInactivos from "./CasillaInactivos";

describe("CasillaInactivos", () => {
  it("refleja el estado recibido y notifica el nuevo valor al marcarla", () => {
    const onCambio = vi.fn();
    render(<CasillaInactivos marcada={false} onCambio={onCambio} />);

    const casilla = screen.getByLabelText("Incluir inactivos");
    expect(casilla).not.toBeChecked();
    fireEvent.click(casilla);

    expect(onCambio).toHaveBeenCalledWith(true);
  });
});
