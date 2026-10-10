import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AVISO_DTE } from "../../utils/fiscal";
import DatosDte from "./DatosDte";

const vacio = { dteUuid: "", dteSerie: "", dteNumero: "", dteFechaCertificacion: "" };

describe("DatosDte", () => {
  it("cuando es obligatorio titula la sección, exige los cuatro campos y muestra el aviso de no certificación", () => {
    render(<DatosDte valor={vacio} onCambio={vi.fn()} obligatorio />);

    expect(screen.getByText("Datos del DTE")).toBeInTheDocument();
    expect(screen.getByText(AVISO_DTE)).toBeInTheDocument();
    ["UUID de autorización", "Serie", "Número", "Fecha y hora de certificación"].forEach((etiqueta) =>
      expect(screen.getByLabelText(etiqueta)).toBeRequired()
    );
  });

  it("cuando es opcional cambia el título y no exige los campos", () => {
    render(<DatosDte valor={vacio} onCambio={vi.fn()} obligatorio={false} />);

    expect(screen.getByText("Datos del DTE del proveedor (obligatorios para tomar crédito fiscal)")).toBeInTheDocument();
    expect(screen.getByLabelText("Serie")).not.toBeRequired();
    expect(screen.getByText(AVISO_DTE)).toBeInTheDocument();
  });

  it("propaga el cambio de un campo conservando los demás", () => {
    const onCambio = vi.fn();
    render(<DatosDte valor={{ ...vacio, dteSerie: "A1" }} onCambio={onCambio} obligatorio />);

    fireEvent.change(screen.getByLabelText("Número"), { target: { value: "99" } });

    expect(onCambio).toHaveBeenCalledWith({ dteUuid: "", dteSerie: "A1", dteNumero: "99", dteFechaCertificacion: "" });
  });

  it("avisa cuando el UUID no tiene formato canónico y no avisa si es válido o está vacío", () => {
    const { rerender } = render(<DatosDte valor={{ ...vacio, dteUuid: "abc" }} onCambio={vi.fn()} obligatorio />);
    expect(screen.getByText("El UUID del DTE no tiene un formato válido.")).toBeInTheDocument();
    expect(screen.getByLabelText("UUID de autorización")).toHaveAttribute("aria-invalid", "true");

    rerender(<DatosDte valor={{ ...vacio, dteUuid: "3f2504e0-4f89-41d3-9a0c-0305e82c3301" }} onCambio={vi.fn()} obligatorio />);
    expect(screen.queryByText("El UUID del DTE no tiene un formato válido.")).toBeNull();

    rerender(<DatosDte valor={vacio} onCambio={vi.fn()} obligatorio />);
    expect(screen.queryByText("El UUID del DTE no tiene un formato válido.")).toBeNull();
  });
});
