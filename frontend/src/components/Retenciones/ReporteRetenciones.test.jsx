import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ajustesRetencionApi, retencionesApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import ReporteRetenciones from "./ReporteRetenciones";

vi.mock("../../services/api", () => ({
  retencionesApi: { reporte: vi.fn(), registrarConstancia: vi.fn(), anularConstancia: vi.fn() },
  ajustesRetencionApi: { listar: vi.fn(), resolver: vi.fn() },
}));
vi.mock("../../utils/csv", async (importOriginal) => ({ ...(await importOriginal()), descargarCsv: vi.fn() }));

function fila(cambios) {
  return {
    retencionId: 7,
    sentido: "EFECTUADA",
    concepto: "ISR_ROS",
    contraparteId: 4,
    nit: "1234567-8",
    nombre: "Proveedor Uno",
    documentoTipo: "Factura",
    documentoNumero: "F-001",
    dteSerie: "A",
    dteNumero: "99",
    dteUuid: "u-1",
    fechaDocumento: "2026-10-05",
    fechaOperacion: "2026-10-05",
    base: 40000,
    tasa: 5,
    tasaEfectiva: 5.5,
    monto: 2200,
    constanciaId: null,
    constanciaNumero: null,
    constanciaFecha: null,
    formulario: null,
    estadoConstancia: "PENDIENTE",
    periodoContableDistinto: false,
    ...cambios,
  };
}

const reporte = {
  anio: 2026,
  mes: 10,
  filas: [
    fila(),
    fila({ retencionId: 8, nombre: "Proveedor Dos", documentoNumero: "F-002", estadoConstancia: "VENCIDA" }),
    fila({ retencionId: 9, sentido: "RECIBIDA", concepto: "IVA_CLIENTE", nombre: "Cliente Uno", documentoNumero: "F-003", base: 1200, tasa: 25, tasaEfectiva: 25, monto: 300, constanciaId: 3, constanciaNumero: "A-123", constanciaFecha: "2026-11-02", estadoConstancia: "EMITIDA", periodoContableDistinto: true }),
  ],
  totales: [
    { sentido: "EFECTUADA", concepto: "ISR_ROS", cantidad: 2, base: 80000, monto: 4400, sinConstancia: 2 },
    { sentido: "RECIBIDA", concepto: "IVA_CLIENTE", cantidad: 1, base: 1200, monto: 300, sinConstancia: 0 },
  ],
  conciliacion: [
    { concepto: "ISR_ROS", cuentaId: 90, cuentaCodigo: "2.1.5", totalReporte: 4400, movimientoContable: 4400, diferencia: 0 },
    { concepto: "IVA_CLIENTE", cuentaId: 91, cuentaCodigo: "1.1.9", totalReporte: 300, movimientoContable: 0, diferencia: 300 },
  ],
};
const reporteVacio = {
  anio: 2026,
  mes: 3,
  filas: [],
  totales: [{ sentido: "EFECTUADA", concepto: "ISR_ROS", cantidad: 0, base: 0, monto: 0, sinConstancia: 0 }],
  conciliacion: [],
};

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

async function consultar(anio = "2026", mes = "10") {
  escribir("Año", anio);
  escribir("Mes", mes);
  fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
  await waitFor(() => expect(retencionesApi.reporte).toHaveBeenCalled());
}

