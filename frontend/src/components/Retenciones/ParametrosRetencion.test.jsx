import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { parametrosRetencionApi } from "../../services/api";
import { formatoMoneda } from "../../utils/formato";
import ParametrosRetencion from "./ParametrosRetencion";

vi.mock("../../services/api", () => ({ parametrosRetencionApi: { listar: vi.fn(), crear: vi.fn(), actualizar: vi.fn() } }));

const base = { tramoLimite: null, tasaExcedente: null, diaLimite: null, nota: "Referencial, validar con asesor", vigenteHasta: null, activo: true, enUso: false, umbralSobre: "BASE", umbralMinimo: 0 };
const isr = { ...base, id: 1, codigo: "ISR_PROVEEDOR_ROS", concepto: "ISR_PROVEEDOR_ROS", nombre: "ISR proveedores simplificado", tasaBase: 5, tramoLimite: 30000, tasaExcedente: 7, umbralMinimo: 2500, articuloLegal: "Dto. 10-2012 art. 48", vigenteDesde: "2014-01-01", enUso: true };
const iva = { ...base, id: 2, codigo: "IVA_CLIENTE_SECTOR_PUBLICO", concepto: "IVA_RETENIDO_CLIENTE", nombre: "IVA sector público", tasaBase: 25, umbralMinimo: 30000, umbralSobre: "TOTAL", articuloLegal: "Dto. 20-2006", vigenteDesde: "2007-01-01", vigenteHasta: "2030-12-31" };
const plazo = { ...base, id: 3, codigo: "PLAZO_CONSTANCIA_ISR", concepto: "PLAZO_CONSTANCIA", nombre: "Plazo de constancia ISR", tasaBase: null, diaLimite: 5, articuloLegal: "Dto. 10-2012 art. 48", vigenteDesde: "2014-01-01" };
const viejo = { ...iva, id: 4, nombre: "IVA viejo", activo: false, vigenteHasta: null };

function iniciarSesionComo(perfil) {
  localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
}

function fila(nombre) {
  return screen.getByText(nombre).closest("tr");
}

async function renderCargado(perfil = "Administrador del sistema") {
  iniciarSesionComo(perfil);
  render(<ParametrosRetencion />);
  await screen.findByText("ISR proveedores simplificado");
}

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

