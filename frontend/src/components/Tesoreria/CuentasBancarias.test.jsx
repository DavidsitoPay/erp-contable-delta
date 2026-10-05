import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cuentasBancariasApi } from "../../services/api";
import CuentasBancarias from "./CuentasBancarias";

vi.mock("../../services/api", () => ({ cuentasBancariasApi: { crear: vi.fn(), desactivar: vi.fn() } }));

const cuentasContables = [
  { id: 1, codigo: "1101", nombre: "Banco BAC", tipo: "Activo", activa: true, cuentaPadreId: null },
  { id: 2, codigo: "3101", nombre: "Capital social", tipo: "Capital", activa: true, cuentaPadreId: null },
  { id: 3, codigo: "4101", nombre: "Ventas", tipo: "Ingreso", activa: true, cuentaPadreId: null },
];
const cuentasBancarias = [
  { id: 7, banco: "BAC", numero: "123", tipo: "Monetaria", activa: true, cuentaContableId: 1, cuentaContableCodigo: "1101", cuentaContableNombre: "Banco BAC", saldoApertura: 1000, fechaApertura: "2025-01-01", saldo: 1500 },
  { id: 8, banco: "Industrial", numero: "456", tipo: "Ahorro", activa: false, cuentaContableId: 4, cuentaContableCodigo: "1102", cuentaContableNombre: "Banco Ind", saldoApertura: 0, fechaApertura: null, saldo: 0 },
];

describe("CuentasBancarias", () => {
  let recargar;

  beforeEach(() => {
    Object.values(cuentasBancariasApi).forEach((fn) => fn.mockReset());
    cuentasBancariasApi.crear.mockResolvedValue({ data: {} });
    cuentasBancariasApi.desactivar.mockResolvedValue({ data: {} });
    recargar = vi.fn().mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function renderCuentas() {
    render(<CuentasBancarias cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} recargar={recargar} />);
  }

  function llenarBasicos() {
    fireEvent.change(screen.getByLabelText("Banco"), { target: { value: " Banrural " } });
    fireEvent.change(screen.getByLabelText("Número de cuenta"), { target: { value: "999" } });
    fireEvent.change(screen.getByLabelText("Cuenta contable"), { target: { value: "1" } });
  }

  it("lista las cuentas con saldo, saldo de apertura y estado", () => {
    renderCuentas();

    expect(within(screen.getByRole("table")).getByText("BAC")).toBeInTheDocument();
    expect(within(screen.getByRole("table")).getByText("1101 - Banco BAC")).toBeInTheDocument();
    expect(screen.getByText(/1[,.\s ]?500[.,]00/)).toBeInTheDocument();
    expect(screen.getByText("Activa")).toBeInTheDocument();
    expect(screen.getByText("Inactiva")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Desactivar" })).toHaveLength(1);
  });

  it("ofrece solo cuentas de Activo como cuenta contable", () => {
    renderCuentas();

    const select = screen.getByLabelText("Cuenta contable");
    expect(within(select).getByRole("option", { name: "1101 - Banco BAC" })).toBeInTheDocument();
    expect(within(select).queryByRole("option", { name: "3101 - Capital social" })).toBeNull();
  });

  it("crea una cuenta sin apertura sin enviar fecha ni contrapartida y recarga", async () => {
    renderCuentas();
    expect(screen.queryByLabelText("Fecha de apertura")).toBeNull();
    llenarBasicos();
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    await waitFor(() => expect(recargar).toHaveBeenCalledTimes(1));
    expect(cuentasBancariasApi.crear).toHaveBeenCalledWith({
      banco: "Banrural",
      numero: "999",
      tipo: "Monetaria",
      cuentaContableId: 1,
      saldoApertura: 0,
    });
  });

  it("con saldo de apertura mayor a cero pide fecha y contrapartida de Capital y las envía", async () => {
    renderCuentas();
    llenarBasicos();
    fireEvent.change(screen.getByLabelText("Tipo de cuenta"), { target: { value: "Ahorro" } });
    fireEvent.change(screen.getByLabelText("Saldo de apertura"), { target: { value: "2500" } });

    const contrapartida = screen.getByLabelText("Cuenta de contrapartida de la apertura");
    expect(within(contrapartida).getByRole("option", { name: "3101 - Capital social" })).toBeInTheDocument();
    expect(within(contrapartida).queryByRole("option", { name: "4101 - Ventas" })).toBeNull();

    fireEvent.change(screen.getByLabelText("Fecha de apertura"), { target: { value: "2025-02-01" } });
    fireEvent.change(contrapartida, { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    await waitFor(() => expect(cuentasBancariasApi.crear).toHaveBeenCalledTimes(1));
    expect(cuentasBancariasApi.crear).toHaveBeenCalledWith({
      banco: "Banrural",
      numero: "999",
      tipo: "Ahorro",
      cuentaContableId: 1,
      saldoApertura: 2500,
      fechaApertura: "2025-02-01",
      cuentaContrapartidaId: 2,
    });
  });

  it("muestra el error del servidor y el mensaje por defecto al crear", async () => {
    cuentasBancariasApi.crear.mockRejectedValueOnce({ response: { data: { error: "Ya existe la cuenta bancaria Banrural 999." } } });
    renderCuentas();
    llenarBasicos();
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    expect(await screen.findByText("Ya existe la cuenta bancaria Banrural 999.")).toBeInTheDocument();

    cuentasBancariasApi.crear.mockRejectedValueOnce(new Error("red"));
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    expect(await screen.findByText("No se pudo crear la cuenta bancaria.")).toBeInTheDocument();
  });

  it("desactiva la cuenta solo si se confirma", async () => {
    vi.stubGlobal("confirm", vi.fn(() => false));
    renderCuentas();
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));
    expect(cuentasBancariasApi.desactivar).not.toHaveBeenCalled();

    vi.stubGlobal("confirm", vi.fn(() => true));
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));

    await waitFor(() => expect(cuentasBancariasApi.desactivar).toHaveBeenCalledWith(7));
    await waitFor(() => expect(recargar).toHaveBeenCalledTimes(1));
  });

  it("muestra el error al desactivar cuando la API falla", async () => {
    cuentasBancariasApi.desactivar.mockRejectedValueOnce({ response: { data: { error: "La cuenta tiene movimientos pendientes." } } });
    vi.stubGlobal("confirm", vi.fn(() => true));
    renderCuentas();
    fireEvent.click(screen.getByRole("button", { name: "Desactivar" }));
    expect(await screen.findByText("La cuenta tiene movimientos pendientes.")).toBeInTheDocument();
  });
});
