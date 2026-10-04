import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi, contrapartesApi, cuentasApi, periodosApi } from "../../services/api";
import GestionCuentasPorCobrarPagar from "./GestionCuentasPorCobrarPagar";

vi.mock("../../services/api", () => ({
  centrosCostoApi: { listar: vi.fn() },
  contrapartesApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  periodosApi: { listar: vi.fn() },
}));

const cuentas = [
  { id: 1, codigo: "1101", nombre: "Clientes", tipo: "Activo", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
  { id: 2, codigo: "4101", nombre: "Ventas", tipo: "Ingreso", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
];
const contrapartes = [{ id: 5, nombre: "ACME" }, { id: 6, nombre: "Otra" }];
const periodos = [{ id: 10, nombre: "Enero 2025", estado: "Abierto" }];
const facturas = [
  { id: 1, numero: "F-1", clienteId: 5, clienteNombre: "ACME", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoTotal: 100, saldoPendiente: 100, estado: "Vigente" },
  { id: 2, numero: "F-2", clienteId: 6, clienteNombre: "Otra", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoTotal: 80, saldoPendiente: 80, estado: "Vigente" },
  { id: 3, numero: "F-3", clienteId: 5, clienteNombre: "ACME", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoTotal: 50, saldoPendiente: 50, estado: "Anulado" },
  { id: 4, numero: "F-4", clienteId: 5, clienteNombre: "ACME", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoTotal: 30, saldoPendiente: 0, estado: "Vigente" },
];

let api;

function crearConfig() {
  api = {
    listarFacturas: vi.fn().mockResolvedValue({ data: facturas }),
    listarPagos: vi.fn().mockResolvedValue({ data: [] }),
    crearFactura: vi.fn().mockResolvedValue({ data: {} }),
    crearPago: vi.fn().mockResolvedValue({ data: {} }),
  };
  return {
    titulo: "Cuentas por cobrar",
    tipoContraparte: "Cliente",
    etiquetaContraparte: "Cliente",
    campoContraparteId: "clienteId",
    campoContraparteNombre: "clienteNombre",
    api,
    filtroCuentaControl: (c) => c.tipo === "Activo" && c.naturaleza === "Deudora",
    etiquetaCuentaControl: "Cuenta de control (CxC)",
    etiquetaCuentaLinea: "Cuenta (ingreso)",
  };
}

async function renderCargado() {
  render(<GestionCuentasPorCobrarPagar config={crearConfig()} />);
  await screen.findByRole("option", { name: "ACME" });
  await screen.findAllByRole("option", { name: "4101 - Ventas" });
  await screen.findByRole("option", { name: "Enero 2025" });
  await screen.findByText("F-1");
}

describe("GestionCuentasPorCobrarPagar", () => {
  beforeEach(() => {
    contrapartesApi.listar.mockResolvedValue({ data: contrapartes });
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
    centrosCostoApi.listar.mockResolvedValue({ data: [] });
    periodosApi.listar.mockResolvedValue({ data: periodos });
  });

  it("calcula el total de la factura con impuesto y suma varias líneas", async () => {
    await renderCargado();
    expect(screen.getByText(/Total de la factura: 0[.,]00/)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Cantidad"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Precio unitario"), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("Porcentaje de impuesto"), { target: { value: "12" } });
    expect(screen.getByText(/Total de la factura: 224[.,]00/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));
    fireEvent.change(screen.getAllByLabelText("Precio unitario")[1], { target: { value: "30" } });
    expect(screen.getByText(/Total de la factura: 254[.,]00/)).toBeInTheDocument();
  });

  it("no permite quitar la única línea", async () => {
    await renderCargado();

    expect(screen.getByRole("button", { name: "Quitar" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));
    screen.getAllByRole("button", { name: "Quitar" }).forEach((boton) => expect(boton).toBeEnabled());
  });

  it("ofrece como cuenta de control solo las que cumplen el filtro del config", async () => {
    await renderCargado();

    const control = screen.getByLabelText("Cuenta de control (CxC)");
    expect(within(control).getByRole("option", { name: "1101 - Clientes" })).toBeInTheDocument();
    expect(within(control).queryByRole("option", { name: "4101 - Ventas" })).toBeNull();
  });

  it("envía la factura con la contraparte dinámica y montos numéricos", async () => {
    await renderCargado();
    expect(screen.getByRole("button", { name: "Registrar factura" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Número de factura"), { target: { value: "F-9" } });
    fireEvent.change(screen.getByLabelText("Cliente"), { target: { value: "5" } });
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "10" } });
    fireEvent.change(screen.getByLabelText("Cuenta de control (CxC)"), { target: { value: "1" } });
    fireEvent.change(screen.getByLabelText("Cuenta de la línea"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Cantidad"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Precio unitario"), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("Porcentaje de impuesto"), { target: { value: "12" } });
    expect(screen.getByRole("button", { name: "Registrar factura" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Registrar factura" }));

    await waitFor(() => expect(api.crearFactura).toHaveBeenCalledTimes(1));
    expect(api.crearFactura).toHaveBeenCalledWith({
      numero: "F-9",
      tipoDocumento: "Factura",
      clienteId: 5,
      fecha: expect.any(String),
      fechaVencimiento: expect.any(String),
      periodoId: 10,
      cuentaControlId: 1,
      lineas: [
        { descripcion: null, cantidad: 2, precioUnitario: 100, porcentajeImpuesto: 12, centroCostoId: null, cuentaContableId: 2 },
      ],
    });
  });

  it("en pagos solo lista facturas vigentes con saldo de la contraparte elegida y envía las aplicaciones", async () => {
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "Pagos" }));

    fireEvent.change(await screen.findByLabelText("Cliente"), { target: { value: "5" } });

    expect(screen.getByLabelText("Monto a aplicar a F-1")).toBeInTheDocument();
    expect(screen.queryByLabelText("Monto a aplicar a F-2")).toBeNull();
    expect(screen.queryByLabelText("Monto a aplicar a F-3")).toBeNull();
    expect(screen.queryByLabelText("Monto a aplicar a F-4")).toBeNull();

    fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Efectivo" } });
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Monto a aplicar a F-1"), { target: { value: "40" } });
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
    expect(api.crearPago).toHaveBeenCalledWith({
      clienteId: 5,
      fecha: expect.any(String),
      metodoPago: "Efectivo",
      referenciaBancaria: null,
      aplicaciones: [{ documentoId: 1, montoAplicado: 40 }],
    });
    await waitFor(() => expect(api.listarFacturas).toHaveBeenCalledTimes(2));
  });
});
