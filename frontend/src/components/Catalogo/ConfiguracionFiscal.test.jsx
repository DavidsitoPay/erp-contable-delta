import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { configuracionFiscalApi, cuentasApi } from "../../services/api";
import ConfiguracionFiscal from "./ConfiguracionFiscal";

vi.mock("../../services/api", () => ({
  configuracionFiscalApi: { obtener: vi.fn(), actualizar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
}));

const AVISO = "Falta configurar las cuentas de IVA: no se podrán registrar facturas con IVA hasta que se configuren.";

const cuentas = [
  { id: 11, codigo: "2105", nombre: "IVA por pagar", tipo: "Pasivo", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 12, codigo: "1105", nombre: "IVA por acreditar", tipo: "Activo", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
  { id: 13, codigo: "4101", nombre: "Ventas", tipo: "Ingreso", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 14, codigo: "2106", nombre: "Pasivo inactivo", tipo: "Pasivo", naturaleza: "Acreedora", activa: false, cuentaPadreId: null },
  { id: 15, codigo: "2100", nombre: "Pasivos", tipo: "Pasivo", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 16, codigo: "2100-1", nombre: "Pasivo hijo", tipo: "Pasivo", naturaleza: "Acreedora", activa: true, cuentaPadreId: 15 },
];

const configCompleta = {
  nitEmpresa: "1234567-9",
  nombreLegal: "Delta S.A.",
  monedaFuncionalId: 1,
  monedaFuncionalCodigo: "GTQ",
  regimenIsr: "UTILIDADES",
  agenteRetencionIva: false,
  tipoAgenteIva: null,
  cuentaIvaDebitoId: 11,
  cuentaIvaCreditoId: 12,
  actualizadoEn: "2026-10-09T15:00:00Z",
  actualizadoPorNombre: "Administradora Delta",
};
const configSinCuentas = { ...configCompleta, cuentaIvaDebitoId: null, cuentaIvaCreditoId: null };

async function renderCon(config) {
  configuracionFiscalApi.obtener.mockResolvedValue({ data: config });
  render(<ConfiguracionFiscal />);
  await screen.findByLabelText("NIT de la empresa");
  await screen.findByRole("option", { name: "2105 - IVA por pagar" });
}

describe("ConfiguracionFiscal", () => {
  beforeEach(() => {
    Object.values(configuracionFiscalApi).forEach((fn) => fn.mockReset());
    cuentasApi.listar.mockReset();
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
  });

  it("muestra los valores guardados, la moneda funcional de solo lectura y la última actualización", async () => {
    await renderCon(configCompleta);

    expect(screen.getByLabelText("NIT de la empresa")).toHaveValue("1234567-9");
    expect(screen.getByLabelText("Nombre legal")).toHaveValue("Delta S.A.");
    expect(screen.getByLabelText("Moneda funcional")).toBeDisabled();
    expect(screen.getByLabelText("Moneda funcional")).toHaveValue("GTQ");
    expect(screen.getByLabelText("Régimen de ISR")).toHaveValue("UTILIDADES");
    expect(screen.getByLabelText("Cuenta de IVA débito fiscal (Pasivo)")).toHaveValue("11");
    expect(screen.getByText(/Última actualización: .*2026.* por Administradora Delta/)).toBeInTheDocument();
  });

  it("avisa cuando falta alguna cuenta de IVA y no avisa cuando están ambas", async () => {
    await renderCon(configSinCuentas);
    expect(screen.getByText(AVISO)).toBeInTheDocument();
  });

  it("no muestra el aviso con ambas cuentas configuradas", async () => {
    await renderCon(configCompleta);
    expect(screen.queryByText(AVISO)).toBeNull();
  });

  it("ofrece solo cuentas hoja activas con el tipo y la naturaleza exigidos", async () => {
    await renderCon(configSinCuentas);

    const debito = screen.getByLabelText("Cuenta de IVA débito fiscal (Pasivo)");
    expect(within(debito).getByRole("option", { name: "2105 - IVA por pagar" })).toBeInTheDocument();
    expect(within(debito).getByRole("option", { name: "2100-1 - Pasivo hijo" })).toBeInTheDocument();
    ["1105 - IVA por acreditar", "4101 - Ventas", "2106 - Pasivo inactivo", "2100 - Pasivos"].forEach((nombre) =>
      expect(within(debito).queryByRole("option", { name: nombre })).toBeNull()
    );
    const credito = screen.getByLabelText("Cuenta de IVA crédito fiscal (Activo)");
    expect(within(credito).getByRole("option", { name: "1105 - IVA por acreditar" })).toBeInTheDocument();
    expect(within(credito).queryByRole("option", { name: "2105 - IVA por pagar" })).toBeNull();
  });

  it("el tipo de agente aparece solo con la casilla y es obligatorio para guardar", async () => {
    await renderCon(configCompleta);
    expect(screen.queryByLabelText("Tipo de agente")).toBeNull();

    fireEvent.click(screen.getByLabelText("La empresa es agente de retención de IVA"));
    expect(screen.getByLabelText("Tipo de agente")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Guardar configuración" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Tipo de agente"), { target: { value: "SECTOR_PUBLICO" } });
    expect(screen.getByRole("button", { name: "Guardar configuración" })).toBeEnabled();
  });

  it("guarda la configuración editada y refleja la respuesta del servidor", async () => {
    const guardada = { ...configCompleta, regimenIsr: "SIMPLIFICADO", agenteRetencionIva: true, tipoAgenteIva: "SECTOR_PUBLICO" };
    configuracionFiscalApi.actualizar.mockResolvedValue({ data: guardada });
    await renderCon(configSinCuentas);

    fireEvent.change(screen.getByLabelText("NIT de la empresa"), { target: { value: " 1234567-9 " } });
    fireEvent.change(screen.getByLabelText("Régimen de ISR"), { target: { value: "SIMPLIFICADO" } });
    fireEvent.click(screen.getByLabelText("La empresa es agente de retención de IVA"));
    fireEvent.change(screen.getByLabelText("Tipo de agente"), { target: { value: "SECTOR_PUBLICO" } });
    fireEvent.change(screen.getByLabelText("Cuenta de IVA débito fiscal (Pasivo)"), { target: { value: "11" } });
    fireEvent.change(screen.getByLabelText("Cuenta de IVA crédito fiscal (Activo)"), { target: { value: "12" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));

    await waitFor(() =>
      expect(configuracionFiscalApi.actualizar).toHaveBeenCalledWith({
        nitEmpresa: "1234567-9",
        nombreLegal: "Delta S.A.",
        regimenIsr: "SIMPLIFICADO",
        agenteRetencionIva: true,
        tipoAgenteIva: "SECTOR_PUBLICO",
        cuentaIvaDebitoId: 11,
        cuentaIvaCreditoId: 12,
      })
    );
    expect(await screen.findByText("Configuración guardada.")).toBeInTheDocument();
    expect(screen.queryByText(AVISO)).toBeNull();
  });

  it("envía null en NIT, nombre, tipo de agente y cuentas cuando están vacíos o la casilla está desmarcada", async () => {
    configuracionFiscalApi.actualizar.mockResolvedValue({ data: configSinCuentas });
    await renderCon({ ...configSinCuentas, agenteRetencionIva: true, tipoAgenteIva: "COMBUSTIBLE" });

    fireEvent.change(screen.getByLabelText("NIT de la empresa"), { target: { value: "" } });
    fireEvent.change(screen.getByLabelText("Nombre legal"), { target: { value: " " } });
    fireEvent.click(screen.getByLabelText("La empresa es agente de retención de IVA"));
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));

    await waitFor(() =>
      expect(configuracionFiscalApi.actualizar).toHaveBeenCalledWith({
        nitEmpresa: null,
        nombreLegal: null,
        regimenIsr: "UTILIDADES",
        agenteRetencionIva: false,
        tipoAgenteIva: null,
        cuentaIvaDebitoId: null,
        cuentaIvaCreditoId: null,
      })
    );
  });

  it("muestra el error del servidor al guardar y un mensaje genérico si no hay respuesta", async () => {
    configuracionFiscalApi.actualizar.mockRejectedValueOnce({ response: { data: { error: "El NIT de la empresa no es válido." } } });
    await renderCon(configCompleta);

    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));
    expect(await screen.findByText("El NIT de la empresa no es válido.")).toBeInTheDocument();

    configuracionFiscalApi.actualizar.mockRejectedValueOnce(new Error("Network Error"));
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));
    expect(await screen.findByText("No se pudo guardar la configuración fiscal.")).toBeInTheDocument();
  });

  it("muestra el error cuando no se puede cargar la configuración", async () => {
    configuracionFiscalApi.obtener.mockRejectedValue({ response: { data: { error: "Sin permiso." } } });
    render(<ConfiguracionFiscal />);

    expect(await screen.findByText("Sin permiso.")).toBeInTheDocument();
    expect(screen.queryByLabelText("NIT de la empresa")).toBeNull();
  });

  it("muestra Cargando mientras llega la configuración", () => {
    configuracionFiscalApi.obtener.mockReturnValue(new Promise(() => {}));
    render(<ConfiguracionFiscal />);

    expect(screen.getByText("Cargando...")).toBeInTheDocument();
  });
});
