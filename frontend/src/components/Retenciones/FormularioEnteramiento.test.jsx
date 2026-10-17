import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { cuentasBancariasApi, enteramientosApi } from "../../services/api";
import FormularioEnteramiento from "./FormularioEnteramiento";

vi.mock("../../services/api", () => ({
  cuentasBancariasApi: { listar: vi.fn() },
  enteramientosApi: { vistaPrevia: vi.fn(), crear: vi.fn() },
}));

const saldo = { periodo: "2026-10-01", retenido: 200, enterado: 50, saldo: 150 };
const cuentas = [
  { id: 1, banco: "Banco Industrial", numero: "001", activa: true, monedaCodigo: "GTQ" },
  { id: 2, banco: "BAC", numero: "USD-1", activa: true, monedaCodigo: "USD" },
];
const vista = {
  lineas: [{ cuentaId: 1, codigo: "2.1.03", nombre: "ISR por pagar", debe: 150, haber: 0 }],
  saldoAntes: 150,
  saldoDespues: 0,
};

function renderCon() {
  return render(<FormularioEnteramiento saldo={saldo} onRegistrado={vi.fn()} onCancelar={vi.fn()} />);
}

async function llenar() {
  await screen.findByRole("option", { name: /Banco Industrial/ });
  fireEvent.change(screen.getByLabelText("Formulario"), { target: { value: "SAT-1331" } });
  fireEvent.change(screen.getByLabelText("Fecha de pago"), { target: { value: "2026-10-10" } });
  fireEvent.change(screen.getByLabelText("Número de boleta"), { target: { value: "B-1" } });
  fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "1" } });
}

describe("FormularioEnteramiento", () => {
  beforeEach(() => {
    vi.mocked(cuentasBancariasApi.listar).mockResolvedValue({ data: cuentas });
    vi.mocked(enteramientosApi.vistaPrevia).mockResolvedValue({ data: vista });
    vi.mocked(enteramientosApi.crear).mockResolvedValue({ data: {} });
  });

  it("ofrece solo cuentas en quetzales", async () => {
    renderCon();

    await waitFor(() => expect(within(screen.getByLabelText("Cuenta bancaria")).getAllByRole("option")).toHaveLength(2));
  });

  it("indica cuando no hay cuentas en quetzales", async () => {
    vi.mocked(cuentasBancariasApi.listar).mockResolvedValue({ data: [cuentas[1]] });
    renderCon();

    expect(await screen.findByText("No hay cuentas bancarias activas en quetzales.")).toBeInTheDocument();
  });

  it("muestra la vista previa y la borra al cambiar un campo", async () => {
    renderCon();
    await llenar();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));

    expect(await screen.findByText("ISR por pagar")).toBeInTheDocument();
    expect(enteramientosApi.vistaPrevia).toHaveBeenCalledWith({ periodo: "2026-10-01", monto: 150, cuentaBancariaId: 1 });

    fireEvent.change(screen.getByLabelText("Número de boleta"), { target: { value: "B-2" } });

    expect(screen.queryByText("ISR por pagar")).toBeNull();
  });

  it("muestra error si la vista previa falla", async () => {
    vi.mocked(enteramientosApi.vistaPrevia).mockRejectedValue(new Error("red"));
    renderCon();
    await llenar();
    fireEvent.click(screen.getByRole("button", { name: "Vista previa" }));

    expect(await screen.findByText("No se pudo calcular la vista previa.")).toBeInTheDocument();
  });

  it("muestra error si no carga las cuentas bancarias", async () => {
    vi.mocked(cuentasBancariasApi.listar).mockRejectedValue(new Error("red"));
    renderCon();

    expect(await screen.findByText("No se pudieron cargar las cuentas bancarias.")).toBeInTheDocument();
  });

  it("mantiene deshabilitado el registro hasta completar la boleta", async () => {
    renderCon();
    await llenar();
    fireEvent.change(screen.getByLabelText("Número de boleta"), { target: { value: "  " } });

    expect(screen.getByRole("button", { name: "Registrar enteramiento" })).toBeDisabled();
  });

  it("registra con el payload exacto y muestra el error de la API", async () => {
    vi.mocked(enteramientosApi.crear).mockRejectedValue({ response: { data: { error: "La boleta ya existe." } } });
    renderCon();
    await llenar();
    fireEvent.click(screen.getByRole("button", { name: "Registrar enteramiento" }));

    expect(enteramientosApi.crear).toHaveBeenCalledWith({
      periodo: "2026-10-01",
      monto: 150,
      cuentaBancariaId: 1,
      formulario: "SAT-1331",
      fechaPago: "2026-10-10",
      boleta: "B-1",
    });
    expect(await screen.findByText("La boleta ya existe.")).toBeInTheDocument();
  });

  it("limita la fecha de pago a hoy en hora de Guatemala", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-18T03:00:00Z"));
    try {
      renderCon();
      await screen.findByRole("option", { name: /Banco Industrial/ });

      expect(screen.getByLabelText("Fecha de pago")).toHaveAttribute("max", "2026-10-17");
    } finally {
      vi.useRealTimers();
    }
  });
});
