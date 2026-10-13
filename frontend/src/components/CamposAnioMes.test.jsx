import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import CamposAnioMes from "./CamposAnioMes";

describe("CamposAnioMes", () => {
  it("muestra el año y el mes recibidos con los doce meses como opciones", () => {
    render(<CamposAnioMes anio="2026" mes="10" onAnio={vi.fn()} onMes={vi.fn()} />);

    expect(screen.getByLabelText("Año")).toHaveValue(2026);
    expect(screen.getByLabelText("Mes")).toHaveValue("10");
    expect(screen.getAllByRole("option")).toHaveLength(12);
  });

  it("notifica los cambios de año y de mes como texto", () => {
    const onAnio = vi.fn();
    const onMes = vi.fn();
    render(<CamposAnioMes anio="2026" mes="10" onAnio={onAnio} onMes={onMes} />);

    fireEvent.change(screen.getByLabelText("Año"), { target: { value: "2027" } });
    fireEvent.change(screen.getByLabelText("Mes"), { target: { value: "3" } });

    expect(onAnio).toHaveBeenCalledWith("2027");
    expect(onMes).toHaveBeenCalledWith("3");
  });
});
