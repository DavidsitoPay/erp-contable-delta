import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ajustesRetencionApi } from "../../services/api";
import ResolverAjusteDialog from "./ResolverAjusteDialog";

vi.mock("../../services/api", () => ({ ajustesRetencionApi: { listar: vi.fn(), resolver: vi.fn() } }));

const ajustes = [
  { id: 31, retencionId: 7, notaCreditoId: 21, documentoOrigenId: 11, notaCreditoNumero: "NC-9", documentoOrigenNumero: "F-001", montoNc: 10 },
  { id: 32, retencionId: 8, notaCreditoId: 22, documentoOrigenId: 12, notaCreditoNumero: "NC-10", documentoOrigenNumero: "F-002", montoNc: 20 },
];

function renderCon(criterio, { onResuelto = vi.fn(), onCancelar = vi.fn() } = {}) {
  render(<ResolverAjusteDialog {...criterio} onResuelto={onResuelto} onCancelar={onCancelar} />);
  return { onResuelto, onCancelar };
}

function llenar(motivo, referencia) {
  fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: motivo } });
  fireEvent.change(screen.getByLabelText("Referencia"), { target: { value: referencia } });
}

beforeEach(() => {
  ajustesRetencionApi.listar.mockReset();
  ajustesRetencionApi.resolver.mockReset();
  ajustesRetencionApi.listar.mockResolvedValue({ data: ajustes });
});

describe("ResolverAjusteDialog", () => {
  it("encuentra el ajuste pendiente de la retención indicada", async () => {
    renderCon({ retencionId: 8 });

    expect(await screen.findByRole("heading", { name: /NC-10 sobre F-002/ })).toBeInTheDocument();
    expect(ajustesRetencionApi.listar).toHaveBeenCalledWith("PENDIENTE");
  });

  it("encuentra el ajuste pendiente por nota de crédito", async () => {
    renderCon({ documentoId: 21 });

    expect(await screen.findByRole("heading", { name: /NC-9 sobre F-001/ })).toBeInTheDocument();
  });

  it("encuentra el ajuste pendiente por documento origen", async () => {
    renderCon({ documentoId: 12 });

    expect(await screen.findByRole("heading", { name: /NC-10 sobre F-002/ })).toBeInTheDocument();
  });

  it("avisa cuando el ajuste ya no está pendiente y permite cancelar", async () => {
    const { onCancelar } = renderCon({ retencionId: 99 });

    expect(await screen.findByText("El ajuste ya no está pendiente.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancelar).toHaveBeenCalledTimes(1);
  });

  it("muestra el error cuando no se puede cargar el ajuste", async () => {
    ajustesRetencionApi.listar.mockRejectedValue({ response: { data: { error: "Servicio no disponible." } } });
    renderCon({ retencionId: 7 });

    expect(await screen.findByText("Servicio no disponible.")).toBeInTheDocument();
  });

  it("deshabilita el envío hasta tener motivo y referencia", async () => {
    renderCon({ retencionId: 7 });
    await screen.findByRole("heading", { name: /NC-9/ });

    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Confirmado" } });
    expect(screen.getByRole("button", { name: "Resolver ajuste" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Referencia"), { target: { value: "NC-9" } });
    expect(screen.getByRole("button", { name: "Resolver ajuste" })).toBeEnabled();
  });

  it("envía motivo y referencia sin espacios y notifica al resolver", async () => {
    ajustesRetencionApi.resolver.mockResolvedValue({ data: {} });
    const { onResuelto } = renderCon({ retencionId: 7 });
    await screen.findByRole("heading", { name: /NC-9/ });

    llenar(" Confirmado ", " NC-9 ");
    fireEvent.click(screen.getByRole("button", { name: "Resolver ajuste" }));

    await waitFor(() => expect(ajustesRetencionApi.resolver).toHaveBeenCalledWith(31, { motivo: "Confirmado", referencia: "NC-9" }));
    await waitFor(() => expect(onResuelto).toHaveBeenCalledTimes(1));
  });

  it("muestra el error del servidor cuando la resolución falla", async () => {
    ajustesRetencionApi.resolver.mockRejectedValue({ response: { data: { error: "El ajuste ya fue resuelto." } } });
    const { onResuelto } = renderCon({ retencionId: 7 });
    await screen.findByRole("heading", { name: /NC-9/ });

    llenar("Confirmado", "NC-9");
    fireEvent.click(screen.getByRole("button", { name: "Resolver ajuste" }));

    expect(await screen.findByText("El ajuste ya fue resuelto.")).toBeInTheDocument();
    expect(onResuelto).not.toHaveBeenCalled();
  });

  it("cancela desde el formulario sin resolver", async () => {
    const { onCancelar } = renderCon({ retencionId: 7 });
    await screen.findByRole("heading", { name: /NC-9/ });

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancelar).toHaveBeenCalledTimes(1);
    expect(ajustesRetencionApi.resolver).not.toHaveBeenCalled();
  });
});
