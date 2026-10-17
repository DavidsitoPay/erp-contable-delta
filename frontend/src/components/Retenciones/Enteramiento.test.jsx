import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { enteramientosApi } from "../../services/api";
import { AVISO_CALENDARIO } from "../../utils/enteramiento";
import Enteramiento from "./Enteramiento";
import { confirmarAnulacion } from "./enteramientoTestData";

vi.mock("../../services/api", () => ({
  cuentasBancariasApi: { listar: vi.fn().mockResolvedValue({ data: [] }) },
  enteramientosApi: { saldo: vi.fn(), listar: vi.fn(), anular: vi.fn() },
}));

const saldoVencido = {
  periodo: "2026-09-01",
  retenido: 200,
  enterado: 0,
  saldo: 200,
  fechaVencimiento: "2026-10-15",
  diasParaVencer: -2,
  estadoVencimiento: "VENCIDO",
  avisoMulta: "Multa y recargos por pago extemporáneo.",
  calendarioIncompleto: true,
};
const saldoSinSaldo = {
  periodo: "2026-10-01",
  retenido: 100,
  enterado: 100,
  saldo: 0,
  fechaVencimiento: "2026-11-15",
  diasParaVencer: 29,
  estadoVencimiento: "SIN_SALDO",
  avisoMulta: null,
  calendarioIncompleto: false,
};
const vigente = { id: 4, periodo: "2026-09-01", formulario: "SAT-1331", fechaPago: "2026-10-10", boleta: "B-4", monto: 200, estado: "VIGENTE", motivoAnulacion: null };
const anulado = { id: 5, periodo: "2026-09-01", formulario: "SAT-1331", fechaPago: "2026-10-09", boleta: "B-5", monto: 50, estado: "ANULADO", motivoAnulacion: "Error de captura" };

function cargarCon(saldos, enteramientos) {
  vi.mocked(enteramientosApi.saldo).mockResolvedValue({ data: saldos });
  vi.mocked(enteramientosApi.listar).mockResolvedValue({ data: enteramientos });
}

function renderCon() {
  return render(<Enteramiento />);
}

describe("Enteramiento", () => {
  beforeEach(() => {
    cargarCon([saldoVencido, saldoSinSaldo], [vigente, anulado]);
    vi.mocked(enteramientosApi.anular).mockResolvedValue({ data: {} });
  });

  it("muestra el estado de vencimiento, el aviso de calendario y la multa", async () => {
    renderCon();

    expect(await screen.findByText("Vencido")).toBeInTheDocument();
    expect(screen.getByText(AVISO_CALENDARIO)).toBeInTheDocument();
    expect(screen.getByText("Multa y recargos por pago extemporáneo.")).toBeInTheDocument();
    expect(screen.getByText("15/10/2026")).toBeInTheDocument();
  });

  it("avisa de vencimiento próximo cuando faltan 5 días hábiles o menos", async () => {
    cargarCon([{ ...saldoSinSaldo, saldo: 200, fechaVencimiento: "2026-10-20", diasParaVencer: 3, estadoVencimiento: "POR_VENCER", avisoMulta: null, calendarioIncompleto: false }], []);
    renderCon();

    expect(await screen.findByText("Vence en 3 días hábiles o menos: programe el pago.")).toBeInTheDocument();
  });

  it("ofrece Enterar solo en los periodos con saldo", async () => {
    renderCon();

    expect(await screen.findByRole("button", { name: "Enterar 2026-09" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Enterar 2026-10" })).toBeNull();
  });

  it("muestra el motivo de un entero anulado y ofrece Anular solo en los vigentes", async () => {
    renderCon();

    expect(await screen.findByText("Error de captura")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Anular entero B-4" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Anular entero B-5" })).toBeNull();
  });

  it("indica cuando no hay enteramientos registrados", async () => {
    cargarCon([saldoSinSaldo], []);
    renderCon();

    expect(await screen.findByText("No hay enteramientos registrados.")).toBeInTheDocument();
  });

  it("anula con motivo y recarga la pantalla", async () => {
    renderCon();
    fireEvent.click(await screen.findByRole("button", { name: "Anular entero B-4" }));
    confirmarAnulacion("B-4", "Duplicado");

    await waitFor(() => expect(enteramientosApi.anular).toHaveBeenCalledWith(4, "Duplicado"));
    await waitFor(() => expect(enteramientosApi.listar).toHaveBeenCalledTimes(2));
  });

  it("muestra el error si la anulación falla", async () => {
    vi.mocked(enteramientosApi.anular).mockRejectedValue({ response: { data: { error: "El periodo está cerrado." } } });
    renderCon();
    fireEvent.click(await screen.findByRole("button", { name: "Anular entero B-4" }));
    confirmarAnulacion("B-4", "Duplicado");

    expect(await screen.findByText("El periodo está cerrado.")).toBeInTheDocument();
  });

  it("cancela el registro de enteramiento sin cambios", async () => {
    renderCon();
    fireEvent.click(await screen.findByRole("button", { name: "Enterar 2026-09" }));

    expect(screen.getByRole("heading", { name: "Enterar ISR del período 2026-09" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByRole("heading", { name: "Enterar ISR del período 2026-09" })).toBeNull();
  });

  it("muestra el error si la carga falla", async () => {
    vi.mocked(enteramientosApi.saldo).mockRejectedValue(new Error("red"));
    renderCon();

    expect(await screen.findByText("No se pudo cargar el enteramiento.")).toBeInTheDocument();
  });
});
