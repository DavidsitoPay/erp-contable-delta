import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centrosCostoApi, contrapartesApi, cuentasApi, cuentasBancariasApi, impuestosApi, parametrosRetencionApi, periodosApi, tiposCambioApi } from "../../services/api";
import { formatoDiferencial, formatoMoneda, formatoQuetzales } from "../../utils/formato";
import GestionCuentasPorCobrarPagar from "./GestionCuentasPorCobrarPagar";

vi.mock("../../services/api", () => ({
  centrosCostoApi: { listar: vi.fn() },
  contrapartesApi: { listar: vi.fn() },
  cuentasApi: { listar: vi.fn() },
  cuentasBancariasApi: { listar: vi.fn() },
  impuestosApi: { listar: vi.fn() },
  parametrosRetencionApi: { listar: vi.fn().mockResolvedValue({ data: [] }) },
  periodosApi: { listar: vi.fn() },
  tiposCambioApi: { consultar: vi.fn() },
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

const facturasCxp = [
  { ...base, id: 21, numero: "P-1", proveedorId: 5, proveedorNombre: "ACME", montoTotal: 100, saldoPendiente: 90, montoRetencionIsr: 10, revisionRetencion: "PENDIENTE", revisionMotivo: null, estado: "Vigente" },
  { ...base, id: 22, numero: "P-2", proveedorId: 5, proveedorNombre: "ACME", montoTotal: 50, saldoPendiente: 50, montoRetencionIsr: 0, revisionRetencion: "NINGUNA", revisionMotivo: null, estado: "Vigente" },
];

function crearConfigCxp() {
  api = {
    listarFacturas: vi.fn().mockResolvedValue({ data: facturasCxp }),
    listarPagos: vi.fn().mockResolvedValue({ data: [] }),
    crearFactura: vi.fn().mockResolvedValue({ data: {} }),
    crearPago: vi.fn().mockResolvedValue({ data: {} }),
    retencionPrevia: vi.fn().mockResolvedValue({ data: null }),
    resolverRevisionRetencion: vi.fn().mockResolvedValue({ data: {} }),
  };
  return {
    titulo: "Cuentas por pagar",
    tipoContraparte: "Proveedor",
    etiquetaContraparte: "Proveedor",
    campoContraparteId: "proveedorId",
    campoContraparteNombre: "proveedorNombre",
    api,
    filtroCuentaControl: (c) => c.tipo === "Pasivo" && c.naturaleza === "Acreedora",
    etiquetaCuentaControl: "Cuenta de control (CxP)",
    etiquetaCuentaLinea: "Cuenta (gasto/activo)",
    aplicaImpuestoA: "COMPRAS",
    etiquetaIva: "IVA crédito",
    dteObligatorio: false,
    tipoBienDefecto: "BIEN",
    retencionIsr: true,
  };
}

async function renderCargado(config = crearConfig(), primeraFactura = "F-1") {
  render(<GestionCuentasPorCobrarPagar config={config} />);
  await screen.findByRole("option", { name: "ACME" });
  await screen.findAllByRole("option", { name: "4101 - Ventas" });
  await screen.findByRole("option", { name: "Enero 2025" });
  await screen.findByRole("option", { name: "IVA general 12 %" });
  await screen.findByText(primeraFactura);
}

function iniciarSesionComo(perfil) {
  localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
}

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

async function prepararCobro(config) {
  await renderCargado(config);
  fireEvent.click(screen.getByRole("button", { name: "Pagos" }));
  fireEvent.change(await screen.findByLabelText("Cliente"), { target: { value: "5" } });
  const cuenta = screen.getByLabelText("Cuenta bancaria");
  await within(cuenta).findByRole("option", { name: "BAC 123" });
  fireEvent.change(cuenta, { target: { value: "7" } });
  fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Transferencia" } });
  fireEvent.change(screen.getByLabelText("Monto a aplicar a F-1"), { target: { value: "100" } });
}

function configConRetencionIva(sugerencia) {
  const config = crearConfig();
  api.retencionIvaSugerida = vi.fn().mockResolvedValue({ data: sugerencia });
  return { ...config, retencionIvaCobro: true };
}

const sugerenciaIva = { aplica: true, motivo: null, clienteAgente: true, ivaRetenible: 12, sugerido: 12, yaRetenida: false };

const tiposRentaNr = [
  { id: 3, codigo: "SERV", nombre: "Servicios", tasaBase: 15 },
  { id: 4, codigo: "OTRO", nombre: "Otros", tasaBase: 10 },
];
const previaNr = { aplica: true, lineas: [], totalBaseGtq: 40, totalRetencionGtq: 6, netoBancoGtq: 34, advertencias: [] };

async function prepararPagoNoResidente(retencionPreviaPago, documentos = facturasCxp) {
  const config = crearConfigCxp();
  config.api.listarFacturas = vi.fn().mockResolvedValue({ data: documentos });
  contrapartesApi.listar.mockResolvedValue({ data: [{ id: 5, nombre: "ACME", regimenIva: "GENERAL", esResidente: false, tipoRentaNrCodigo: "SERV" }] });
  parametrosRetencionApi.listar.mockResolvedValue({ data: tiposRentaNr });
  api.retencionPreviaPago = retencionPreviaPago;
  await renderCargado(config, "P-1");
  fireEvent.click(screen.getByRole("button", { name: "Pagos" }));
  fireEvent.change(await screen.findByLabelText("Proveedor"), { target: { value: "5" } });
  await within(screen.getByLabelText("Cuenta bancaria")).findByRole("option", { name: "BAC 123" });
  fireEvent.change(screen.getByLabelText("Cuenta bancaria"), { target: { value: "7" } });
  fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Transferencia" } });
  fireEvent.change(screen.getByLabelText("Monto a aplicar a P-1"), { target: { value: "40" } });
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

  it("en cuentas por pagar muestra la retención, filtra las facturas por revisar y las resuelve con motivo", async () => {
    await renderCargado(crearConfigCxp(), "P-1");
    expect(screen.getByRole("columnheader", { name: "Retención ISR" })).toBeInTheDocument();
    expect(screen.getByText("P-2")).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("Solo facturas por revisar"));
    expect(screen.queryByText("P-2")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Resolver revisión de P-1" }));
    fireEvent.change(screen.getByLabelText("Motivo de la revisión de P-1"), { target: { value: " Régimen confirmado " } });
    fireEvent.click(screen.getByRole("button", { name: "Resolver revisión" }));

    await waitFor(() => expect(api.resolverRevisionRetencion).toHaveBeenCalledWith(21, "Régimen confirmado"));
    await waitFor(() => expect(api.listarFacturas).toHaveBeenCalledTimes(2));
  });

  it("al cancelar la resolución cierra el formulario de motivo", async () => {
    await renderCargado(crearConfigCxp(), "P-1");
    fireEvent.click(screen.getByRole("button", { name: "Resolver revisión de P-1" }));
    expect(screen.getByLabelText("Motivo de la revisión de P-1")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByLabelText("Motivo de la revisión de P-1")).toBeNull();
  });

  it("en cuentas por cobrar no ofrece el filtro de revisión ni la columna de retención", async () => {
    await renderCargado();

    expect(screen.queryByLabelText("Solo facturas por revisar")).toBeNull();
    expect(screen.queryByRole("columnheader", { name: "Retención ISR" })).toBeNull();
  });

  it("en cobros captura el IVA retenido por un cliente agente, lo sugiere y envía la retención con constancia", async () => {
    contrapartesApi.listar.mockResolvedValue({ data: [{ ...contrapartes[0], esAgenteRetencionIva: true }, contrapartes[1]] });
    await prepararCobro(configConRetencionIva(sugerenciaIva));

    expect(screen.getByLabelText("El cliente retuvo IVA en F-1")).toBeChecked();
    await waitFor(() => expect(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" })).toBeEnabled());
    expect(api.retencionIvaSugerida).toHaveBeenCalledWith(1);
    fireEvent.click(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" }));

    expect(screen.getByLabelText("IVA retenido en F-1")).toHaveValue(12);
    expect(screen.getByText(/^Neto a recibir en banco: 88[.,]00$/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Número de constancia de F-1"), { target: { value: "A-123" } });
    fireEvent.change(screen.getByLabelText("Fecha de constancia de F-1"), { target: { value: "2026-10-20" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
    expect(api.crearPago).toHaveBeenCalledWith({
      clienteId: 5,
      cuentaBancariaId: 7,
      fecha: expect.any(String),
      metodoPago: "Transferencia",
      referenciaBancaria: null,
      aplicaciones: [
        {
          documentoId: 1,
          montoAplicado: 100,
          retencionIva: { monto: 12, confirmaClienteNoAgente: false, constancia: { numero: "A-123", fecha: "2026-10-20" } },
        },
      ],
    });
  });

  it("un cliente no agente exige confirmar la retención de IVA antes de habilitar el cobro", async () => {
    await prepararCobro(configConRetencionIva({ ...sugerenciaIva, clienteAgente: false }));

    expect(screen.getByLabelText("El cliente retuvo IVA en F-1")).not.toBeChecked();
    fireEvent.click(screen.getByLabelText("El cliente retuvo IVA en F-1"));
    fireEvent.change(screen.getByLabelText("IVA retenido en F-1"), { target: { value: "10" } });

    expect(screen.getByText("Confirme que el cliente retuvo IVA aunque no está registrado como agente de retención.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();

    fireEvent.click(screen.getByLabelText("Confirmo la retención de IVA en F-1 de un cliente no agente"));
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() =>
      expect(api.crearPago).toHaveBeenCalledWith(
        expect.objectContaining({ aplicaciones: [{ documentoId: 1, montoAplicado: 100, retencionIva: { monto: 10, confirmaClienteNoAgente: true } }] })
      )
    );
  });

  it("no permite retener más del IVA retenible y muestra las advertencias del servidor tras registrar", async () => {
    contrapartesApi.listar.mockResolvedValue({ data: [{ ...contrapartes[0], esAgenteRetencionIva: true }, contrapartes[1]] });
    const config = configConRetencionIva(sugerenciaIva);
    api.crearPago.mockResolvedValue({ data: { advertencias: ["El monto de IVA difiere del sugerido."] } });
    await prepararCobro(config);
    await waitFor(() => expect(screen.getByRole("button", { name: "Sugerir IVA retenido para F-1" })).toBeEnabled());

    fireEvent.change(screen.getByLabelText("IVA retenido en F-1"), { target: { value: "20" } });
    expect(screen.getByText(`El IVA retenido no puede exceder el IVA retenible de la factura (${formatoMoneda.format(12)}).`)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();

    fireEvent.change(screen.getByLabelText("IVA retenido en F-1"), { target: { value: "10" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    expect(await screen.findByText("El monto de IVA difiere del sugerido.")).toBeInTheDocument();
  });

  it("si la factura ya tiene IVA retenido no envía retención aunque el cliente sea agente", async () => {
    contrapartesApi.listar.mockResolvedValue({ data: [{ ...contrapartes[0], esAgenteRetencionIva: true }, contrapartes[1]] });
    await prepararCobro(configConRetencionIva({ ...sugerenciaIva, yaRetenida: true }));

    expect(await screen.findByText("La factura ya tiene IVA retenido registrado.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
    expect(api.crearPago.mock.calls[0][0].aplicaciones).toEqual([{ documentoId: 1, montoAplicado: 100 }]);
  });

  it("a un proveedor no residente muestra el tipo de renta por factura, consulta la previa y registra el pago con el tipo elegido", async () => {
    await prepararPagoNoResidente(vi.fn().mockResolvedValue({ data: previaNr }));

    expect(screen.getByRole("combobox", { name: "Tipo de renta de P-1" })).toHaveValue("SERV");
    await waitFor(() =>
      expect(api.retencionPreviaPago).toHaveBeenCalledWith({
        proveedorId: 5,
        fecha: expect.any(String),
        aplicaciones: [{ documentoId: 21, montoAplicado: 40, tipoRentaCodigo: "SERV" }],
      })
    );
    expect(await screen.findByText(`Neto al banco: ${formatoMoneda.format(34)}`)).toBeInTheDocument();

    fireEvent.change(screen.getByRole("combobox", { name: "Tipo de renta de P-1" }), { target: { value: "OTRO" } });
    await waitFor(() =>
      expect(api.retencionPreviaPago).toHaveBeenLastCalledWith({
        proveedorId: 5,
        fecha: expect.any(String),
        aplicaciones: [{ documentoId: 21, montoAplicado: 40, tipoRentaCodigo: "OTRO" }],
      })
    );

    fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

    await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
    expect(api.crearPago).toHaveBeenCalledWith({
      proveedorId: 5,
      cuentaBancariaId: 7,
      fecha: expect.any(String),
      metodoPago: "Transferencia",
      referenciaBancaria: null,
      aplicaciones: [{ documentoId: 21, montoAplicado: 40, tipoRentaCodigo: "OTRO" }],
    });
  });

  it("un 409 de la previa muestra el motivo y impide registrar el pago a un proveedor no residente", async () => {
    await prepararPagoNoResidente(vi.fn().mockRejectedValue({ response: { status: 409, data: { error: "Falta el tipo de renta." } } }));

    expect(await screen.findByText("Falta el tipo de renta.")).toHaveClass("error-chip");
    expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();
    expect(api.crearPago).not.toHaveBeenCalled();
  });

  it("a un proveedor no residente no ofrece tipo de renta para una nota de crédito aplicada", async () => {
    const notaCredito = { ...base, id: 23, numero: "NC-1", tipoDocumento: "NotaCredito", proveedorId: 5, proveedorNombre: "ACME", montoTotal: 20, saldoPendiente: 20, montoRetencionIsr: 0, revisionRetencion: "NINGUNA", revisionMotivo: null, estado: "Vigente" };
    await prepararPagoNoResidente(vi.fn().mockResolvedValue({ data: previaNr }), [...facturasCxp, notaCredito]);
    fireEvent.change(screen.getByLabelText("Monto a aplicar a NC-1"), { target: { value: "20" } });

    await waitFor(() =>
      expect(api.retencionPreviaPago).toHaveBeenLastCalledWith(
        expect.objectContaining({ aplicaciones: expect.arrayContaining([expect.objectContaining({ documentoId: 23 })]) })
      )
    );
    expect(screen.getByRole("combobox", { name: "Tipo de renta de P-1" })).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Tipo de renta de NC-1" })).toBeNull();
  });

  describe("cobros en moneda extranjera", () => {
    const cuentaUsd = { id: 9, banco: "BAC", numero: "USD-1", activa: true, monedaCodigo: "USD" };
    const facturaUsd = { ...base, id: 7, numero: "F-USD", clienteId: 5, clienteNombre: "ACME", moneda: "USD", montoTotal: 8512, montoTotalOriginal: 1120, saldoPendiente: 1120, saldoPendienteGtq: 8512, tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat", tipoCambioArrastrado: false, estado: "Vigente" };
    const tasa77 = { data: { disponible: true, tipoCambio: { id: 2, tasa: 7.7, fuente: "Banguat", arrastrado: false, fechaReferencia: "2026-11-10" }, propuesta: null, mensaje: null } };

    function configCobroUsd(config = crearConfig(), listado = [facturaUsd]) {
      config.esCobro = true;
      api.listarFacturas.mockResolvedValue({ data: listado });
      cuentasBancariasApi.listar.mockResolvedValue({ data: [...cuentasBancarias, cuentaUsd] });
      tiposCambioApi.consultar.mockResolvedValue(tasa77);
      return config;
    }

    async function iniciarCobro(config, { cuentaId, factura = "F-USD", monto = "400" }) {
      await renderCargado(config, factura);
      fireEvent.click(screen.getByRole("button", { name: "Pagos" }));
      fireEvent.change(await screen.findByLabelText("Cliente"), { target: { value: "5" } });
      const cuenta = screen.getByLabelText("Cuenta bancaria");
      await within(cuenta).findByRole("option", { name: cuentaId === "9" ? /USD-1/ : /BAC 123/ });
      fireEvent.change(cuenta, { target: { value: cuentaId } });
      fireEvent.change(screen.getByLabelText("Método de pago"), { target: { value: "Transferencia" } });
      fireEvent.change(screen.getByLabelText(`Monto a aplicar a ${factura}`), { target: { value: monto } });
    }

    it("con cuenta GTQ pide el efectivo en quetzales y calcula el diferencial estimado", async () => {
      const config = configCobroUsd();
      await iniciarCobro(config, { cuentaId: "7" });

      expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();
      fireEvent.change(screen.getByLabelText("Efectivo en Q de F-USD"), { target: { value: "3080" } });

      expect(screen.getByText(`Diferencial estimado: ${formatoDiferencial(40)} (el sistema recalcula)`)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled();

      fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

      await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
      expect(api.crearPago).toHaveBeenCalledWith(
        expect.objectContaining({ cuentaBancariaId: 7, aplicaciones: [{ documentoId: 7, montoAplicado: 400, valorEfectivoGtq: 3080 }] })
      );
    });

    it("con cuenta USD valora con el tipo de cambio de la fecha y no pide efectivo", async () => {
      const config = configCobroUsd();
      await iniciarCobro(config, { cuentaId: "9" });

      expect(await screen.findByText("Tipo de cambio: 7.700000 (Banguat)")).toBeInTheDocument();
      expect(tiposCambioApi.consultar).toHaveBeenCalledWith("USD", expect.any(String));
      expect(screen.getByRole("columnheader", { name: "Valor en Q" })).toBeInTheDocument();
      expect(screen.getByText(formatoQuetzales(3080))).toBeInTheDocument();
      expect(screen.getByText(`Diferencial estimado: ${formatoDiferencial(40)} (el sistema recalcula)`)).toBeInTheDocument();
      expect(screen.queryByLabelText("Efectivo en Q de F-USD")).toBeNull();

      await waitFor(() => expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled());
      fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

      await waitFor(() => expect(api.crearPago).toHaveBeenCalledTimes(1));
      expect(api.crearPago.mock.calls[0][0].aplicaciones[0]).not.toHaveProperty("valorEfectivoGtq");
      expect(api.crearPago.mock.calls[0][0].cuentaBancariaId).toBe(9);
    });

    it("bloquea el pago de una factura en quetzales con cuenta en dólares (RN-27)", async () => {
      const config = configCobroUsd(crearConfig(), [facturaUsd, facturas[0]]);
      await iniciarCobro(config, { cuentaId: "9", factura: "F-1", monto: "100" });

      expect(await screen.findByText(/^RN-27/)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();
    });

    it("sin tasa de Banguat mantiene bloqueado el pago en moneda extranjera", async () => {
      const config = configCobroUsd();
      tiposCambioApi.consultar.mockResolvedValue({ data: { disponible: false, tipoCambio: null, propuesta: null, mensaje: "Banguat no respondió; capture la tasa manual o reintente." } });
      await iniciarCobro(config, { cuentaId: "9" });

      expect(await screen.findByText("Banguat no respondió; capture la tasa manual o reintente.")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Registrar pago" })).toBeDisabled();
    });

    it("muestra el 409 de cuenta de diferencial cambiario tal cual", async () => {
      const config = configCobroUsd();
      api.crearPago.mockRejectedValue({ response: { data: { error: "Falta configurar la cuenta de ganancia cambiaria realizada en Catálogo > Configuración fiscal." } } });
      await iniciarCobro(config, { cuentaId: "9" });
      await waitFor(() => expect(screen.getByRole("button", { name: "Registrar pago" })).toBeEnabled());

      fireEvent.click(screen.getByRole("button", { name: "Registrar pago" }));

      expect(await screen.findByText("Falta configurar la cuenta de ganancia cambiaria realizada en Catálogo > Configuración fiscal.")).toBeInTheDocument();
    });

    it("no ofrece la retención de IVA para una factura en moneda extranjera", async () => {
      const config = configCobroUsd(configConRetencionIva(sugerenciaIva));
      await iniciarCobro(config, { cuentaId: "9" });
      await screen.findByText("Tipo de cambio: 7.700000 (Banguat)");

      expect(screen.queryByText(/El cliente retuvo IVA en F-USD/)).toBeNull();
      expect(api.retencionIvaSugerida).not.toHaveBeenCalled();
    });
  });
});
