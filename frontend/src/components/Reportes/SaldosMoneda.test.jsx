import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { reportesSaldosApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import { formatoImporte, formatoMoneda } from "../../utils/formato";
import SaldosMoneda from "./SaldosMoneda";

vi.mock("../../services/api", () => ({ reportesSaldosApi: { saldosAntiguedad: vi.fn() } }));
vi.mock("../../utils/csv", async (importOriginal) => ({ ...(await importOriginal()), descargarCsv: vi.fn() }));

const reporte = {
  tipo: "CxC",
  corte: "2026-10-31",
  leyenda: "Saldos en moneda extranjera valorados a la tasa de libros.",
  filas: [{ documentoId: 7, numero: "F-100", tercero: "Cliente SA", fecha: "2026-09-12", fechaVencimiento: "2026-10-12", moneda: "USD", saldoOriginal: 1000, tipoCambioLibros: 7.6, saldoGtq: 7600, diasVencido: 19, tramo: "1-30" }],
  totales: [{ moneda: "USD", saldoOriginal: 1000, saldoGtq: 7600, porTramo: { NO_VENCIDO: 0, "1-30": 1000, "31-60": 0, "61-90": 0, "91+": 0 } }],
  totalGtq: 7600,
  conciliacion: [{ cuentaId: 4, cuentaCodigo: "1.1.3", totalReporte: 7600, saldoMayor: 7600, revaluacionVigente: 0, diferencia: 0 }],
};

describe("SaldosMoneda", () => {
  beforeEach(() => {
    descargarCsv.mockReset();
    reportesSaldosApi.saldosAntiguedad.mockReset();
    reportesSaldosApi.saldosAntiguedad.mockResolvedValue({ data: reporte });
  });

  it("consulta CxC sin moneda cuando no se elige una", async () => {
    render(<SaldosMoneda />);
    fireEvent.change(screen.getByLabelText("Fecha de corte"), { target: { value: "2026-10-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    await screen.findByText("F-100");
    expect(reportesSaldosApi.saldosAntiguedad).toHaveBeenCalledWith({ tipo: "CxC", corte: "2026-10-31" });
  });

  it("consulta CxP con la moneda elegida", async () => {
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "CxP" }));
    fireEvent.change(screen.getByLabelText("Moneda del reporte"), { target: { value: "USD" } });
    fireEvent.change(screen.getByLabelText("Fecha de corte"), { target: { value: "2026-10-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    await screen.findByText("F-100");
    expect(reportesSaldosApi.saldosAntiguedad).toHaveBeenCalledWith({ tipo: "CxP", corte: "2026-10-31", moneda: "USD" });
  });

  it("muestra la leyenda, la fila, los totales por moneda y el total en GTQ", async () => {
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(await screen.findByText(reporte.leyenda)).toBeInTheDocument();
    const fila = within(screen.getByText("F-100").closest("tr"));
    expect(fila.getByText("Cliente SA")).toBeInTheDocument();
    expect(fila.getByText(formatoImporte(1000, "USD"))).toBeInTheDocument();
    expect(fila.getByText("7.600000")).toBeInTheDocument();
    expect(fila.getByText("1-30 días")).toBeInTheDocument();
    expect(screen.getByText(`Total en GTQ: ${formatoMoneda.format(7600)}`)).toBeInTheDocument();
  });

  it("muestra la conciliación solo cuando hay cuentas", async () => {
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(await screen.findByText("Conciliación con el libro mayor")).toBeInTheDocument();
    expect(screen.getByText("1.1.3")).toBeInTheDocument();
  });

  it("sin conciliación no muestra ese bloque", async () => {
    reportesSaldosApi.saldosAntiguedad.mockResolvedValue({ data: { ...reporte, conciliacion: [] } });
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    await screen.findByText("F-100");
    expect(screen.queryByText("Conciliación con el libro mayor")).toBeNull();
  });

  it("sin filas muestra el mensaje de corte", async () => {
    reportesSaldosApi.saldosAntiguedad.mockResolvedValue({ data: { ...reporte, filas: [] } });
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(await screen.findByText("No hay saldos pendientes al corte.")).toBeInTheDocument();
  });

  it("muestra el error del servidor", async () => {
    reportesSaldosApi.saldosAntiguedad.mockRejectedValue({ response: { data: { error: "La fecha de corte es obligatoria." } } });
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(await screen.findByText("La fecha de corte es obligatoria.")).toBeInTheDocument();
  });

  it("exporta el CSV del reporte consultado", async () => {
    render(<SaldosMoneda />);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    await screen.findByText("F-100");

    fireEvent.click(screen.getByRole("button", { name: "Exportar CSV" }));

    await waitFor(() => expect(descargarCsv).toHaveBeenCalledTimes(1));
    const [nombre, contenido] = descargarCsv.mock.calls[0];
    expect(nombre).toBe("saldos-cxc-2026-10-31.csv");
    expect(contenido.startsWith("Número,")).toBe(true);
  });
});