describe("ReporteRetenciones", () => {
  beforeEach(() => {
    Object.values(retencionesApi).forEach((fn) => fn.mockReset());
    descargarCsv.mockReset();
    retencionesApi.reporte.mockResolvedValue({ data: reporte });
    retencionesApi.registrarConstancia.mockResolvedValue({ data: { advertencias: [] } });
    retencionesApi.anularConstancia.mockResolvedValue({ data: {} });
  });

  it("muestra el aviso de que el ERP registra la constancia y el año y mes actuales por defecto", () => {
    render(<ReporteRetenciones />);

    expect(screen.getByText("Reporte de retenciones")).toBeInTheDocument();
    expect(screen.getByText("Insumo para declarar; el ERP registra los datos de la constancia, no la emite.")).toBeInTheDocument();
    expect(screen.getByLabelText("Año")).toHaveValue(new Date().getFullYear());
    expect(screen.getByLabelText("Mes")).toHaveValue(String(new Date().getMonth() + 1));
    expect(screen.queryByRole("button", { name: "Exportar CSV" })).toBeNull();
  });

  it("consulta el período y muestra filas, totales y conciliación con su explicación", async () => {
    render(<ReporteRetenciones />);

    await consultar();

    expect(retencionesApi.reporte).toHaveBeenCalledWith({ anio: 2026, mes: 10 });
    expect(await screen.findByText("Proveedor Uno")).toBeInTheDocument();
    expect(screen.getByText("Cliente Uno")).toBeInTheDocument();
    expect(screen.getByText("Totales por impuesto")).toBeInTheDocument();
    expect(screen.getByText("2.1.5")).toBeInTheDocument();
    expect(screen.getByText("La diferencia se explica por cobros registrados en un mes distinto al de la factura.")).toBeInTheDocument();
    expect(within(screen.getByText("Proveedor Dos").closest("tr")).getByText("Vencida")).toBeInTheDocument();
  });

  it("envía solo los filtros elegidos", async () => {
    render(<ReporteRetenciones />);

    escribir("Sentido", "EFECTUADA");
    escribir("Impuesto", "ISR_ROS");
    escribir("NIT", " 1234567-8 ");
    escribir("Estado de la constancia", "PENDIENTE");
    await consultar();

    expect(retencionesApi.reporte).toHaveBeenCalledWith({
      anio: 2026,
      mes: 10,
      sentido: "EFECTUADA",
      concepto: "ISR_ROS",
      nit: "1234567-8",
      estadoConstancia: "PENDIENTE",
    });
  });

  it("registra una constancia, muestra las advertencias y vuelve a consultar el período", async () => {
    retencionesApi.registrarConstancia.mockResolvedValue({ data: { advertencias: ["La fecha de la constancia difiere de la fecha de la factura."] } });
    render(<ReporteRetenciones />);
    await consultar();
    await screen.findByText("Proveedor Uno");

    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia de F-001" }));
    fireEvent.change(screen.getByLabelText("Número de constancia"), { target: { value: "A-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia" }));

    await waitFor(() => expect(retencionesApi.registrarConstancia).toHaveBeenCalledWith(7, expect.objectContaining({ numero: "A-1", fecha: "2026-10-05" })));
    expect(await screen.findByText("La fecha de la constancia difiere de la fecha de la factura.")).toBeInTheDocument();
    await waitFor(() => expect(retencionesApi.reporte).toHaveBeenCalledTimes(2));
    expect(screen.queryByLabelText("Número de constancia")).toBeNull();
  });

  it("anula una constancia emitida con motivo y vuelve a consultar", async () => {
    render(<ReporteRetenciones />);
    await consultar();
    await screen.findByText("Cliente Uno");

    fireEvent.click(screen.getByRole("button", { name: "Anular constancia de F-003" }));
    fireEvent.change(screen.getByLabelText("Motivo de anulación de la constancia A-123"), { target: { value: "Número mal digitado" } });
    fireEvent.click(screen.getByRole("button", { name: "Anular constancia" }));

    await waitFor(() => expect(retencionesApi.anularConstancia).toHaveBeenCalledWith(3, "Número mal digitado"));
    await waitFor(() => expect(retencionesApi.reporte).toHaveBeenCalledTimes(2));
  });

  it("cancelar el registro cierra el formulario sin llamar a la API", async () => {
    render(<ReporteRetenciones />);
    await consultar();
    await screen.findByText("Proveedor Uno");
    fireEvent.click(screen.getByRole("button", { name: "Registrar constancia de F-001" }));

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByLabelText("Número de constancia")).toBeNull();
    expect(retencionesApi.registrarConstancia).not.toHaveBeenCalled();
  });

  it("resuelve el ajuste de retención pendiente con motivo y referencia y vuelve a consultar", async () => {
    retencionesApi.reporte.mockResolvedValue({ data: { ...reporte, filas: [fila({ ajustePendiente: true })] } });
    ajustesRetencionApi.listar.mockResolvedValue({ data: [{ id: 31, retencionId: 7, notaCreditoId: 21, notaCreditoNumero: "NC-9", documentoOrigenNumero: "F-001", montoNc: 10 }] });
    ajustesRetencionApi.resolver.mockResolvedValue({ data: {} });
    render(<ReporteRetenciones />);
    await consultar();

    fireEvent.click(await screen.findByRole("button", { name: "Resolver ajuste de F-001" }));
    fireEvent.change(await screen.findByLabelText("Motivo"), { target: { value: "Confirmado" } });
    fireEvent.change(screen.getByLabelText("Referencia"), { target: { value: "NC-9" } });
    fireEvent.click(screen.getByRole("button", { name: "Resolver ajuste" }));

    await waitFor(() => expect(ajustesRetencionApi.resolver).toHaveBeenCalledWith(31, { motivo: "Confirmado", referencia: "NC-9" }));
    await waitFor(() => expect(retencionesApi.reporte).toHaveBeenCalledTimes(2));
  });

  it("exporta el CSV con el nombre del período consultado y los encabezados fijos", async () => {
    render(<ReporteRetenciones />);
    await consultar("2026", "3");
    await screen.findByText("Proveedor Uno");
    escribir("Año", "2030");

    fireEvent.click(screen.getByRole("button", { name: "Exportar CSV" }));

    expect(descargarCsv).toHaveBeenCalledTimes(1);
    const [nombre, contenido] = descargarCsv.mock.calls[0];
    expect(nombre).toBe("retenciones-2026-03.csv");
    expect(contenido.split("\r\n")[0]).toBe(
      "Sentido,Impuesto,NIT,Nombre,Pais,Documento,Serie DTE,Numero DTE,UUID,Fecha documento,Fecha pago o cobro,Base,Tasa,Retencion,Constancia,Fecha constancia,Formulario,Estado constancia,Tipo de renta (codigo),Tipo de renta,Moneda,Monto origen,Tipo de cambio,Fecha tipo de cambio,Enterado,Saldo por enterar,AjustePendiente"
    );
    expect(contenido.split("\r\n")).toHaveLength(4);
  });

  it("un mes sin retenciones muestra el mensaje y exporta solo el encabezado", async () => {
    retencionesApi.reporte.mockResolvedValue({ data: reporteVacio });
    render(<ReporteRetenciones />);
    await consultar("2026", "3");

    expect(await screen.findByText("No hay retenciones en el período seleccionado.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Exportar CSV" }));

    expect(descargarCsv.mock.calls[0][1].split("\r\n")).toHaveLength(1);
  });

  it("muestra el error del servidor y uno genérico, y oculta el reporte", async () => {
    retencionesApi.reporte.mockRejectedValueOnce({ response: { data: { error: "El mes debe estar entre 1 y 12." } } });
    render(<ReporteRetenciones />);

    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    expect(await screen.findByText("El mes debe estar entre 1 y 12.")).toBeInTheDocument();

    retencionesApi.reporte.mockRejectedValueOnce(new Error("Network Error"));
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    expect(await screen.findByText("No se pudo consultar el reporte de retenciones.")).toBeInTheDocument();
    expect(screen.queryByText("Totales por impuesto")).toBeNull();
  });

  it("muestra Cargando mientras llega la respuesta", () => {
    retencionesApi.reporte.mockReturnValue(new Promise(() => {}));
    render(<ReporteRetenciones />);

    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Consultar" })).toBeDisabled();
  });
});
