import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi } from "../../services/api";
import CentrosCosto from "./CentrosCosto";

vi.mock("../../services/api", () => ({ centrosCostoApi: { listar: vi.fn(), crear: vi.fn(), desactivar: vi.fn() } }));

describe("CentrosCosto", () => {
  beforeEach(() => {
    Object.values(centrosCostoApi).forEach((fn) => fn.mockReset());
    centrosCostoApi.listar.mockResolvedValue({ data: [{ id: 1, codigo: "CC1", nombre: "Ventas" }] });
    centrosCostoApi.crear.mockResolvedValue({ data: {} });
    centrosCostoApi.desactivar.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lista los centros y crea uno nuevo", async () => {
    render(<CentrosCosto />);
    await screen.findByText("Ventas");

    fireEvent.change(screen.getByLabelText("Código"), { target: { value: "CC2" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Compras" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    await waitFor(() => expect(centrosCostoApi.crear).toHaveBeenCalledWith({ codigo: "CC2", nombre: "Compras" }));
    await waitFor(() => expect(centrosCostoApi.listar).toHaveBeenCalledTimes(2));
  });

  it("muestra el error del servidor al crear", async () => {
    centrosCostoApi.crear.mockRejectedValue({ response: { data: { error: "Código duplicado" } } });
    render(<CentrosCosto />);
    await screen.findByText("Ventas");

    fireEvent.change(screen.getByLabelText("Código"), { target: { value: "CC1" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Otro" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    expect(await screen.findByText("Código duplicado")).toBeInTheDocument();
  });

  it("desactiva un centro tras confirmar", async () => {
    vi.stubGlobal("confirm", vi.fn(() => true));
    render(<CentrosCosto />);
    await screen.findByText("Ventas");

    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(centrosCostoApi.desactivar).toHaveBeenCalledWith(1));
  });
});
