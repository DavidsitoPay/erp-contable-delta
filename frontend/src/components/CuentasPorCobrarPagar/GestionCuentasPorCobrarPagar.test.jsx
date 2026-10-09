import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi, contrapartesApi, cuentasApi, cuentasBancariasApi, impuestosApi, periodosApi } from "../../services/api";
import GestionCuentasPorCobrarPagar from "./GestionCuentasPorCobrarPagar";

vi.mock("../../services/api", () => ({
  centrosCostoApi: { listar: vi.fn() },
  contrapartesApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  cuentasBancariasApi: { listar: vi.fn() },
  impuestosApi: { listar: vi.fn() },
  periodosApi: { listar: vi.fn() },
}));

const UUID = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
const cuentas = [
  { id: 1, codigo: "1101", nombre: "Clientes", tipo: "Activo", naturaleza: "Deudora", activa: true, cuentaPadreId: null },
  { id: 2, codigo: "4101", nombre: "Ventas", tipo: "Ingreso", naturaleza: "Acreedora", activa: true, cuentaPadreId: null },
];
const cuentasBancarias = [
  { id: 7, banco: "BAC", numero: "123", activa: true },
  { id: 8, banco: "Viejo", numero: "000", activa: false },
];
const contrapartes = [{ id: 5, nombre: "ACME", regimenIva: "GENERAL" }, { id: 6, nombre: "Otra", regimenIva: "GENERAL" }];
const periodos = [{ id: 10, nombre: "Enero 2025", estado: "Abierto" }];
const impuestos = [{ id: 1, codigo: "IVA_GENERAL", nombre: "IVA general 12 %", tipo: "IVA_GENERAL", tasa: 12, generaCredito: true }];
const base = { tipoDocumento: "Factura", fecha: "2025-01-10", fechaVencimiento: "2025-02-10", montoBase: 0, montoIva: 0, dteSerie: null, dteNumero: null, calculoLegado: false };
const facturas = [
  { ...base, id: 1, numero: "F-1", clienteId: 5, clienteNombre: "ACME", montoTotal: 100, saldoPendiente: 100, estado: "Vigente" },
  { ...base, id: 2, numero: "F-2", clienteId: 6, clienteNombre: "Otra", montoTotal: 80, saldoPendiente: 80, estado: "Vigente" },
  { ...base, id: 3, numero: "F-3", clienteId: 5, clienteNombre: "ACME", montoTotal: 50, saldoPendiente: 50, estado: "Anulado" },
  { ...base, id: 4, numero: "F-4", clienteId: 5, clienteNombre: "ACME", montoTotal: 30, saldoPendiente: 0, estado: "Vigente" },
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
    aplicaImpuestoA: "VENTAS",
    etiquetaIva: "IVA débito",
    dteObligatorio: true,
    tipoBienDefecto: "SERVICIO",
  };
}

async function renderCargado() {
  render(<GestionCuentasPorCobrarPagar config={crearConfig()} />);
  await screen.findByRole("option", { name: "ACME" });
  await screen.findAllByRole("option", { name: "4101 - Ventas" });
  await screen.findByRole("option", { name: "Enero 2025" });
  await screen.findByRole("option", { name: "IVA general 12 %" });
  await screen.findByText("F-1");
}

function iniciarSesionComo(perfil) {
  localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
}

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

