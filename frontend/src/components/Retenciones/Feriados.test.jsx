import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { feriadosApi } from "../../services/api";
import Feriados from "./Feriados";

vi.mock("../../services/api", () => ({
  feriadosApi: { listar: vi.fn(), crear: vi.fn(), eliminar: vi.fn() },
}));

const ADMINISTRADOR = "Administrador del sistema";
const feriados = [
  { fecha: "2026-12-25", nombre: "Navidad", medioDia: false },
  { fecha: "2026-12-24", nombre: "Nochebuena", medioDia: true },
];

function setPerfil(perfil) {
  localStorage.setItem("delta_usuario", JSON.stringify({ perfil }));
}

function renderCon() {
  return render(<Feriados />);
}

describe("Feriados", () => {
  beforeEach(() => {
    localStorage.clear();
    setPerfil(ADMINISTRADOR);
    vi.mocked(feriadosApi.listar).mockResolvedValue({ data: feriados });
    vi.mocked(feriadosApi.crear).mockResolvedValue({ data: {} });
    vi.mocked(feriadosApi.eliminar).mockResolvedValue({ data: {} });
  });

  it("permite agregar y eliminar al Administrador", async () => {
    renderCon();

    expect(await screen.findByRole("button", { name: "Eliminar feriado 2026-12-25" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Agregar feriado" })).toBeInTheDocument();
  });

  it("muestra solo lectura a otros perfiles", async () => {
    setPerfil("Contador");
    renderCon();

    expect(await screen.findByText("Navidad")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Eliminar feriado 2026-12-25" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Agregar feriado" })).toBeNull();
    expect(screen.getByText("Medio día (hábil)")).toBeInTheDocument();
  });

  it("recarga al cambiar el año", async () => {
    renderCon();
    await screen.findByText("Navidad");
    fireEvent.change(screen.getByLabelText("Año"), { target: { value: "2027" } });

    await waitFor(() => expect(feriadosApi.listar).toHaveBeenLastCalledWith("2027"));
  });

  it("agrega el feriado con el nombre sin espacios y medio día", async () => {
    renderCon();
    await screen.findByText("Navidad");
    fireEvent.change(screen.getByLabelText("Fecha"), { target: { value: "2026-12-31" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "  Fin de año " } });
    fireEvent.click(screen.getByLabelText("Medio día (hábil)"));
    fireEvent.click(screen.getByRole("button", { name: "Agregar feriado" }));

    await waitFor(() => expect(feriadosApi.crear).toHaveBeenCalledWith({ fecha: "2026-12-31", nombre: "Fin de año", medioDia: true }));
  });

  it("muestra el error cuando el feriado ya existe", async () => {
    vi.mocked(feriadosApi.crear).mockRejectedValue({ response: { data: { error: "Ya existe un feriado en esa fecha." } } });
    renderCon();
    await screen.findByText("Navidad");
    fireEvent.change(screen.getByLabelText("Fecha"), { target: { value: "2026-12-25" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Navidad" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar feriado" }));

    expect(await screen.findByText("Ya existe un feriado en esa fecha.")).toBeInTheDocument();
  });

  it("elimina por fecha y muestra el error si falla", async () => {
    vi.mocked(feriadosApi.eliminar).mockRejectedValue({ response: { data: { error: "No se puede eliminar." } } });
    renderCon();
    fireEvent.click(await screen.findByRole("button", { name: "Eliminar feriado 2026-12-25" }));

    expect(feriadosApi.eliminar).toHaveBeenCalledWith("2026-12-25");
    expect(await screen.findByText("No se puede eliminar.")).toBeInTheDocument();
  });

  it("muestra el error si la carga falla", async () => {
    vi.mocked(feriadosApi.listar).mockRejectedValue(new Error("red"));
    renderCon();

    expect(await screen.findByText("No se pudo cargar los feriados.")).toBeInTheDocument();
  });
});
