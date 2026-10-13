import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import FormularioMotivo from "./FormularioMotivo";

function renderMotivo(onConfirmar = vi.fn().mockResolvedValue(undefined)) {
  const onCancelar = vi.fn();
  render(
    <FormularioMotivo
      titulo="Motivo de la revisión de F-1"
      etiquetaConfirmar="Resolver revisión"
      mensajeFallo="No se pudo resolver la revisión."
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  );
  return { onConfirmar, onCancelar };
}

const motivo = () => screen.getByLabelText("Motivo de la revisión de F-1");
const confirmar = () => screen.getByRole("button", { name: "Resolver revisión" });

describe("FormularioMotivo", () => {
  it("no permite confirmar sin motivo y envía el motivo recortado", async () => {
    const { onConfirmar } = renderMotivo();
    expect(confirmar()).toBeDisabled();

    fireEvent.change(motivo(), { target: { value: "  Régimen confirmado  " } });
    expect(confirmar()).toBeEnabled();
    fireEvent.click(confirmar());

    await waitFor(() => expect(onConfirmar).toHaveBeenCalledWith("Régimen confirmado"));
  });

  it("muestra el error del servidor o el mensaje por defecto y deja volver a intentar", async () => {
    const onConfirmar = vi
      .fn()
      .mockRejectedValueOnce({ response: { data: { error: "La factura ya no está pendiente." } } })
      .mockRejectedValueOnce(new Error("Network Error"));
    renderMotivo(onConfirmar);
    fireEvent.change(motivo(), { target: { value: "Motivo" } });

    fireEvent.click(confirmar());
    expect(await screen.findByText("La factura ya no está pendiente.")).toBeInTheDocument();
    await waitFor(() => expect(confirmar()).toBeEnabled());

    fireEvent.click(confirmar());
    expect(await screen.findByText("No se pudo resolver la revisión.")).toBeInTheDocument();
  });

  it("cancelar invoca onCancelar", () => {
    const { onCancelar } = renderMotivo();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancelar).toHaveBeenCalledTimes(1);
  });
});
