import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import { hoyIso } from "../../utils/formato";
import FormularioTasaManual from "./FormularioTasaManual";

vi.mock("../../services/api", () => ({ tiposCambioApi: { capturarManual: vi.fn() } }));

function llenar({ tasa = "7.64136", motivo = " Banguat caído ", fecha = "2026-10-12" } = {}) {
  fireEvent.change(screen.getByLabelText("Fecha de la tasa"), { target: { value: fecha } });
  fireEvent.change(screen.getByLabelText("Tasa de cambio (GTQ por unidad)"), { target: { value: tasa } });
  fireEvent.change(screen.getByLabelText("Motivo de la tasa manual"), { target: { value: motivo } });
}

describe("FormularioTasaManual", () => {
  beforeEach(() => {
    tiposCambioApi.capturarManual.mockReset();
  });

  it("precarga los valores recibidos y usa hoy y USD por defecto", () => {
    const { unmount } = render(<FormularioTasaManual monedaInicial="EUR" fechaInicial="2026-10-09" tasaInicial={8.55} onCapturada={vi.fn()} onCancelar={vi.fn()} />);

    expect(screen.getByRole("combobox", { name: "Moneda de la tasa" })).toHaveValue("EUR");
    expect(screen.getByLabelText("Fecha de la tasa")).toHaveValue("2026-10-09");
    expect(screen.getByLabelText("Tasa de cambio (GTQ por unidad)")).toHaveValue(8.55);
    unmount();

    render(<FormularioTasaManual onCapturada={vi.fn()} onCancelar={vi.fn()} />);
    expect(screen.getByRole("combobox", { name: "Moneda de la tasa" })).toHaveValue("USD");
    expect(screen.getByLabelText("Fecha de la tasa")).toHaveValue(hoyIso());
  });

  it("solo habilita guardar con tasa válida y motivo", () => {
    render(<FormularioTasaManual onCapturada={vi.fn()} onCancelar={vi.fn()} />);
    const guardar = screen.getByRole("button", { name: "Guardar tasa manual" });
    expect(guardar).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Tasa de cambio (GTQ por unidad)"), { target: { value: "7.1234567" } });
    fireEvent.change(screen.getByLabelText("Motivo de la tasa manual"), { target: { value: "Motivo" } });
    expect(guardar).toBeDisabled();
  });

  it("envía la captura con el motivo recortado y notifica", async () => {
    tiposCambioApi.capturarManual.mockResolvedValue({});
    const onCapturada = vi.fn();
    render(<FormularioTasaManual onCapturada={onCapturada} onCancelar={vi.fn()} />);

    llenar();
    fireEvent.click(screen.getByRole("button", { name: "Guardar tasa manual" }));

    await waitFor(() => expect(onCapturada).toHaveBeenCalledTimes(1));
    expect(tiposCambioApi.capturarManual).toHaveBeenCalledWith({ monedaCodigo: "USD", fecha: "2026-10-12", tasa: 7.64136, motivo: "Banguat caído" });
  });

  it("muestra el error del servidor y no notifica", async () => {
    tiposCambioApi.capturarManual.mockRejectedValue({ response: { data: { error: "Ya existe el tipo de cambio oficial de Banguat para esa fecha." } } });
    const onCapturada = vi.fn();
    render(<FormularioTasaManual onCapturada={onCapturada} onCancelar={vi.fn()} />);

    llenar();
    fireEvent.click(screen.getByRole("button", { name: "Guardar tasa manual" }));

    expect(await screen.findByText("Ya existe el tipo de cambio oficial de Banguat para esa fecha.")).toBeInTheDocument();
    expect(onCapturada).not.toHaveBeenCalled();
  });

  it("muestra un mensaje genérico cuando no hay respuesta del servidor", async () => {
    tiposCambioApi.capturarManual.mockRejectedValue(new Error("Network Error"));
    render(<FormularioTasaManual onCapturada={vi.fn()} onCancelar={vi.fn()} />);

    llenar();
    fireEvent.click(screen.getByRole("button", { name: "Guardar tasa manual" }));

    expect(await screen.findByText("No se pudo capturar el tipo de cambio.")).toBeInTheDocument();
  });

  it("Cancelar llama onCancelar", () => {
    const onCancelar = vi.fn();
    render(<FormularioTasaManual onCapturada={vi.fn()} onCancelar={onCancelar} />);

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancelar).toHaveBeenCalledTimes(1);
  });

  it("con fijo deshabilita moneda y fecha", () => {
    render(<FormularioTasaManual fijo onCapturada={vi.fn()} onCancelar={vi.fn()} />);

    expect(screen.getByRole("combobox", { name: "Moneda de la tasa" })).toBeDisabled();
    expect(screen.getByLabelText("Fecha de la tasa")).toBeDisabled();
  });

  it("en línea no crea un formulario propio y envía con un botón de tipo button", async () => {
    tiposCambioApi.capturarManual.mockResolvedValue({});
    const onCapturada = vi.fn();
    const { container } = render(<FormularioTasaManual enLinea onCapturada={onCapturada} onCancelar={vi.fn()} />);

    expect(container.querySelector("form")).toBeNull();
    const guardar = screen.getByRole("button", { name: "Guardar tasa manual" });
    expect(guardar).toHaveAttribute("type", "button");

    llenar();
    fireEvent.click(guardar);

    await waitFor(() => expect(onCapturada).toHaveBeenCalledTimes(1));
    expect(tiposCambioApi.capturarManual).toHaveBeenCalledWith({ monedaCodigo: "USD", fecha: "2026-10-12", tasa: 7.64136, motivo: "Banguat caído" });
  });
});
