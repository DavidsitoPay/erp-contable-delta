import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { retencionesApi } from "../../services/api";
import FormularioConstancia from "./FormularioConstancia";

vi.mock("../../services/api", () => ({ retencionesApi: { registrarConstancia: vi.fn() } }));

const fila = { retencionId: 7, nombre: "Proveedor Uno", documentoNumero: "F-001", fechaDocumento: "2026-10-05" };

function renderFormulario() {
  const onRegistrada = vi.fn().mockResolvedValue(undefined);
  const onCancelar = vi.fn();
  render(<FormularioConstancia fila={fila} onRegistrada={onRegistrada} onCancelar={onCancelar} />);
  return { onRegistrada, onCancelar };
}

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

describe("FormularioConstancia", () => {
  beforeEach(() => {
    retencionesApi.registrarConstancia.mockReset();
    retencionesApi.registrarConstancia.mockResolvedValue({ data: { advertencias: [] } });
  });

  it("propone la fecha de la factura y titula con el tercero y el documento", () => {
    renderFormulario();

    expect(screen.getByText("Registrar constancia de Proveedor Uno (F-001)")).toBeInTheDocument();
    expect(screen.getByLabelText("Fecha de la constancia")).toHaveValue("2026-10-05");
  });

  it("registra la constancia con los datos capturados y entrega las advertencias", async () => {
    retencionesApi.registrarConstancia.mockResolvedValue({ data: { advertencias: ["La fecha de la constancia difiere de la fecha de la factura."] } });
    const { onRegistrada } = renderFormulario();

    escribir("Número de constancia", " A-123 ");
    escribir("Fecha de la constancia", "2026-10-20");
    escribir("Formulario", "SAT-1331");
    escribir("Fecha de pago del formulario", "2026-11-02");
    escribir("Período declarado", "2026-10");
    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia" }));

    await waitFor(() =>
      expect(retencionesApi.registrarConstancia).toHaveBeenCalledWith(7, {
        numero: "A-123",
        fecha: "2026-10-20",
        formulario: "SAT-1331",
        fechaPagoFormulario: "2026-11-02",
        periodoDeclarado: "2026-10-01",
      })
    );
    await waitFor(() => expect(onRegistrada).toHaveBeenCalledWith(["La fecha de la constancia difiere de la fecha de la factura."]));
  });

  it("envía null en los datos opcionales vacíos", async () => {
    renderFormulario();

    escribir("Número de constancia", "B-1");
    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia" }));

    await waitFor(() =>
      expect(retencionesApi.registrarConstancia).toHaveBeenCalledWith(7, {
        numero: "B-1",
        fecha: "2026-10-05",
        formulario: null,
        fechaPagoFormulario: null,
        periodoDeclarado: null,
      })
    );
  });

  it("muestra el error del servidor y uno genérico sin cerrar el formulario", async () => {
    retencionesApi.registrarConstancia
      .mockRejectedValueOnce({ response: { data: { error: "Ya existe una constancia con ese número." } } })
      .mockRejectedValueOnce(new Error("Network Error"));
    const { onRegistrada } = renderFormulario();
    escribir("Número de constancia", "A-1");

    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia" }));
    expect(await screen.findByText("Ya existe una constancia con ese número.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia" }));
    expect(await screen.findByText("No se pudo registrar la constancia.")).toBeInTheDocument();
    expect(onRegistrada).not.toHaveBeenCalled();
  });

  it("cancelar invoca onCancelar", () => {
    const { onCancelar } = renderFormulario();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(onCancelar).toHaveBeenCalledTimes(1);
  });
});
