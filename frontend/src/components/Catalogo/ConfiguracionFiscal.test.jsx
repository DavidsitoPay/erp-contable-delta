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
  { id: 17, codigo: "2107", nombre: "ISR retenido", tipo: "Pasivo", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 18, codigo: "1106", nombre: "IVA retenido por clientes", tipo: "Activo", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
  { id: 19, codigo: "2108", nombre: "ISR retenido a no residentes", tipo: "Pasivo", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 21, codigo: "4201", nombre: "Ganancia cambiaria realizada", tipo: "Ingreso", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 22, codigo: "5201", nombre: "Pérdida cambiaria realizada", tipo: "Gasto", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
  { id: 23, codigo: "4202", nombre: "Ganancia por revaluación", tipo: "Ingreso", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
  { id: 24, codigo: "5202", nombre: "Pérdida por revaluación", tipo: "Gasto", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
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
  cuentaIsrRetenidoPagarId: 17,
  cuentaIsrNrRetenidoPagarId: 19,
  cuentaIvaRetenidoClientesId: 18,
  cuentaDifGananciaRealizadaId: 21,
  cuentaDifPerdidaRealizadaId: 22,
  cuentaRevalGananciaId: 23,
  cuentaRevalPerdidaId: 24,
  actualizadoEn: "2026-10-09T15:00:00Z",
  actualizadoPorNombre: "Administradora Delta",
};
const configSinCuentas = {
  ...configCompleta,
  cuentaIvaDebitoId: null,
  cuentaIvaCreditoId: null,
  cuentaIsrRetenidoPagarId: null,
  cuentaIsrNrRetenidoPagarId: null,
  cuentaIvaRetenidoClientesId: null,
  cuentaDifGananciaRealizadaId: null,
  cuentaDifPerdidaRealizadaId: null,
  cuentaRevalGananciaId: null,
  cuentaRevalPerdidaId: null,
};

async function renderCon(config) {
  configuracionFiscalApi.obtener.mockResolvedValue({ data: config });
  render(<ConfiguracionFiscal />);
  await screen.findByLabelText("NIT de la empresa");
  await screen.findAllByRole("option", { name: "2105 - IVA por pagar" });
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
        cuentaIsrRetenidoPagarId: null,
        cuentaIsrNrRetenidoPagarId: null,
        cuentaIvaRetenidoClientesId: null,
        cuentaDifGananciaRealizadaId: null,
        cuentaDifPerdidaRealizadaId: null,
        cuentaRevalGananciaId: null,
        cuentaRevalPerdidaId: null,
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
        cuentaIsrRetenidoPagarId: null,
        cuentaIsrNrRetenidoPagarId: null,
        cuentaIvaRetenidoClientesId: null,
        cuentaDifGananciaRealizadaId: null,
        cuentaDifPerdidaRealizadaId: null,
        cuentaRevalGananciaId: null,
        cuentaRevalPerdidaId: null,
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

  it("avisa cuando faltan las cuentas de retención y no cuando están configuradas", async () => {
    await renderCon(configSinCuentas);
    expect(screen.getByText(/Falta configurar las cuentas de retención/)).toBeInTheDocument();
  });

  it("no avisa de las cuentas de retención con ambas configuradas", async () => {
    await renderCon(configCompleta);
    expect(screen.queryByText(/Falta configurar las cuentas de retención/)).toBeNull();
    expect(screen.getByLabelText("Cuenta de ISR retenido por pagar (Pasivo)")).toHaveValue("17");
    expect(screen.getByLabelText("Cuenta de IVA retenido por clientes (Activo)")).toHaveValue("18");
  });

  it("ofrece para las cuentas de retención solo hojas activas del tipo y naturaleza exigidos", async () => {
    await renderCon(configSinCuentas);

    const isr = screen.getByLabelText("Cuenta de ISR retenido por pagar (Pasivo)");
    expect(within(isr).getByRole("option", { name: "2107 - ISR retenido" })).toBeInTheDocument();
    expect(within(isr).queryByRole("option", { name: "1106 - IVA retenido por clientes" })).toBeNull();
    const ivaRetenido = screen.getByLabelText("Cuenta de IVA retenido por clientes (Activo)");
    expect(within(ivaRetenido).getByRole("option", { name: "1106 - IVA retenido por clientes" })).toBeInTheDocument();
    expect(within(ivaRetenido).queryByRole("option", { name: "2107 - ISR retenido" })).toBeNull();
  });

  it("guarda las cuentas de retención elegidas", async () => {
    configuracionFiscalApi.actualizar.mockResolvedValue({ data: configCompleta });
    await renderCon(configSinCuentas);

    fireEvent.change(screen.getByLabelText("Cuenta de ISR retenido por pagar (Pasivo)"), { target: { value: "17" } });
    fireEvent.change(screen.getByLabelText("Cuenta de IVA retenido por clientes (Activo)"), { target: { value: "18" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));

    await waitFor(() =>
      expect(configuracionFiscalApi.actualizar).toHaveBeenCalledWith(
        expect.objectContaining({ cuentaIsrRetenidoPagarId: 17, cuentaIvaRetenidoClientesId: 18 })
      )
    );
  });

  it("muestra el aviso de que la retención de IVA como agente no está disponible cuando la empresa es agente", async () => {
    await renderCon(configCompleta);
    expect(screen.queryByText("La retención de IVA como agente no está disponible en esta versión.")).toBeNull();

    fireEvent.click(screen.getByLabelText("La empresa es agente de retención de IVA"));

    expect(screen.getByText("La retención de IVA como agente no está disponible en esta versión.")).toBeInTheDocument();
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

  it("muestra las cuatro cuentas cambiarias guardadas sin aviso", async () => {
    await renderCon(configCompleta);

    expect(screen.getByLabelText("Ganancia cambiaria realizada (Ingreso)")).toHaveValue("21");
    expect(screen.getByLabelText("Pérdida cambiaria realizada (Gasto)")).toHaveValue("22");
    expect(screen.getByLabelText("Ganancia por revaluación no realizada (Ingreso)")).toHaveValue("23");
    expect(screen.getByLabelText("Pérdida por revaluación no realizada (Gasto)")).toHaveValue("24");
    expect(screen.queryByText(/Falta configurar las cuentas de diferencial cambiario/)).toBeNull();
  });

  it("avisa de las cuentas cambiarias pendientes", async () => {
    await renderCon(configSinCuentas);

    expect(screen.getByText(/Falta configurar las cuentas de diferencial cambiario/)).toBeInTheDocument();
  });

  it("ofrece ingresos para ganancias y gastos para pérdidas", async () => {
    await renderCon(configSinCuentas);

    const ganancia = screen.getByLabelText("Ganancia cambiaria realizada (Ingreso)");
    expect(within(ganancia).getByRole("option", { name: "4201 - Ganancia cambiaria realizada" })).toBeInTheDocument();
    expect(within(ganancia).queryByRole("option", { name: "5201 - Pérdida cambiaria realizada" })).toBeNull();
    const perdida = screen.getByLabelText("Pérdida cambiaria realizada (Gasto)");
    expect(within(perdida).getByRole("option", { name: "5201 - Pérdida cambiaria realizada" })).toBeInTheDocument();
    expect(within(perdida).queryByRole("option", { name: "4201 - Ganancia cambiaria realizada" })).toBeNull();
  });

  it("guarda las cuatro cuentas cambiarias elegidas", async () => {
    configuracionFiscalApi.actualizar.mockResolvedValue({ data: configCompleta });
    await renderCon(configSinCuentas);

    fireEvent.change(screen.getByLabelText("Ganancia cambiaria realizada (Ingreso)"), { target: { value: "21" } });
    fireEvent.change(screen.getByLabelText("Pérdida cambiaria realizada (Gasto)"), { target: { value: "22" } });
    fireEvent.change(screen.getByLabelText("Ganancia por revaluación no realizada (Ingreso)"), { target: { value: "23" } });
    fireEvent.change(screen.getByLabelText("Pérdida por revaluación no realizada (Gasto)"), { target: { value: "24" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));

    await waitFor(() =>
      expect(configuracionFiscalApi.actualizar).toHaveBeenCalledWith(
        expect.objectContaining({ cuentaDifGananciaRealizadaId: 21, cuentaDifPerdidaRealizadaId: 22, cuentaRevalGananciaId: 23, cuentaRevalPerdidaId: 24 })
      )
    );
  });

  it("avisa de la cuenta de ISR a no residentes cuando falta y no cuando está configurada", async () => {
    await renderCon(configSinCuentas);
    expect(screen.getByText(/Falta configurar la cuenta de ISR a no residentes/)).toBeInTheDocument();
  });

  it("no avisa de la cuenta de ISR a no residentes con la cuenta configurada", async () => {
    await renderCon(configCompleta);
    expect(screen.queryByText(/Falta configurar la cuenta de ISR a no residentes/)).toBeNull();
    expect(screen.getByLabelText("Cuenta de ISR retenido a no residentes por pagar (Pasivo)")).toHaveValue("19");
  });

  it("ofrece para ISR a no residentes solo cuentas hoja activas de pasivo", async () => {
    await renderCon(configSinCuentas);

    const isrNr = screen.getByLabelText("Cuenta de ISR retenido a no residentes por pagar (Pasivo)");
    expect(within(isrNr).getByRole("option", { name: "2108 - ISR retenido a no residentes" })).toBeInTheDocument();
    expect(within(isrNr).queryByRole("option", { name: "1106 - IVA retenido por clientes" })).toBeNull();
  });

  it("guarda la cuenta de ISR a no residentes elegida", async () => {
    configuracionFiscalApi.actualizar.mockResolvedValue({ data: configCompleta });
    await renderCon(configSinCuentas);

    fireEvent.change(screen.getByLabelText("Cuenta de ISR retenido a no residentes por pagar (Pasivo)"), { target: { value: "19" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar configuración" }));

    await waitFor(() =>
      expect(configuracionFiscalApi.actualizar).toHaveBeenCalledWith(expect.objectContaining({ cuentaIsrNrRetenidoPagarId: 19 }))
    );
  });
});
