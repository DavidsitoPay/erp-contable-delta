import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { impuestosApi } from "../../services/api";
import Impuestos from "./Impuestos";

vi.mock("../../services/api", () => ({ impuestosApi: { listar: vi.fn(), crear: vi.fn(), actualizar: vi.fn(), desactivar: vi.fn() } }));

const iva = {
  id: 1,
  codigo: "IVA_GENERAL",
  nombre: "IVA general 12 %",
  tipo: "IVA_GENERAL",
  tasa: 12,
  aplicaA: "AMBOS",
  generaCredito: true,
  articuloLegal: "Decreto 27-92, art. 10",
  vigenteDesde: "2001-01-01",
  vigenteHasta: null,
  activo: true,
  enUso: true,
  nota: "[VERIFICAR con asesor] vigencia referencial",
};
const exento = {
  id: 2,
  codigo: "EXENTO_EXPORTACION",
  nombre: "Exento: exportación",
  tipo: "EXENTO",
  tasa: 0,
  aplicaA: "AMBOS",
  generaCredito: false,
  articuloLegal: "Decreto 27-92, art. 7",
  vigenteDesde: "1992-07-01",
  vigenteHasta: "2030-12-31",
  activo: true,
  enUso: false,
  nota: null,
};
const viejo = { ...exento, id: 3, codigo: "VIEJO", nombre: "Impuesto viejo", activo: false, vigenteHasta: null };

function fila(nombre) {
  return screen.getByText(nombre).closest("tr");
}

async function renderCargado() {
  render(<Impuestos />);
  await screen.findByText("IVA general 12 %");
}