describe("ParametrosRetencion", () => {
  beforeEach(() => {
    Object.values(parametrosRetencionApi).forEach((fn) => fn.mockReset());
    parametrosRetencionApi.listar.mockImplementation(({ incluirInactivos }) =>
      Promise.resolve({ data: incluirInactivos ? [isr, iva, plazo, viejo] : [isr, iva, plazo] })
    );
    parametrosRetencionApi.crear.mockResolvedValue({ data: {} });
    parametrosRetencionApi.actualizar.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lista los parámetros con tasa, umbral, vigencia, estado y la leyenda referencial", async () => {
    await renderCargado();

    const filaIsr = within(fila("ISR proveedores simplificado"));
    expect(filaIsr.getByText(`5 % hasta ${formatoMoneda.format(30000)} y 7 % sobre el excedente`)).toBeInTheDocument();
    expect(filaIsr.getByText(`${formatoMoneda.format(2500)} · Base sin IVA`)).toBeInTheDocument();
    expect(filaIsr.getByText("2014-01-01 – vigente")).toBeInTheDocument();
    expect(filaIsr.getByText("En uso")).toBeInTheDocument();
    expect(filaIsr.getByText("Referencial, validar con asesor")).toBeInTheDocument();
    expect(within(fila("IVA sector público")).getByText("25 %")).toBeInTheDocument();
    expect(within(fila("IVA sector público")).getByText(`${formatoMoneda.format(30000)} · Total de la factura`)).toBeInTheDocument();
    expect(within(fila("Plazo de constancia ISR")).getByText("Día 5 del mes siguiente")).toBeInTheDocument();
    expect(screen.getByText(/Parámetros referenciales: validar con el asesor fiscal/)).toBeInTheDocument();
    expect(parametrosRetencionApi.listar).toHaveBeenCalledWith({ incluirInactivos: false });
  });

  it("el Administrador ve el formulario y las acciones", async () => {
    await renderCargado();

    expect(screen.getByText("Nueva versión de parámetro")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Nueva versión de ISR proveedores simplificado" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Desactivar ISR proveedores simplificado" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cerrar vigencia de IVA sector público" })).toBeNull();
  });

  it("el Contador solo consulta: sin formulario ni acciones", async () => {
    await renderCargado("Contador");

    expect(screen.getByText("Solo el Administrador del sistema puede modificar los parámetros.")).toBeInTheDocument();
    expect(screen.queryByText("Nueva versión de parámetro")).toBeNull();
    expect(screen.queryByRole("button", { name: /Desactivar/ })).toBeNull();
  });

  it("muestra un mensaje sin parámetros y el error cuando falla la carga", async () => {
    iniciarSesionComo("Contador");
    parametrosRetencionApi.listar.mockResolvedValueOnce({ data: [] });
    const { unmount } = render(<ParametrosRetencion />);
    expect(await screen.findByText("No hay parámetros de retención para mostrar.")).toBeInTheDocument();
    unmount();

    parametrosRetencionApi.listar.mockRejectedValueOnce({ response: { data: { error: "Sin permiso." } } });
    render(<ParametrosRetencion />);
    expect(await screen.findByText("Sin permiso.")).toBeInTheDocument();
  });

  it("incluir inactivos recarga la lista y no ofrece desactivar los ya inactivos", async () => {
    await renderCargado();

    fireEvent.click(screen.getByLabelText("Incluir inactivos"));

    expect(await screen.findByText("IVA viejo")).toBeInTheDocument();
    expect(parametrosRetencionApi.listar).toHaveBeenLastCalledWith({ incluirInactivos: true });
    expect(screen.queryByRole("button", { name: "Desactivar IVA viejo" })).toBeNull();
  });

  it("crea una versión nueva de ISR con tramo, recarga y limpia el formulario", async () => {
    await renderCargado();

    escribir("Código", "ISR_PROVEEDOR_ROS");
    escribir("Nombre", "ISR 2027");
    escribir("Tasa base (%)", "5");
    escribir("Tramo límite (opcional)", "30000");
    escribir("Tasa sobre el excedente (%)", "7");
    escribir("Umbral mínimo", "2500");
    escribir("Referencia legal", "Decreto X");
    escribir("Vigente desde", "2027-01-01");
    fireEvent.click(screen.getByRole("button", { name: "Guardar versión" }));

    await waitFor(() =>
      expect(parametrosRetencionApi.crear).toHaveBeenCalledWith({
        codigo: "ISR_PROVEEDOR_ROS",
        concepto: "ISR_PROVEEDOR_ROS",
        nombre: "ISR 2027",
        tasaBase: 5,
        tramoLimite: 30000,
        tasaExcedente: 7,
        umbralMinimo: 2500,
        umbralSobre: "BASE",
        diaLimite: null,
        articuloLegal: "Decreto X",
        nota: "Referencial, validar con asesor",
        vigenteDesde: "2027-01-01",
        vigenteHasta: null,
      })
    );
    await waitFor(() => expect(parametrosRetencionApi.listar).toHaveBeenCalledTimes(2));
    expect(screen.getByLabelText("Código")).toHaveValue("");
  });

  it("crea el plazo de la constancia con su día límite", async () => {
    await renderCargado();

    escribir("Concepto", "PLAZO_CONSTANCIA");
    escribir("Código", "PLAZO_CONSTANCIA_ISR");
    escribir("Nombre", "Plazo 2027");
    escribir("Día límite del mes siguiente", "10");
    escribir("Referencia legal", "Decreto X");
    escribir("Vigente desde", "2027-01-01");
    fireEvent.click(screen.getByRole("button", { name: "Guardar versión" }));

    await waitFor(() =>
      expect(parametrosRetencionApi.crear).toHaveBeenCalledWith(
        expect.objectContaining({ concepto: "PLAZO_CONSTANCIA", tasaBase: null, umbralMinimo: 0, diaLimite: 10 })
      )
    );
  });

  it("muestra el error del servidor y el mensaje genérico al crear", async () => {
    parametrosRetencionApi.crear.mockRejectedValueOnce({ response: { data: { error: "La vigencia se traslapa con otra versión." } } });
    await renderCargado();
    escribir("Código", "ISR_PROVEEDOR_ROS");
    escribir("Nombre", "ISR");
    escribir("Tasa base (%)", "5");
    escribir("Referencia legal", "Decreto X");
    escribir("Vigente desde", "2014-01-01");

    fireEvent.click(screen.getByRole("button", { name: "Guardar versión" }));
    expect(await screen.findByText("La vigencia se traslapa con otra versión.")).toBeInTheDocument();

    parametrosRetencionApi.crear.mockRejectedValueOnce(new Error("Network Error"));
    fireEvent.click(screen.getByRole("button", { name: "Guardar versión" }));
    expect(await screen.findByText("No se pudo guardar el parámetro.")).toBeInTheDocument();
  });

  it("nueva versión precarga el parámetro sin vigencias y conserva el código", async () => {
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Nueva versión de ISR proveedores simplificado" }));

    expect(screen.getByLabelText("Código")).toHaveValue("ISR_PROVEEDOR_ROS");
    expect(screen.getByLabelText("Tasa base (%)")).toHaveValue(5);
    expect(screen.getByLabelText("Tramo límite (opcional)")).toHaveValue(30000);
    expect(screen.getByLabelText("Vigente desde")).toHaveValue("");
  });

  it("cerrar vigencia envía solo nota, activo y la vigencia final elegida", async () => {
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Cerrar vigencia de ISR proveedores simplificado" }));
    fireEvent.change(screen.getByLabelText("Fecha final de vigencia de ISR proveedores simplificado"), { target: { value: "2026-12-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar cierre" }));

    await waitFor(() =>
      expect(parametrosRetencionApi.actualizar).toHaveBeenCalledWith(1, { nota: "Referencial, validar con asesor", activo: true, vigenteHasta: "2026-12-31" })
    );
    await waitFor(() => expect(screen.queryByRole("button", { name: "Confirmar cierre" })).toBeNull());
  });

  it("cancelar el cierre oculta el formulario sin llamar a la API", async () => {
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "Cerrar vigencia de ISR proveedores simplificado" }));

    fireEvent.click(screen.getByRole("button", { name: "Cancelar cierre" }));

    expect(screen.queryByRole("button", { name: "Confirmar cierre" })).toBeNull();
    expect(parametrosRetencionApi.actualizar).not.toHaveBeenCalled();
  });

  it("desactivar pide confirmación y solo cambia el estado activo", async () => {
    const confirmar = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true);
    vi.stubGlobal("confirm", confirmar);
    await renderCargado();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar ISR proveedores simplificado" }));
    expect(parametrosRetencionApi.actualizar).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Desactivar ISR proveedores simplificado" }));
    await waitFor(() =>
      expect(parametrosRetencionApi.actualizar).toHaveBeenCalledWith(1, { nota: "Referencial, validar con asesor", activo: false, vigenteHasta: null })
    );
  });
});