describe("GestionCuentasPorCobrarPagar", () => {
  beforeEach(() => {
    iniciarSesionComo("Contador");
    contrapartesApi.listar.mockResolvedValue({ data: contrapartes });
    cuentasApi.listar.mockResolvedValue({ data: cuentas });
    centrosCostoApi.listar.mockResolvedValue({ data: [] });
    periodosApi.listar.mockResolvedValue({ data: periodos });
    impuestosApi.listar.mockResolvedValue({ data: impuestos });
    cuentasBancariasApi.listar.mockClear();
    cuentasBancariasApi.listar.mockResolvedValue({ data: cuentasBancarias });
  });

  it("carga los catálogos y muestra las facturas con base, IVA y DTE", async () => {
    await renderCargado();

    expect(screen.getByText("Cuentas por cobrar")).toBeInTheDocument();
    expect(screen.getByText("Facturas registradas")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "DTE" })).toBeInTheDocument();
  });

  it("registra la factura con la contraparte dinámica, el impuesto y el DTE y recarga la lista", async () => {
    await renderCargado();
    expect(screen.getByRole("button", { name: "Registrar factura" })).toBeDisabled();

    escribir("Número de factura", "F-9");
    escribir("Cliente", "5");
    escribir("Periodo", "10");
    escribir("Cuenta de control (CxC)", "1");
    escribir("Cuenta de la línea", "2");
    escribir("Cantidad", "2");
    escribir("Precio unitario", "100");
    escribir("UUID de autorización", UUID);
    escribir("Serie", "A1B2");
    escribir("Número", "123");
    escribir("Fecha y hora de certificación", "2026-10-09T10:15");
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
      dteUuid: UUID,
      dteSerie: "A1B2",
      dteNumero: "123",
      dteFechaCertificacion: expect.stringMatching(/^2026-10-09T10:15:00[+-]\d{2}:\d{2}$/),
      lineas: [
        { descripcion: null, cantidad: 2, precioUnitario: 100, impuestoId: 1, tipoBienServicio: "SERVICIO", centroCostoId: null, cuentaContableId: 2 },
      ],
    });
    await waitFor(() => expect(api.listarFacturas).toHaveBeenCalledTimes(2));
  });

  it("en pagos exige cuenta bancaria, lista facturas vigentes con saldo y envía cuentaBancariaId y aplicaciones", async () => {
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "Pagos" }));

    fireEvent.change(await screen.findByLabelText("Cliente"), { target: { value: "5" } });

    expect(screen.getByLabelText("Monto a aplicar a F-1")).toBeInTheDocument();
    expect(screen.queryByLabelText("Monto a aplicar a F-2")).toBeNull();
    expect(screen.queryByLabelText("Monto a aplicar a F-3")).toBeNull();
    expect(screen.queryByLabelText("Monto a aplicar a F-4")).toBeNull();

    const cuenta = screen.getByLabelText("Cuenta bancaria");
    await within(cuenta).findByRole("option", { name: "BAC 123" });
    expect(within(cuenta).queryByRole("option", { name: "Viejo 000" })).toBeNull();

    fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Efectivo" } });
    fireEvent.change(screen.getByLabelText("Monto a aplicar a F-1"), { target: { value: "40" } });
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();

    fireEvent.change(cuenta, { target: { value: "7" } });
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
    expect(api.crearPago).toHaveBeenCalledWith({
      clienteId: 5,
      cuentaBancariaId: 7,
      fecha: expect.any(String),
      metodoPago: "Efectivo",
      referenciaBancaria: null,
      aplicaciones: [{ documentoId: 1, montoAplicado: 40 }],
    });
    await waitFor(() => expect(api.listarFacturas).toHaveBeenCalledTimes(2));
  });

  it("muestra el error del servidor cuando el pago falla", async () => {
    await renderCargado();
    api.crearPago.mockRejectedValueOnce({ response: { data: { error: "El pago excede el saldo." } } });
    fireEvent.click(screen.getByRole("button", { name: "Pagos" }));
    fireEvent.change(await screen.findByLabelText("Cliente"), { target: { value: "5" } });
    await within(screen.getByLabelText("Cuenta bancaria")).findByRole("option", { name: "BAC 123" });
    fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Efectivo" } });
    fireEvent.change(screen.getByLabelText("Monto a aplicar a F-1"), { target: { value: "40" } });

    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    expect(await screen.findByText("El pago excede el saldo.")).toBeInTheDocument();
  });

  it("un vendedor no ve el formulario de pagos ni consulta cuentas bancarias, pero sí la lista de pagos", async () => {
    iniciarSesionComo("Vendedor");
    await renderCargado();
    fireEvent.click(screen.getByRole("button", { name: "Pagos" }));

    expect(await screen.findByText("Tu perfil no puede registrar cobros ni pagos.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Cuenta bancaria")).toBeNull();
    expect(screen.queryByRole("button", { name: "Registrar pago" })).toBeNull();
    expect(screen.getByText("Pagos registrados")).toBeInTheDocument();
    expect(cuentasBancariasApi.listar).not.toHaveBeenCalled();
  });

  it("un vendedor sí ve el formulario de factura con los datos fiscales de venta", async () => {
    iniciarSesionComo("Vendedor");
    await renderCargado();

    expect(screen.getByLabelText("Impuesto de la línea")).toBeInTheDocument();
    expect(screen.getByText("Datos del DTE")).toBeInTheDocument();
    expect(impuestosApi.listar).toHaveBeenCalledWith(expect.objectContaining({ aplicaA: "VENTAS" }));
  });
});