describe("Impuestos", () => {
  beforeEach(() => {
    Object.values(impuestosApi).forEach((fn) => fn.mockReset());
    impuestosApi.listar.mockImplementation(({ incluirInactivos }) =>
      Promise.resolve({ data: incluirInactivos ? [iva, exento, viejo] : [iva, exento] })
    );
    impuestosApi.crear.mockResolvedValue({ data: {} });
    impuestosApi.actualizar.mockResolvedValue({ data: {} });
    impuestosApi.desactivar.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lista los impuestos con tipo, tasa, ámbito, crédito, vigencia, estado y nota", async () => {
    await renderCargado();

    const filaIva = within(fila("IVA general 12 %"));
    expect(filaIva.getByText("IVA general")).toBeInTheDocument();
    expect(filaIva.getByText("12")).toBeInTheDocument();
    expect(filaIva.getByText("Ventas y compras")).toBeInTheDocument();
    expect(filaIva.getByText("Sí")).toBeInTheDocument();
    expect(filaIva.getByText("2001-01-01 – vigente")).toBeInTheDocument();
    expect(filaIva.getByText("Activo")).toBeInTheDocument();
    expect(filaIva.getByText("[VERIFICAR con asesor] vigencia referencial")).toBeInTheDocument();
    expect(within(fila("Exento: exportación")).getByText("1992-07-01 – 2030-12-31")).toBeInTheDocument();
    expect(impuestosApi.listar).toHaveBeenCalledWith({ incluirInactivos: false });
  });

  it("muestra un mensaje cuando no hay impuestos", async () => {
    impuestosApi.listar.mockResolvedValue({ data: [] });
    render(<Impuestos />);

    expect(await screen.findByText("No hay impuestos para mostrar.")).toBeInTheDocument();
  });

  it("incluir inactivos recarga la lista, muestra los inactivos", async () => {
    await renderCargado();
    expect(screen.queryByText("Impuesto viejo")).toBeNull();

    fireEvent.click(screen.getByLabelText("Incluir inactivos"));

    expect(await screen.findByText("Impuesto viejo")).toBeInTheDocument();
    expect(within(fila("Impuesto viejo")).getByText("Inactivo")).toBeInTheDocument();
    expect(impuestosApi.listar).toHaveBeenLastCalledWith({ incluirInactivos: true });
    expect(screen.queryByRole("button", { name: "Desactivar Impuesto viejo" })).toBeNull();
  });

  it("muestra el error cuando falla la carga", async () => {
    impuestosApi.listar.mockRejectedValue({ response: { data: { error: "Sin permiso." } } });
    render(<Impuestos />);

    expect(await screen.findByText("Sin permiso.")).toBeInTheDocument();
  });

  it("crea un impuesto, recarga y limpia el formulario", async () => {
    await renderCargado();

    fireEvent.change(screen.getByLabelText("Código"), { target: { value: "IVA_ESPECIAL" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "IVA especial" } });
    fireEvent.change(screen.getByLabelText("Tasa (%)"), { target: { value: "8" } });
    fireEvent.change(screen.getByLabelText("Aplica a"), { target: { value: "VENTAS" } });
    fireEvent.change(screen.getByLabelText("Referencia legal"), { target: { value: "Decreto X" } });
    fireEvent.change(screen.getByLabelText("Vigente desde"), { target: { value: "2027-01-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar impuesto" }));

    await waitFor(() =>
      expect(impuestosApi.crear).toHaveBeenCalledWith({
        codigo: "IVA_ESPECIAL",
        nombre: "IVA especial",
        tipo: "IVA_GENERAL",
        tasa: 8,
        aplicaA: "VENTAS",
        generaCredito: true,
        articuloLegal: "Decreto X",
        vigenteDesde: "2027-01-01",
        vigenteHasta: null,
      })
    );
    await waitFor(() => expect(impuestosApi.listar).toHaveBeenCalledTimes(2));
    expect(screen.getByLabelText("Código")).toHaveValue("");
  });

  it("un tipo exento fija la tasa en 0 y desactiva tasa y crédito; IVA general los reactiva", async () => {
    await renderCargado();

    fireEvent.change(screen.getByLabelText("Tipo"), { target: { value: "EXENTO" } });
    expect(screen.getByLabelText("Tasa (%)")).toHaveValue(0);
    expect(screen.getByLabelText("Tasa (%)")).toBeDisabled();
    expect(screen.getByLabelText("Genera crédito fiscal")).toBeDisabled();
    expect(screen.getByLabelText("Genera crédito fiscal")).not.toBeChecked();

    fireEvent.change(screen.getByLabelText("Tipo"), { target: { value: "IVA_GENERAL" } });
    expect(screen.getByLabelText("Tasa (%)")).toBeEnabled();
    expect(screen.getByLabelText("Genera crédito fiscal")).toBeChecked();
  });

  it("muestra el error del servidor y el mensaje genérico al crear", async () => {
    impuestosApi.crear.mockRejectedValueOnce({ response: { data: { error: "Ya existe una versión de ese impuesto con la misma fecha de inicio." } } });
    await renderCargado();
    fireEvent.change(screen.getByLabelText("Código"), { target: { value: "IVA_GENERAL" } });
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Otro" } });
    fireEvent.change(screen.getByLabelText("Tasa (%)"), { target: { value: "12" } });
    fireEvent.change(screen.getByLabelText("Referencia legal"), { target: { value: "Art" } });
    fireEvent.change(screen.getByLabelText("Vigente desde"), { target: { value: "2001-01-01" } });

    fireEvent.click(screen.getByRole("button", { name: "Agregar impuesto" }));
    expect(await screen.findByText("Ya existe una versión de ese impuesto con la misma fecha de inicio.")).toBeInTheDocument();

    impuestosApi.crear.mockRejectedValueOnce(new Error("Network Error"));
    fireEvent.click(screen.getByRole("button", { name: "Agregar impuesto" }));
    expect(await screen.findByText("No se pudo guardar el impuesto.")).toBeInTheDocument();
  });

  it("nueva versión precarga el impuesto con el mismo código fijo y sin vigencias", async () => {
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Nueva versión de IVA general 12 %" }));

    expect(screen.getByText("Nueva versión del impuesto")).toBeInTheDocument();
    expect(screen.getByLabelText("Código")).toHaveValue("IVA_GENERAL");
    expect(screen.getByLabelText("Código")).toBeDisabled();
    expect(screen.getByLabelText("Tasa (%)")).toHaveValue(12);
    expect(screen.getByLabelText("Vigente desde")).toHaveValue("");

    fireEvent.change(screen.getByLabelText("Tasa (%)"), { target: { value: "10" } });
    fireEvent.change(screen.getByLabelText("Vigente desde"), { target: { value: "2027-01-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Crear nueva versión" }));

    await waitFor(() =>
      expect(impuestosApi.crear).toHaveBeenCalledWith(expect.objectContaining({ codigo: "IVA_GENERAL", tasa: 10, vigenteDesde: "2027-01-01", vigenteHasta: null }))
    );
    await waitFor(() => expect(screen.getByText("Agregar impuesto", { selector: "h3" })).toBeInTheDocument());
  });

  it("editar un impuesto en uso bloquea los campos sensibles, avisa y conserva sus valores en el cuerpo", async () => {
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Editar IVA general 12 %" }));

    expect(screen.getByLabelText("Tipo")).toBeDisabled();
    expect(screen.getByLabelText("Tasa (%)")).toBeDisabled();
    expect(screen.getByLabelText("Aplica a")).toBeDisabled();
    expect(screen.getByLabelText("Vigente desde")).toBeDisabled();
    expect(screen.getByLabelText("Genera crédito fiscal")).toBeDisabled();
    expect(screen.getByText("Ya fue usado en documentos; cree una nueva versión para cambiar la tasa")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "IVA general (12 %)" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));

    await waitFor(() =>
      expect(impuestosApi.actualizar).toHaveBeenCalledWith(1, {
        nombre: "IVA general (12 %)",
        tipo: "IVA_GENERAL",
        tasa: 12,
        aplicaA: "AMBOS",
        generaCredito: true,
        articuloLegal: "Decreto 27-92, art. 10",
        nota: "[VERIFICAR con asesor] vigencia referencial",
        vigenteDesde: "2001-01-01",
        vigenteHasta: null,
        activo: true,
      })
    );
    await waitFor(() => expect(screen.queryByText("Ya fue usado en documentos; cree una nueva versión para cambiar la tasa")).toBeNull());
  });

  it("editar un impuesto sin uso permite cambiar todos los campos", async () => {
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Editar Exento: exportación" }));
    expect(screen.getByLabelText("Vigente desde")).toBeEnabled();
    expect(screen.getByLabelText("Vigente hasta (opcional)")).toHaveValue("2030-12-31");
    fireEvent.change(screen.getByLabelText("Aplica a"), { target: { value: "VENTAS" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));

    await waitFor(() =>
      expect(impuestosApi.actualizar).toHaveBeenCalledWith(2, {
        nombre: "Exento: exportación",
        articuloLegal: "Decreto 27-92, art. 7",
        vigenteHasta: "2030-12-31",
        activo: true,
        tipo: "EXENTO",
        tasa: 0,
        aplicaA: "VENTAS",
        generaCredito: false,
        nota: null,
        vigenteDesde: "1992-07-01",
      })
    );
  });

  it("cancelar la edición vuelve al formulario de alta", async () => {
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "Editar IVA general 12 %" }));

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByRole("button", { name: "Agregar impuesto" })).toBeInTheDocument();
    expect(screen.getByLabelText("Código")).toHaveValue("");
  });

  it("cierra la vigencia solo de impuestos activos sin fecha final", async () => {
    await renderCargado();

    expect(screen.getByRole("button", { name: "Cerrar vigencia de IVA general 12 %" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cerrar vigencia de Exento: exportación" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Cerrar vigencia de IVA general 12 %" }));
    fireEvent.change(screen.getByLabelText("Fecha final de vigencia de IVA general 12 %"), { target: { value: "2026-12-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar cierre" }));

    await waitFor(() =>
      expect(impuestosApi.actualizar).toHaveBeenCalledWith(1, {
        nombre: "IVA general 12 %",
        tipo: "IVA_GENERAL",
        tasa: 12,
        aplicaA: "AMBOS",
        generaCredito: true,
        articuloLegal: "Decreto 27-92, art. 10",
        nota: "[VERIFICAR con asesor] vigencia referencial",
        vigenteDesde: "2001-01-01",
        vigenteHasta: "2026-12-31",
        activo: true,
      })
    );
    await waitFor(() => expect(screen.queryByRole("button", { name: "Confirmar cierre" })).toBeNull());
  });

  it("si el cierre falla muestra el error y conserva el panel; también se puede cancelar", async () => {
    impuestosApi.actualizar.mockRejectedValue({ response: { data: { error: "No se puede cerrar la vigencia del impuesto IVA_GENERAL antes del 2026-10-01: existen documentos con esa fecha." } } });
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Cerrar vigencia de IVA general 12 %" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmar cierre" }));

    expect(await screen.findByText(/No se puede cerrar la vigencia del impuesto IVA_GENERAL/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Confirmar cierre" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar cierre" }));
    expect(screen.queryByRole("button", { name: "Confirmar cierre" })).toBeNull();
  });

  it("desactiva un impuesto tras confirmar y no hace nada si se cancela", async () => {
    const confirmar = vi.fn(() => false);
    vi.stubGlobal("confirm", confirmar);
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar IVA general 12 %" }));
    expect(impuestosApi.desactivar).not.toHaveBeenCalled();

    confirmar.mockReturnValue(true);
    fireEvent.click(screen.getByRole("button", { name: "Desactivar IVA general 12 %" }));

    await waitFor(() => expect(impuestosApi.desactivar).toHaveBeenCalledWith(1));
    await waitFor(() => expect(impuestosApi.listar).toHaveBeenCalledTimes(2));
  });

  it("muestra el error si la desactivación falla", async () => {
    vi.stubGlobal("confirm", vi.fn(() => true));
    impuestosApi.desactivar.mockRejectedValue(new Error("Network Error"));
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar IVA general 12 %" }));

    expect(await screen.findByText("No se pudo desactivar el impuesto.")).toBeInTheDocument();
  });
});
