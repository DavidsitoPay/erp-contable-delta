import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { asientosApi, centrosCostoApi, cuentasApi, periodosApi } from "../../services/api";
import RegistrarAsiento from "./RegistrarAsiento";

vi.mock("../../services/api", () => ({
  asientosApi: { crear: vi.fn() },
  centrosCostoApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  periodosApi: { listar: vi.fn() },
}));

const cuentas = [
  { id: 1, codigo: "1101", nombre: "Caja", activa: true, cuentaPadreId: 3 },
  { id: 2, codigo: "4101", nombre: "Ventas", activa: true, cuentaPadreId: null },
  { id: 3, codigo: "11", nombre: "Activo corriente", activa: true, cuentaPadreId: null },
  { id: 4, codigo: "9999", nombre: "Vieja", activa: false, cuentaPadreId: null },
];
const centros = [{ id: 7, codigo: "CC1", nombre: "Ventas" }];
const periodos = [
  { id: 10, nombre: "Enero 2025", estado: "Abierto" },
  { id: 11, nombre: "Diciembre 2024", estado: "Cerrado" },
];

async function renderCargado() {
  render(<RegistrarAsiento />);
  await screen.findAllByRole("option", { name: "1101 - Caja" });
  await screen.findAllByRole("option", { name: "CC1 - Ventas" });
  await screen.findByRole("option", { name: "Enero 2025" });
}

function escribir(etiqueta, indice, valor) {
  fireEvent.change(screen.getAllByLabelText(etiqueta)[indice], { target: { value: valor } });
}

describe("RegistrarAsiento", () => {
  beforeEach(() => {
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
    centrosCostoApi.listar.mockResolvedValue({ data: centros });
    periodosApi.listar.mockResolvedValue({ data: periodos });
    asientosApi.crear.mockReset();
  });

  it("solo ofrece cuentas hoja activas y periodos abiertos", async () => {
    await renderCargado();

    expect(screen.queryByRole("option", { name: "11 - Activo corriente" })).toBeNull();
    expect(screen.queryByRole("option", { name: "9999 - Vieja" })).toBeNull();
    expect(screen.getAllByRole("option", { name: "4101 - Ventas" }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("option", { name: "Diciembre 2024" })).toBeNull();
  });

  it("muestra la diferencia mientras débitos y créditos no cuadran y cuadra al igualarlos", async () => {
    await renderCargado();

    escribir("Débito", 0, "100");
    expect(screen.getByText(/Débitos:/)).toHaveAttribute("data-balance", "off");
    expect(screen.getByText(/Diferencia: 100[.,]00/)).toBeInTheDocument();

    escribir("Crédito", 1, "100");
    expect(screen.getByText(/Débitos:/)).toHaveAttribute("data-balance", "ok");
    expect(screen.getByText(/Cuadrado/)).toBeInTheDocument();
  });

  it("redondea a centavos para no marcar descuadre por punto flotante", async () => {
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));

    escribir("Débito", 0, "0.1");
    escribir("Débito", 1, "0.2");
    escribir("Crédito", 2, "0.3");

    expect(screen.getByText(/Débitos:/)).toHaveAttribute("data-balance", "ok");
  });

  it("limpia el crédito de una línea al escribir un débito mayor a cero", async () => {
    await renderCargado();

    escribir("Crédito", 0, "50");
    escribir("Débito", 0, "20");

    expect(screen.getAllByLabelText("Crédito")[0]).toHaveValue(null);
    expect(screen.getAllByLabelText("Débito")[0]).toHaveValue(20);
  });

  it("no permite quitar líneas por debajo de dos", async () => {
    await renderCargado();

    screen.getAllByRole("button", { name: "Quitar" }).forEach((boton) => expect(boton).toBeDisabled());
    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));
    screen.getAllByRole("button", { name: "Quitar" }).forEach((boton) => expect(boton).toBeEnabled());
  });

  it("mantiene deshabilitado el envío hasta que el asiento está completo y cuadrado", async () => {
    await renderCargado();
    expect(screen.getByRole("button", { name: "Registrar asiento" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Número de asiento"), { target: { value: "A-1" } });
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "10" } });
    escribir("Cuenta", 0, "1");
    escribir("Cuenta", 1, "2");
    escribir("Débito", 0, "100");
    expect(screen.getByRole("button", { name: "Registrar asiento" })).toBeDisabled();

    escribir("Crédito", 1, "100");
    expect(screen.getByRole("button", { name: "Registrar asiento" })).toBeEnabled();
  });

  it("envía el asiento con montos numéricos y muestra confirmación", async () => {
    asientosApi.crear.mockResolvedValue({ data: {} });
    await renderCargado();
    fireEvent.change(screen.getByLabelText("Número de asiento"), { target: { value: "A-1" } });
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "10" } });
    escribir("Cuenta", 0, "1");
    escribir("Cuenta", 1, "2");
    escribir("Débito", 0, "100");
    escribir("Crédito", 1, "100");

    fireEvent.click(screen.getByRole("button", { name: "Registrar asiento" }));

    await waitFor(() => expect(asientosApi.crear).toHaveBeenCalledTimes(1));
    expect(asientosApi.crear).toHaveBeenCalledWith(
      expect.objectContaining({
        numero: "A-1",
        periodoId: 10,
        monto: 100,
        estado: "Confirmado",
        lineas: [
          { cuentaId: 1, centroCostoId: null, debito: 100, credito: 0 },
          { cuentaId: 2, centroCostoId: null, debito: 0, credito: 100 },
        ],
      })
    );
    expect(await screen.findByText("Asiento A-1 registrado correctamente.")).toBeInTheDocument();
  });

  it("muestra el error del servidor cuando el registro falla", async () => {
    asientosApi.crear.mockRejectedValue({ response: { data: { error: "Periodo cerrado." } } });
    await renderCargado();
    fireEvent.change(screen.getByLabelText("Número de asiento"), { target: { value: "A-2" } });
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "10" } });
    escribir("Cuenta", 0, "1");
    escribir("Cuenta", 1, "2");
    escribir("Débito", 0, "10");
    escribir("Crédito", 1, "10");

    fireEvent.click(screen.getByRole("button", { name: "Registrar asiento" }));

    expect(await screen.findByText("Periodo cerrado.")).toBeInTheDocument();
  });
});
