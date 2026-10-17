import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import TiposCambio from "./TiposCambio";

vi.mock("../../services/api", () => ({
  tiposCambioApi: { historial: vi.fn(), darDeBaja: vi.fn(), sustituirPorBanguat: vi.fn(), capturarManual: vi.fn() },
}));

const filas = [
  { id: 1, fecha: "2026-10-12", monedaCodigo: "USD", tasa: 7.6, fuente: "Banguat", arrastrado: false, motivo: null, usuarioNombre: null, usada: true, activa: true },
  { id: 2, fecha: "2026-10-10", monedaCodigo: "USD", tasa: 7.64136, fuente: "Manual", arrastrado: true, motivo: "Banguat caído", usuarioNombre: "Ana", usada: false, activa: true },
];

function historialCon(datos) {
  tiposCambioApi.historial.mockResolvedValue({ data: datos });
}

describe("TiposCambio", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    historialCon(filas);
  });

  it("carga el historial de USD al montar", async () => {
    render(<TiposCambio />);

    expect(await screen.findByText("7.641360")).toBeInTheDocument();
    expect(tiposCambioApi.historial).toHaveBeenCalledWith({ moneda: "USD" });
  });

  it("aplica moneda, rango e inactivos a la consulta", async () => {
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.change(screen.getByRole("combobox", { name: "Filtrar por moneda" }), { target: { value: "EUR" } });
    fireEvent.change(screen.getByLabelText("Desde"), { target: { value: "2026-10-01" } });
    fireEvent.click(screen.getByLabelText("Incluir inactivos"));

    await waitFor(() => expect(tiposCambioApi.historial).toHaveBeenLastCalledWith({ moneda: "EUR", desde: "2026-10-01", incluirInactivas: true }));
  });

  it("muestra el error de carga", async () => {
    tiposCambioApi.historial.mockRejectedValue({ response: { data: { error: "Servicio no disponible." } } });
    render(<TiposCambio />);

    expect(await screen.findByText("Servicio no disponible.")).toBeInTheDocument();
  });

  it("captura una tasa manual y recarga el historial", async () => {
    tiposCambioApi.capturarManual.mockResolvedValue({});
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.click(screen.getByRole("button", { name: "Capturar tasa manual" }));
    fireEvent.change(screen.getByLabelText("Tasa de cambio (GTQ por unidad)"), { target: { value: "7.5" } });
    fireEvent.change(screen.getByLabelText("Motivo de la tasa manual"), { target: { value: "Banguat caído" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar tasa manual" }));

    await waitFor(() => expect(tiposCambioApi.historial).toHaveBeenCalledTimes(2));
    expect(tiposCambioApi.capturarManual).toHaveBeenCalledWith(expect.objectContaining({ monedaCodigo: "USD", tasa: 7.5, motivo: "Banguat caído" }));
    expect(screen.queryByLabelText("Motivo de la tasa manual")).toBeNull();
  });

  it("da de baja una tasa con motivo y recarga", async () => {
    tiposCambioApi.darDeBaja.mockResolvedValue({});
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.click(screen.getByRole("button", { name: "Dar de baja la tasa del 2026-10-10" }));
    fireEvent.change(screen.getByLabelText("Motivo de la baja de la tasa del 2026-10-10"), { target: { value: "motivo" } });
    fireEvent.click(screen.getByRole("button", { name: "Dar de baja" }));

    await waitFor(() => expect(tiposCambioApi.darDeBaja).toHaveBeenCalledWith(2, "motivo"));
    await waitFor(() => expect(tiposCambioApi.historial).toHaveBeenCalledTimes(2));
  });

  it("muestra el error de baja dentro del formulario de motivo", async () => {
    tiposCambioApi.darDeBaja.mockRejectedValue({ response: { data: { error: "La tasa ya fue usada." } } });
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.click(screen.getByRole("button", { name: "Dar de baja la tasa del 2026-10-10" }));
    fireEvent.change(screen.getByLabelText("Motivo de la baja de la tasa del 2026-10-10"), { target: { value: "motivo" } });
    fireEvent.click(screen.getByRole("button", { name: "Dar de baja" }));

    expect(await screen.findByText("La tasa ya fue usada.")).toBeInTheDocument();
  });

  it("sustituye una tasa manual por la de Banguat y recarga", async () => {
    tiposCambioApi.sustituirPorBanguat.mockResolvedValue({});
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.click(screen.getByRole("button", { name: "Sustituir por Banguat la tasa del 2026-10-10" }));

    await waitFor(() => expect(tiposCambioApi.historial).toHaveBeenCalledTimes(2));
    expect(tiposCambioApi.sustituirPorBanguat).toHaveBeenCalledWith({ monedaCodigo: "USD", fecha: "2026-10-10" });
  });

  it("muestra el error si la sustitución por Banguat falla", async () => {
    tiposCambioApi.sustituirPorBanguat.mockRejectedValue({ response: { data: { error: "Banguat no respondió" } } });
    render(<TiposCambio />);
    await screen.findByText("7.641360");

    fireEvent.click(screen.getByRole("button", { name: "Sustituir por Banguat la tasa del 2026-10-10" }));

    expect(await screen.findByText("Banguat no respondió")).toBeInTheDocument();
  });
});
