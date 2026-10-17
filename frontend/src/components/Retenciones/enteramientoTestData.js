import { fireEvent, screen } from "@testing-library/react";

export function confirmarAnulacion(boleta, motivo) {
  fireEvent.change(screen.getByLabelText(`Motivo de anulación del entero ${boleta}`), { target: { value: motivo } });
  fireEvent.click(screen.getByRole("button", { name: "Anular enteramiento" }));
}
