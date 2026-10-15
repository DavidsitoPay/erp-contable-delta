import { configure, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { impuestosApi, tiposCambioApi } from "../../services/api";
import { AVISO_DTE } from "../../utils/fiscal";
import { formatoImporte, formatoMoneda, formatoQuetzales, hoyIso } from "../../utils/formato";
import FormularioFactura from "./FormularioFactura";

vi.mock("../../services/api", () => ({ impuestosApi: { listar: vi.fn() }, tiposCambioApi: { consultar: vi.fn() } }));

const UUID = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
const ERROR_CUENTA_IVA = "Falta configurar la cuenta de IVA débito fiscal en Catálogo > Configuración fiscal. Solicite al Administrador del sistema que la configure antes de registrar facturas con IVA.";

const impuestos = [
  { id: 1, codigo: "IVA_GENERAL", nombre: "IVA general 12 %", tipo: "IVA_GENERAL", tasa: 12, generaCredito: true },
  { id: 2, codigo: "EXENTO_EXPORTACION", nombre: "Exento: exportación", tipo: "EXENTO", tasa: 0, generaCredito: false },
  { id: 3, codigo: "PEQUENO_CONTRIBUYENTE", nombre: "Pequeño contribuyente 5 %", tipo: "PEQUENO_CONTRIBUYENTE", tasa: 5, generaCredito: false },
  { id: 4, codigo: "IVA_GENERAL_SIN_CREDITO", nombre: "IVA 12 % sin crédito", tipo: "IVA_GENERAL", tasa: 12, generaCredito: false },
];
const contrapartes = [
  { id: 5, nombre: "ACME", regimenIva: "GENERAL" },
  { id: 6, nombre: "Exenta SA", regimenIva: "EXENTO" },
  { id: 7, nombre: "Pequeño SA", regimenIva: "PEQUENO_CONTRIBUYENTE" },
];
const hojas = [
  { id: 1, codigo: "1101", nombre: "Clientes" },
  { id: 2, codigo: "4101", nombre: "Ventas" },
];
const periodos = [{ id: 10, nombre: "Enero 2025" }];
const facturas = [
  { id: 1, numero: "F-1", tipoDocumento: "Factura", clienteId: 5, estado: "Vigente", saldoPendiente: 100, calculoLegado: false },
  { id: 2, numero: "F-2", tipoDocumento: "Factura", clienteId: 6, estado: "Vigente", saldoPendiente: 80, calculoLegado: false },
  { id: 3, numero: "F-3", tipoDocumento: "Factura", clienteId: 5, estado: "Anulado", saldoPendiente: 50, calculoLegado: false },
  { id: 4, numero: "F-4", tipoDocumento: "Factura", clienteId: 5, estado: "Vigente", saldoPendiente: 0, calculoLegado: false },
  { id: 5, numero: "N-1", tipoDocumento: "NotaCredito", clienteId: 5, estado: "Vigente", saldoPendiente: 0, calculoLegado: false },
  { id: 6, numero: "F-6", tipoDocumento: "Factura", clienteId: 5, estado: "Vigente", saldoPendiente: 300, calculoLegado: true },
];

function configVentas() {
  return {
    campoContraparteId: "clienteId",
    etiquetaContraparte: "Cliente",
    etiquetaCuentaControl: "Cuenta de control (CxC)",
    etiquetaCuentaLinea: "Cuenta (ingreso)",
    aplicaImpuestoA: "VENTAS",
    etiquetaIva: "IVA débito",
    dteObligatorio: true,
    tipoBienDefecto: "SERVICIO",
    api: { crearFactura: vi.fn().mockResolvedValue({ data: {} }) },
  };
}

function configCompras() {
  return {
    ...configVentas(),
    campoContraparteId: "proveedorId",
    etiquetaContraparte: "Proveedor",
    etiquetaCuentaControl: "Cuenta de control (CxP)",
    etiquetaCuentaLinea: "Cuenta (gasto/activo)",
    aplicaImpuestoA: "COMPRAS",
    etiquetaIva: "IVA crédito",
    dteObligatorio: false,
    tipoBienDefecto: "BIEN",
  };
}

const previaRetenida = {
  estado: "Retenida",
  motivo: null,
  base: 40000,
  tasaBase: 5,
  tramoLimite: 30000,
  tasaExcedente: 7,
  umbralMinimo: 2500,
  retencion: 2200,
  neto: 42600,
  parametroId: 1,
  referencial: true,
  advertencias: [],
};

function configComprasConRetencion() {
  const config = configCompras();
  config.retencionIsr = true;
  config.api.retencionPrevia = vi.fn().mockResolvedValue({ data: previaRetenida });
  return config;
}

async function renderFormulario({ config = configVentas(), listaFacturas = [] } = {}) {
  const onRegistrada = vi.fn().mockResolvedValue(undefined);
  render(
    <FormularioFactura
      config={config}
      contrapartes={contrapartes}
      hojas={hojas}
      cuentasControl={[hojas[0]]}
      centros={[]}
      periodosAbiertos={periodos}
      facturas={listaFacturas}
      onRegistrada={onRegistrada}
    />
  );
  await screen.findByRole("option", { name: "IVA general 12 %" });
  return { config, onRegistrada };
}

function escribir(etiqueta, valor, indice = 0) {
  fireEvent.change(screen.getAllByLabelText(etiqueta)[indice], { target: { value: valor } });
}

function completarCabecera(config, contraparteId = "5") {
  escribir("Número de factura", "F-9");
  escribir(config.etiquetaContraparte, contraparteId);
  escribir("Periodo", "10");
  escribir(config.etiquetaCuentaControl, "1");
  escribir("Cuenta de la línea", "2");
  escribir("Precio unitario", "100");
}

function completarDte() {
  escribir("UUID de autorización", UUID);
  escribir("Serie", "A1B2");
  escribir("Número", "123");
  escribir("Fecha y hora de certificación", "2026-10-09T10:15");
}

function textosDeOpciones(etiqueta) {
  return within(screen.getByLabelText(etiqueta)).getAllByRole("option").map((o) => o.textContent);
}

const botonRegistrar = () => screen.getByRole("button", { name: "Registrar factura" });

describe("FormularioFactura", () => {
  beforeEach(() => {
    impuestosApi.listar.mockReset();
    impuestosApi.listar.mockResolvedValue({ data: impuestos });
    tiposCambioApi.consultar.mockReset();
    tiposCambioApi.consultar.mockResolvedValue({ data: { disponible: true, tipoCambio: { id: 1, tasa: 7.6, fuente: "Banguat", arrastrado: false, fechaReferencia: "2026-10-12" }, propuesta: null, mensaje: null } });
  });

  it("consulta los impuestos vigentes a la fecha y para el ámbito, y recarga al cambiar la fecha", async () => {
    await renderFormulario();
    expect(impuestosApi.listar).toHaveBeenCalledWith({ vigenteEn: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), aplicaA: "VENTAS" });

    escribir("Fecha", "2026-03-01");
    await waitFor(() => expect(impuestosApi.listar).toHaveBeenLastCalledWith({ vigenteEn: "2026-03-01", aplicaA: "VENTAS" }));

    const llamadas = impuestosApi.listar.mock.calls.length;
    escribir("Fecha", "");
    expect(impuestosApi.listar).toHaveBeenCalledTimes(llamadas);
  });

  it("en compras consulta con el ámbito COMPRAS", async () => {
    await renderFormulario({ config: configCompras() });

    expect(impuestosApi.listar).toHaveBeenCalledWith(expect.objectContaining({ aplicaA: "COMPRAS" }));
  });

  it("muestra los impuestos permitidos según el régimen de IVA de la contraparte", async () => {
    await renderFormulario();
    expect(textosDeOpciones("Impuesto de la línea")).toHaveLength(4);

    escribir("Cliente", "5");
    expect(textosDeOpciones("Impuesto de la línea")).toEqual(["IVA general 12 %", "Exento: exportación", "IVA 12 % sin crédito"]);

    escribir("Cliente", "6");
    expect(textosDeOpciones("Impuesto de la línea")).toEqual(["Exento: exportación"]);
  });

  it("para un proveedor pequeño contribuyente ofrece su impuesto, no el IVA general, y lo preselecciona", async () => {
    await renderFormulario({ config: configCompras() });
    escribir("Proveedor", "5");
    expect(screen.getByLabelText("Impuesto de la línea")).toHaveValue("1");

    escribir("Proveedor", "7");

    expect(textosDeOpciones("Impuesto de la línea")).toEqual(["Exento: exportación", "Pequeño contribuyente 5 %"]);
    expect(screen.getByLabelText("Impuesto de la línea")).toHaveValue("3");
  });

  it("si el impuesto elegido deja de estar permitido al cambiar de contraparte vuelve al predeterminado", async () => {
    await renderFormulario();
    escribir("Cliente", "5");
    escribir("Impuesto de la línea", "4");
    expect(screen.getByLabelText("Impuesto de la línea")).toHaveValue("4");

    escribir("Cliente", "6");

    expect(screen.getByLabelText("Impuesto de la línea")).toHaveValue("2");
  });

  it("calcula la vista previa por línea con el precio con IVA incluido y los totales del documento", async () => {
    await renderFormulario();
    escribir("Cantidad", "2");
    escribir("Precio unitario", "100");

    const fila = within(screen.getAllByRole("row")[1]);
    expect(fila.getByText(/^178[.,]57$/)).toBeInTheDocument();
    expect(fila.getByText(/^21[.,]43$/)).toBeInTheDocument();
    expect(fila.getByText(/^200[.,]00$/)).toBeInTheDocument();
    expect(screen.getByText(/^Base: 178[.,]57$/)).toBeInTheDocument();
    expect(screen.getByText(/^IVA débito: 21[.,]43$/)).toBeInTheDocument();
    expect(screen.getByText(/^Total de la factura: 200[.,]00$/)).toBeInTheDocument();
    expect(screen.getByText("Vista previa; el sistema recalcula al registrar.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));
    escribir("Impuesto de la línea", "2", 1);
    escribir("Precio unitario", "50.5", 1);

    expect(screen.getByText(/^Base: 229[.,]07$/)).toBeInTheDocument();
    expect(screen.getByText(/^IVA débito: 21[.,]43$/)).toBeInTheDocument();
    expect(screen.getByText(/^Total de la factura: 250[.,]50$/)).toBeInTheDocument();
  });

  it("en compras separa el IVA acreditable del IVA a costo", async () => {
    await renderFormulario({ config: configCompras() });
    escribir("Cantidad", "2");
    escribir("Precio unitario", "100");
    escribir("Impuesto de la línea", "4");

    expect(screen.getByText(/^IVA crédito: 0[.,]00$/)).toBeInTheDocument();
    expect(screen.getByText(/^IVA a costo: 21[.,]43$/)).toBeInTheDocument();

    escribir("Impuesto de la línea", "1");

    expect(screen.getByText(/^IVA crédito: 21[.,]43$/)).toBeInTheDocument();
    expect(screen.queryByText(/IVA a costo/)).toBeNull();
  });

  it("no permite quitar la única línea", async () => {
    await renderFormulario();

    expect(screen.getByRole("button", { name: "Quitar" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "+ Agregar línea" }));
    screen.getAllByRole("button", { name: "Quitar" }).forEach((boton) => expect(boton).toBeEnabled());
  });

  it("ofrece como cuenta de control solo las recibidas del config", async () => {
    await renderFormulario();

    const control = screen.getByLabelText("Cuenta de control (CxC)");
    expect(within(control).getByRole("option", { name: "1101 - Clientes" })).toBeInTheDocument();
    expect(within(control).queryByRole("option", { name: "4101 - Ventas" })).toBeNull();
  });

  it("el tipo de bien o servicio inicia según el config y se puede cambiar por línea", async () => {
    await renderFormulario();
    expect(screen.getByLabelText("Tipo de bien o servicio")).toHaveValue("SERVICIO");
  });

  it("en compras el tipo inicia en BIEN", async () => {
    await renderFormulario({ config: configCompras() });
    expect(screen.getByLabelText("Tipo de bien o servicio")).toHaveValue("BIEN");
  });

  it("en ventas el DTE es obligatorio: muestra el aviso y habilita el envío solo con los cuatro datos válidos", async () => {
    const { config } = await renderFormulario();
    completarCabecera(config);

    expect(screen.getByText(AVISO_DTE)).toBeInTheDocument();
    expect(botonRegistrar()).toBeDisabled();

    escribir("UUID de autorización", "no-es-uuid");
    escribir("Serie", "A1B2");
    escribir("Número", "123");
    escribir("Fecha y hora de certificación", "2026-10-09T10:15");
    expect(screen.getByText("El UUID del DTE no tiene un formato válido.")).toBeInTheDocument();
    expect(botonRegistrar()).toBeDisabled();

    escribir("UUID de autorización", UUID);
    expect(botonRegistrar()).toBeEnabled();
  });

  it("en compras el DTE es opcional pero, si se informa, debe estar completo", async () => {
    const { config } = await renderFormulario({ config: configCompras() });
    completarCabecera(config);

    expect(screen.getByText("Datos del DTE del proveedor (obligatorios para tomar crédito fiscal)")).toBeInTheDocument();
    expect(botonRegistrar()).toBeEnabled();

    escribir("Serie", "A1B2");
    expect(botonRegistrar()).toBeDisabled();

    completarDte();
    expect(botonRegistrar()).toBeEnabled();
  });

  it("envía la factura de venta con impuestoId, tipo de línea y datos del DTE, y limpia el formulario", async () => {
    const { config, onRegistrada } = await renderFormulario();
    completarCabecera(config);
    escribir("Cantidad", "2");
    completarDte();

    fireEvent.click(botonRegistrar());

    await waitFor(() => expect(config.api.crearFactura).toHaveBeenCalledTimes(1));
    expect(config.api.crearFactura).toHaveBeenCalledWith({
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
    await waitFor(() => expect(onRegistrada).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByLabelText("Número de factura")).toHaveValue(""));
    expect(screen.getByLabelText("Serie")).toHaveValue("");
    expect(screen.getByLabelText("Periodo")).toHaveValue("10");
  });

  it("envía la factura de compra sin DTE con valores null y el tipo BIEN", async () => {
    const { config } = await renderFormulario({ config: configCompras() });
    completarCabecera(config);
    escribir("Impuesto de la línea", "2");

    fireEvent.click(botonRegistrar());

    await waitFor(() =>
      expect(config.api.crearFactura).toHaveBeenCalledWith(
        expect.objectContaining({
          proveedorId: 5,
          dteUuid: null,
          dteSerie: null,
          dteNumero: null,
          dteFechaCertificacion: null,
          lineas: [expect.objectContaining({ impuestoId: 2, tipoBienServicio: "BIEN" })],
        })
      )
    );
    expect(config.api.crearFactura.mock.calls[0][0]).not.toHaveProperty("documentoOrigenId");
  });

  it("muestra el error del servidor al registrar (por ejemplo cuenta fiscal sin configurar)", async () => {
    const { config } = await renderFormulario();
    config.api.crearFactura.mockRejectedValueOnce({ response: { data: { error: ERROR_CUENTA_IVA } } });
    completarCabecera(config);
    completarDte();

    fireEvent.click(botonRegistrar());

    expect(await screen.findByText(ERROR_CUENTA_IVA)).toBeInTheDocument();
    expect(screen.getByLabelText("Número de factura")).toHaveValue("F-9");
  });

  it("muestra un mensaje genérico si el registro falla sin respuesta del servidor", async () => {
    const { config } = await renderFormulario();
    config.api.crearFactura.mockRejectedValueOnce(new Error("Network Error"));
    completarCabecera(config);
    completarDte();

    fireEvent.click(botonRegistrar());

    expect(await screen.findByText("No se pudo registrar la factura.")).toBeInTheDocument();
  });

  it("si no se pueden cargar los impuestos avisa y no permite registrar", async () => {
    impuestosApi.listar.mockRejectedValue({ response: { data: { error: "Sin conexión con el catálogo." } } });
    render(
      <FormularioFactura config={configVentas()} contrapartes={contrapartes} hojas={hojas} cuentasControl={[]} centros={[]} periodosAbiertos={periodos} facturas={[]} onRegistrada={vi.fn()} />
    );

    expect(await screen.findByText("Sin conexión con el catálogo.")).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Sin impuestos vigentes" })).toBeInTheDocument();
    expect(botonRegistrar()).toBeDisabled();
  });

  it("la nota de crédito exige elegir una factura de origen vigente del mismo tercero con saldo", async () => {
    const { config } = await renderFormulario({ listaFacturas: facturas });
    expect(screen.queryByLabelText("Documento de origen")).toBeNull();

    escribir("Tipo de documento", "NotaCredito");
    escribir("Cliente", "5");

    expect(textosDeOpciones("Documento de origen")).toEqual([
      "Selecciona factura de origen",
      `F-1 — saldo ${formatoMoneda.format(100)}`,
      `F-6 (legado) — saldo ${formatoMoneda.format(300)}`,
    ]);

    completarCabecera(config);
    completarDte();
    expect(botonRegistrar()).toBeDisabled();

    escribir("Documento de origen", "1");
    expect(botonRegistrar()).toBeEnabled();

    fireEvent.click(botonRegistrar());

    await waitFor(() =>
      expect(config.api.crearFactura).toHaveBeenCalledWith(expect.objectContaining({ tipoDocumento: "NotaCredito", documentoOrigenId: 1 }))
    );
  });

  it("la nota de crédito no puede superar el saldo del documento de origen", async () => {
    const { config } = await renderFormulario({ listaFacturas: facturas });
    escribir("Tipo de documento", "NotaCredito");
    completarCabecera(config);
    completarDte();
    escribir("Documento de origen", "1");
    escribir("Precio unitario", "150");

    expect(screen.getByText("El monto de la nota de crédito excede el saldo pendiente del documento de origen.")).toBeInTheDocument();
    expect(botonRegistrar()).toBeDisabled();

    escribir("Precio unitario", "100");
    expect(screen.queryByText("El monto de la nota de crédito excede el saldo pendiente del documento de origen.")).toBeNull();
    expect(botonRegistrar()).toBeEnabled();
  });

  it("al cambiar de cliente se limpia el documento de origen elegido", async () => {
    await renderFormulario({ listaFacturas: facturas });
    escribir("Tipo de documento", "NotaCredito");
    escribir("Cliente", "5");
    escribir("Documento de origen", "1");
    expect(screen.getByLabelText("Documento de origen")).toHaveValue("1");

    escribir("Cliente", "6");

    expect(screen.getByLabelText("Documento de origen")).toHaveValue("");
    expect(textosDeOpciones("Documento de origen")).toEqual(["Selecciona factura de origen", `F-2 — saldo ${formatoMoneda.format(80)}`]);
  });
});

describe("FormularioFactura con retención de ISR", () => {
  beforeEach(() => {
    configure({ asyncUtilTimeout: 3000 });
    impuestosApi.listar.mockReset();
    impuestosApi.listar.mockResolvedValue({ data: impuestos });
  });

  afterEach(() => {
    configure({ asyncUtilTimeout: 1000 });
  });

  it("consulta la retención previa con proveedor, fecha, base y total, y muestra el resultado", async () => {
    const config = configComprasConRetencion();
    await renderFormulario({ config });
    completarCabecera(config);

    expect(await screen.findByText(`Retención de ISR: ${formatoMoneda.format(2200)}`)).toBeInTheDocument();
    expect(config.api.retencionPrevia).toHaveBeenCalledTimes(1);
    expect(config.api.retencionPrevia).toHaveBeenCalledWith({
      proveedorId: 5,
      fecha: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      montoBase: 89.29,
      montoTotal: 100,
    });
    expect(screen.getByText(`Neto a pagar al proveedor: ${formatoMoneda.format(42600)}`)).toBeInTheDocument();
    expect(screen.getByText("Referencial, validar con asesor")).toBeInTheDocument();
  });

  it("advierte cuando el proveedor no tiene régimen definido y permite registrar", async () => {
    const config = configComprasConRetencion();
    config.api.retencionPrevia.mockResolvedValue({
      data: { ...previaRetenida, estado: "PendienteRevision", motivo: "El proveedor ACME no tiene régimen de ISR definido.", retencion: 0, neto: 100 },
    });
    await renderFormulario({ config });
    completarCabecera(config);

    expect(await screen.findByText("El proveedor ACME no tiene régimen de ISR definido.")).toBeInTheDocument();
    expect(botonRegistrar()).toBeEnabled();
  });

  it("un 409 de la vista previa se muestra en el panel y bloquea el envío", async () => {
    const config = configComprasConRetencion();
    config.api.retencionPrevia.mockRejectedValue({ response: { status: 409, data: { error: "No hay parámetro de retención vigente en la fecha de la factura." } } });
    await renderFormulario({ config });
    completarCabecera(config);

    expect(await screen.findByText("No hay parámetro de retención vigente en la fecha de la factura.")).toBeInTheDocument();
    expect(botonRegistrar()).toBeDisabled();
  });

  it("el ajuste exige monto y motivo y se envía como retencionIsrAjuste", async () => {
    const config = configComprasConRetencion();
    await renderFormulario({ config });
    completarCabecera(config);
    await screen.findByText(`Retención de ISR: ${formatoMoneda.format(2200)}`);

    fireEvent.click(screen.getByLabelText("Ajustar retención"));
    expect(botonRegistrar()).toBeDisabled();
    escribir("Monto de retención ajustado", "0");
    escribir("Motivo del ajuste", "Constancia de exención");
    expect(botonRegistrar()).toBeEnabled();
    expect(screen.getByText(`Neto a pagar al proveedor: ${formatoMoneda.format(100)}`)).toBeInTheDocument();

    fireEvent.click(botonRegistrar());

    await waitFor(() =>
      expect(config.api.crearFactura).toHaveBeenCalledWith(
        expect.objectContaining({ retencionIsrAjuste: { monto: 0, motivo: "Constancia de exención" } })
      )
    );
  });

  it("sin ajuste no envía retencionIsrAjuste", async () => {
    const config = configComprasConRetencion();
    await renderFormulario({ config });
    completarCabecera(config);
    await screen.findByText(`Retención de ISR: ${formatoMoneda.format(2200)}`);

    fireEvent.click(botonRegistrar());

    await waitFor(() => expect(config.api.crearFactura).toHaveBeenCalledTimes(1));
    expect(config.api.crearFactura.mock.calls[0][0]).not.toHaveProperty("retencionIsrAjuste");
  });

  it("no consulta la retención para una nota de crédito", async () => {
    const config = configComprasConRetencion();
    await renderFormulario({ config, listaFacturas: facturas });
    escribir("Tipo de documento", "NotaCredito");
    completarCabecera(config);

    await new Promise((resolver) => setTimeout(resolver, 400));

    expect(config.api.retencionPrevia).not.toHaveBeenCalled();
    expect(screen.queryByText("Retención de ISR")).toBeNull();
  });

  it("lista las advertencias de la respuesta tras registrar", async () => {
    const config = configComprasConRetencion();
    config.api.crearFactura.mockResolvedValue({ data: { advertencias: ["La factura quedó marcada para revisión de retención."] } });
    await renderFormulario({ config });
    completarCabecera(config);

    fireEvent.click(botonRegistrar());

    expect(await screen.findByText("La factura quedó marcada para revisión de retención.")).toBeInTheDocument();
  });

  describe("moneda del documento", () => {
    const facturaUsd = { id: 20, numero: "F-USD", tipoDocumento: "Factura", clienteId: 5, estado: "Vigente", saldoPendiente: 100, moneda: "USD", tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat", tipoCambioArrastrado: false };

    it("en GTQ no consulta tipo de cambio", async () => {
      await renderFormulario();

      expect(screen.queryByText(/Tipo de cambio USD/)).toBeNull();
      expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
    });

    it("en USD consulta la tasa, muestra los totales en moneda y habilita el registro", async () => {
      const config = configVentas();
      await renderFormulario({ config });
      escribir("Moneda", "USD");
      escribir("Fecha", "2026-10-12");
      completarCabecera(config);
      escribir("Precio unitario", "1120");
      completarDte();

      expect((await screen.findAllByText("Tipo de cambio: 7.600000 (Banguat)")).length).toBeGreaterThan(0);
      expect(tiposCambioApi.consultar).toHaveBeenCalledWith("USD", "2026-10-12");
      expect(screen.getByText(`Total original: ${formatoImporte(1120, "USD")}`)).toBeInTheDocument();
      expect(screen.getByText(`Total en GTQ: ${formatoQuetzales(8512)}`)).toBeInTheDocument();
      expect(screen.getByRole("columnheader", { name: "Precio unitario (IVA incluido) en USD" })).toBeInTheDocument();
      await waitFor(() => expect(botonRegistrar()).toBeEnabled());
    });

    it("sin tasa disponible bloquea el registro y muestra el error", async () => {
      tiposCambioApi.consultar.mockRejectedValueOnce({ response: { status: 409, data: { error: "No hay tipo de cambio para fechas futuras." } } });
      const config = configVentas();
      await renderFormulario({ config });
      escribir("Moneda", "USD");
      completarCabecera(config);
      completarDte();

      expect(await screen.findByText("No hay tipo de cambio para fechas futuras.")).toBeInTheDocument();
      expect(botonRegistrar()).toBeDisabled();
    });

    it("con propuesta de Banguat muestra el último valor y mantiene bloqueado el registro", async () => {
      tiposCambioApi.consultar.mockResolvedValueOnce({ data: { disponible: false, tipoCambio: null, propuesta: { fecha: "2026-10-09", tasa: 7.64036, fuente: "Banguat" }, mensaje: "Banguat no respondió; capture la tasa manual o reintente." } });
      const config = configVentas();
      await renderFormulario({ config });
      escribir("Moneda", "USD");
      completarCabecera(config);
      completarDte();

      expect(await screen.findByText("Último valor guardado: 7.640360 del 2026-10-09")).toBeInTheDocument();
      expect(botonRegistrar()).toBeDisabled();
    });

    it("envía el código de moneda y el precio en moneda original", async () => {
      const config = configVentas();
      await renderFormulario({ config });
      escribir("Moneda", "USD");
      completarCabecera(config);
      escribir("Precio unitario", "1120");
      completarDte();
      await screen.findAllByText("Tipo de cambio: 7.600000 (Banguat)");
      await waitFor(() => expect(botonRegistrar()).toBeEnabled());

      fireEvent.click(botonRegistrar());

      await waitFor(() => expect(config.api.crearFactura).toHaveBeenCalledTimes(1));
      expect(config.api.crearFactura).toHaveBeenCalledWith(
        expect.objectContaining({ monedaCodigo: "USD", lineas: [expect.objectContaining({ precioUnitario: 1120 })] })
      );
    });

    it("una nota de crédito hereda la moneda y la tasa de su factura de origen sin consultar", async () => {
      const config = configVentas();
      await renderFormulario({ config, listaFacturas: [...facturas, facturaUsd] });
      tiposCambioApi.consultar.mockClear();
      escribir("Tipo de documento", "NotaCredito");
      completarCabecera(config);
      escribir("Documento de origen", "20");
      escribir("Precio unitario", "112");

      expect(await screen.findByText("Heredado de la factura de origen.")).toBeInTheDocument();
      expect(screen.getByRole("combobox", { name: "Moneda" })).toBeDisabled();
      expect(screen.getByRole("combobox", { name: "Moneda" })).toHaveValue("USD");
      expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
      expect(screen.getByText("El monto de la nota de crédito excede el saldo pendiente del documento de origen.")).toBeInTheDocument();
    });

    it("en compras con ISR calcula la retención con los montos ya convertidos a quetzales", async () => {
      const config = configComprasConRetencion();
      await renderFormulario({ config });
      escribir("Moneda", "USD");
      completarCabecera(config, "6");
      escribir("Precio unitario", "1000");

      await waitFor(() => expect(config.api.retencionPrevia).toHaveBeenCalledWith(expect.objectContaining({ montoBase: 7600, montoTotal: 7600 })), { timeout: 2000 });
    });
  });
});
