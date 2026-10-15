import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { revaluacionesApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import { LEYENDA_REVALUACION, mesAnterior } from "../../utils/revaluacion";
import Revaluaciones from "./Revaluaciones";

vi.mock("../../services/api", () => ({
  revaluacionesApi: { vistaPrevia: vi.fn(), ejecutar: vi.fn(), listar: vi.fn(), obtener: vi.fn(), anular: vi.fn() },
}));
vi.mock("../../utils/csv", async (importOriginal) => ({ ...(await importOriginal()), descargarCsv: vi.fn() }));

const vista = {
  anio: 2026,
  mes: 10,
  monedaCodigo: "USD",
  fechaCierre: "2026-10-31",
  tipoCambio: { id: 1, tasa: 7.65, fuente: "Banguat", arrastrado: false },
  totalGanancia: 55,
  totalPerdida: 100,
  partidas: [
    { tipo: "CxC", referencia: "F-100", saldoOriginal: 1000, valorLibrosGtq: 7600, valorCierreGtq: 7650, ajusteGtq: 50, efecto: "Ganancia" },
    { tipo: "CxP", referencia: "P-50", saldoOriginal: 500, valorLibrosGtq: 3830, valorCierreGtq: 3825, ajusteGtq: -5, efecto: "Ganancia" },
    { tipo: "Banco", referencia: "Banco USD", saldoOriginal: 2000, valorLibrosGtq: 15400, valorCierreGtq: 15300, ajusteGtq: -100, efecto: "Perdida" },
  ],
};
const historial = [
  { id: 3, monedaCodigo: "USD", anio: 2026, mes: 10, fechaCierre: "2026-10-31", tasa: 7.65, totalGanancia: 55, totalPerdida: 100, estado: "Vigente", asientoId: 501, asientoReversaId: 502 },
  { id: 2, monedaCodigo: "USD", anio: 2026, mes: 9, fechaCierre: "2026-09-30", tasa: 7.6, totalGanancia: 0, totalPerdida: 0, estado: "Anulada", asientoId: 401, asientoReversaId: 402 },
];

async function llenarPeriodo() {
  fireEvent.change(screen.getByLabelText("Año"), { target: { value: "2026" } });
  fireEvent.change(screen.getByLabelText("Mes"), { target: { value: "10" } });
}

describe("Revaluaciones", () => {
  beforeEach(() => {
    revaluacionesApi.vistaPrevia.mockReset();
    revaluacionesApi.ejecutar.mockReset();
    revaluacionesApi.listar.mockReset();
    revaluacionesApi.obtener.mockReset();
    revaluacionesApi.anular.mockReset();
    revaluacionesApi.listar.mockResolvedValue({ data: historial });
    revaluacionesApi.vistaPrevia.mockResolvedValue({ data: vista });
    revaluacionesApi.ejecutar.mockResolvedValue({ data: { anio: 2026, mes: 10, monedaCodigo: "USD", asientoId: 501, asientoReversaId: 502 } });
  });

  it("muestra la leyenda, el historial y deshabilita la ejecución hasta la vista previa", async () => {
    render(<Revaluaciones />);

    expect(screen.getByText(LEYENDA_REVALUACION)).toBeInTheDocument();
    expect(await screen.findByText("Vigente")).toBeInTheDocument();
    expect(screen.getByLabelText("Mes")).toHaveValue(String(mesAnterior().mes));
    expect(screen.getByRole("button", { name: "Ejecutar revaluación" })).toBeDisabled();
  });

  it("la vista previa muestra la tasa de cierre, los totales y las partidas", async () => {
    render(<Revaluaciones />);
    await llenarPeriodo();

    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));

    expect(await screen.findByText("Tasa de cierre: 7.650000 (Banguat)")).toBeInTheDocument();
    expect(revaluacionesApi.vistaPrevia).toHaveBeenCalledWith({ anio: 2026, mes: 10, monedaCodigo: "USD" });
    expect(screen.getByText("F-100")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ejecutar revaluación" })).toBeEnabled();
  });

  it("cambiar el periodo oculta la vista previa y deshabilita la ejecución", async () => {
    render(<Revaluaciones />);
    await llenarPeriodo();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));
    await screen.findByText("F-100");

    fireEvent.change(screen.getByLabelText("Mes"), { target: { value: "9" } });

    expect(screen.queryByText("F-100")).toBeNull();
    expect(screen.getByRole("button", { name: "Ejecutar revaluación" })).toBeDisabled();
  });

  it("no ejecuta la revaluación si el usuario no confirma", async () => {
    vi.stubGlobal("confirm", vi.fn(() => false));
    render(<Revaluaciones />);
    await llenarPeriodo();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));
    await screen.findByText("F-100");

    fireEvent.click(screen.getByRole("button", { name: "Ejecutar revaluación" }));

    expect(confirm).toHaveBeenCalledTimes(1);
    expect(revaluacionesApi.ejecutar).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("ejecuta tras confirmar, informa los asientos y recarga el historial", async () => {
    vi.stubGlobal("confirm", vi.fn(() => true));
    render(<Revaluaciones />);
    await llenarPeriodo();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));
    await screen.findByText("F-100");

    fireEvent.click(screen.getByRole("button", { name: "Ejecutar revaluación" }));

    expect(await screen.findByText("Revaluación 10/2026 USD registrada (asientos 501 y 502).")).toBeInTheDocument();
    expect(revaluacionesApi.ejecutar).toHaveBeenCalledWith({ anio: 2026, mes: 10, monedaCodigo: "USD" });
    await waitFor(() => expect(revaluacionesApi.listar).toHaveBeenCalledTimes(2));
    vi.unstubAllGlobals();
  });

  it("muestra el 409 de fecha y el de Banguat sin habilitar la ejecución", async () => {
    revaluacionesApi.vistaPrevia.mockRejectedValueOnce({ response: { data: { error: "El mes 10/2026 aún no termina; la revaluación se ejecuta desde su último día." } } });
    render(<Revaluaciones />);
    await llenarPeriodo();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));

    expect(await screen.findByText("El mes 10/2026 aún no termina; la revaluación se ejecuta desde su último día.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ejecutar revaluación" })).toBeDisabled();

    revaluacionesApi.vistaPrevia.mockRejectedValueOnce({ response: { data: { error: "No hay tipo de cambio de USD para el 2026-10-31: Banguat no respondió." } } });
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));

    expect(await screen.findByText("No hay tipo de cambio de USD para el 2026-10-31: Banguat no respondió.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ejecutar revaluación" })).toBeDisabled();
  });

  it("anula solo las revaluaciones vigentes con motivo", async () => {
    render(<Revaluaciones />);
    await screen.findByText("Vigente");

    expect(screen.queryByRole("button", { name: "Anular la revaluación 09/2026 USD" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Anular la revaluación 10/2026 USD" }));
    fireEvent.change(screen.getByLabelText("Motivo de la anulación de la revaluación 10/2026 USD"), { target: { value: "Tasa errónea" } });
    fireEvent.click(screen.getByRole("button", { name: "Anular revaluación" }));

    expect(await screen.findByText("Revaluación anulada.")).toBeInTheDocument();
    expect(revaluacionesApi.anular).toHaveBeenCalledWith(3, "Tasa errónea");
  });

  it("descarga el CSV de una revaluación del historial", async () => {
    revaluacionesApi.obtener.mockResolvedValue({ data: { ...vista, id: 3 } });
    render(<Revaluaciones />);
    await screen.findByText("Vigente");

    fireEvent.click(screen.getByRole("button", { name: "Descargar CSV de la revaluación 10/2026 USD" }));

    await waitFor(() => expect(descargarCsv).toHaveBeenCalledTimes(1));
    expect(revaluacionesApi.obtener).toHaveBeenCalledWith(3);
    expect(descargarCsv.mock.calls[0][0]).toBe("revaluacion-USD-2026-10.csv");
    expect(descargarCsv.mock.calls[0][1].startsWith("Tipo,Referencia")).toBe(true);
  });
});
