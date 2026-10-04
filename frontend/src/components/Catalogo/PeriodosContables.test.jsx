import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { periodosApi } from "../../services/api";
import PeriodosContables from "./PeriodosContables";

vi.mock("../../services/api", () => ({ periodosApi: { listar: vi.fn(), crear: vi.fn(), cerrar: vi.fn(), reabrir: vi.fn() } }));

const periodos = [
  { id: 1, nombre: "Enero", fechaInicio: "2025-01-01", fechaFin: "2025-01-31", estado: "Abierto" },
  { id: 2, nombre: "Febrero", fechaInicio: "2025-02-01", fechaFin: "2025-02-28", estado: "Cerrado" },
];

describe("PeriodosContables", () => {
  beforeEach(() => {
    Object.values(periodosApi).forEach((fn) => fn.mockReset());
    periodosApi.listar.mockResolvedValue({ data: periodos });
    periodosApi.crear.mockResolvedValue({ data: {} });
    periodosApi.cerrar.mockResolvedValue({ data: {} });
    periodosApi.reabrir.mockResolvedValue({ data: {} });
    vi.stubGlobal("confirm", vi.fn(() => true));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lista periodos y crea uno nuevo", async () => {
    render(<PeriodosContables />);
    await screen.findByText("Enero");

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Marzo" } });
    fireEvent.change(screen.getByLabelText("Fecha de inicio"), { target: { value: "2025-03-01" } });
    fireEvent.change(screen.getByLabelText("Fecha de fin"), { target: { value: "2025-03-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    await waitFor(() =>
      expect(periodosApi.crear).toHaveBeenCalledWith({ nombre: "Marzo", fechaInicio: "2025-03-01", fechaFin: "2025-03-31" })
    );
  });

  it("cierra un periodo abierto y reabre uno cerrado", async () => {
    render(<PeriodosContables />);
    await screen.findByText("Enero");

    fireEvent.click(screen.getByRole("button", { name: "Cerrar" }));
    await waitFor(() => expect(periodosApi.cerrar).toHaveBeenCalledWith(1));

    fireEvent.click(screen.getByRole("button", { name: "Reabrir" }));
    await waitFor(() => expect(periodosApi.reabrir).toHaveBeenCalledWith(2));
  });

  it("muestra el error al fallar el cierre", async () => {
    periodosApi.cerrar.mockRejectedValue({ response: { data: { error: "Hay asientos pendientes" } } });
    render(<PeriodosContables />);
    await screen.findByText("Enero");

    fireEvent.click(screen.getByRole("button", { name: "Cerrar" }));

    expect(await screen.findByText("Hay asientos pendientes")).toBeInTheDocument();
  });
});
