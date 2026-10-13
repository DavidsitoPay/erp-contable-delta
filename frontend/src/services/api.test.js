import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => {
  const instance = {
    interceptors: { request: { use: vi.fn() } },
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  };
  return { instance, create: vi.fn(() => instance), get: vi.fn() };
});

vi.mock("axios", () => ({ default: { create: mocks.create, get: mocks.get } }));

import api, { asientosApi, checkHealth, conciliacionesApi, configuracionFiscalApi, contrapartesApi, cuentasApi, cuentasBancariasApi, cxcApi, cxpApi, impuestosApi, librosApi, librosFiscalesApi, login, movimientosTesoreriaApi, parametrosRetencionApi, periodosApi, reportesApi, retencionesApi } from "./api";

const interceptor = mocks.instance.interceptors.request.use.mock.calls[0][0];
const opcionesCreate = mocks.create.mock.calls[0][0];

beforeEach(() => {
  mocks.instance.get.mockReset();
  mocks.instance.post.mockReset();
  mocks.instance.put.mockReset();
  mocks.instance.delete.mockReset();
  mocks.get.mockReset();
  localStorage.clear();
});

describe("cliente axios", () => {
  it("se crea con una baseURL http(s) y se exporta como default", () => {
    expect(opcionesCreate.baseURL).toMatch(/^https?:\/\//);
    expect(api).toBe(mocks.instance);
  });
});

describe("interceptor de token", () => {
  it("agrega el encabezado Bearer cuando hay token guardado", () => {
    localStorage.setItem("delta_token", "abc123");

    const config = interceptor({ headers: {} });

    expect(config.headers.Authorization).toBe("Bearer abc123");
  });

  it("no agrega Authorization cuando no hay token", () => {
    const config = interceptor({ headers: {} });

    expect(config.headers.Authorization).toBeUndefined();
  });
});

describe("funciones de la API", () => {
  it("login envía email y password a /auth/login", () => {
    login("a@b.c", "pw");

    expect(mocks.instance.post).toHaveBeenCalledWith("/auth/login", { email: "a@b.c", password: "pw" });
  });

  it("checkHealth consulta la ruta /health", () => {
    checkHealth();

    expect(mocks.get).toHaveBeenCalledWith(expect.stringMatching(/\/health$/));
  });

  it("cuentasApi.listar excluye inactivas por defecto y las incluye si se pide", () => {
    cuentasApi.listar();
    cuentasApi.listar(true);

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/cuentas", { params: { incluirInactivas: false } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/cuentas", { params: { incluirInactivas: true } });
  });

  it("convierte el id a número en la URL al actualizar y desactivar", () => {
    cuentasApi.actualizar("12", { nombre: "x" });
    cuentasApi.desactivar("12");

    expect(mocks.instance.put).toHaveBeenCalledWith("/cuentas/12", { nombre: "x" });
    expect(mocks.instance.delete).toHaveBeenCalledWith("/cuentas/12");
  });

  it("contrapartesApi.listar solo envía tipo cuando se indica", () => {
    contrapartesApi.listar();
    contrapartesApi.listar("Cliente");

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/contrapartes", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/contrapartes", { params: { tipo: "Cliente" } });
  });

  it("librosApi.mayor omite periodoId cuando no se indica", () => {
    librosApi.mayor(3);

    expect(mocks.instance.get).toHaveBeenCalledWith("/libros/mayor", { params: { cuentaId: 3, periodoId: undefined } });
  });

  it("reportesApi consulta balance general y estado de resultados por periodo", () => {
    reportesApi.balanceGeneral(4);
    reportesApi.estadoResultados(4);

    expect(mocks.instance.get).toHaveBeenCalledWith("/reportes/balance-general", { params: { periodoId: 4 } });
    expect(mocks.instance.get).toHaveBeenCalledWith("/reportes/estado-resultados", { params: { periodoId: 4 } });
  });

  it.each([
    ["cxc", cxcApi],
    ["cxp", cxpApi],
  ])("%s apunta a sus rutas de facturas y pagos", (ruta, apiModulo) => {
    apiModulo.obtenerFactura(7);
    apiModulo.crearFactura({ numero: "F-1" });
    apiModulo.crearPago({ metodoPago: "Efectivo" });

    expect(mocks.instance.get).toHaveBeenCalledWith(`/${ruta}/facturas/7`);
    expect(mocks.instance.post).toHaveBeenCalledWith(`/${ruta}/facturas`, { numero: "F-1" });
    expect(mocks.instance.post).toHaveBeenCalledWith(`/${ruta}/pagos`, { metodoPago: "Efectivo" });
  });

  it("periodosApi.cerrar y reabrir envían POST a las rutas correctas", () => {
    periodosApi.cerrar(1);
    periodosApi.reabrir(1);

    expect(mocks.instance.post).toHaveBeenNthCalledWith(1, "/periodos/1/cerrar");
    expect(mocks.instance.post).toHaveBeenNthCalledWith(2, "/periodos/1/reabrir");
  });

  it("asientosApi.reversar envia el motivo a la ruta del asiento", () => {
    asientosApi.reversar("7", "Error de captura");
    expect(mocks.instance.post).toHaveBeenCalledWith("/asientos/7/reversar", { motivo: "Error de captura" });
  });

  it("convierte string ids numéricos a números en la URL", () => {
    cuentasApi.desactivar("7");

    expect(mocks.instance.delete).toHaveBeenCalledWith("/cuentas/7");
  });
});

describe("tesorería", () => {
  it("cuentasBancariasApi usa sus rutas y normaliza el id", () => {
    cuentasBancariasApi.listar();
    cuentasBancariasApi.listar(true);
    cuentasBancariasApi.obtener("3");
    cuentasBancariasApi.crear({ banco: "BAC" });
    cuentasBancariasApi.actualizar("3", { tipo: "Ahorro" });
    cuentasBancariasApi.desactivar("3");

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/cuentas-bancarias", { params: { incluirInactivas: false } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/cuentas-bancarias", { params: { incluirInactivas: true } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(3, "/cuentas-bancarias/3");
    expect(mocks.instance.post).toHaveBeenCalledWith("/cuentas-bancarias", { banco: "BAC" });
    expect(mocks.instance.put).toHaveBeenCalledWith("/cuentas-bancarias/3", { tipo: "Ahorro" });
    expect(mocks.instance.delete).toHaveBeenCalledWith("/cuentas-bancarias/3");
  });

  it("movimientosTesoreriaApi lista con filtros, crea y transfiere", () => {
    movimientosTesoreriaApi.listar();
    movimientosTesoreriaApi.listar({ desde: "2025-01-01" });
    movimientosTesoreriaApi.crear({ monto: 5 });
    movimientosTesoreriaApi.transferir({ monto: 9 });

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/movimientos-tesoreria", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/movimientos-tesoreria", { params: { desde: "2025-01-01" } });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(1, "/movimientos-tesoreria", { monto: 5 });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(2, "/movimientos-tesoreria/transferencias", { monto: 9 });
  });

  it("conciliacionesApi cubre listar, obtener, crear, actualizar, cancelar, marcar, desmarcar y finalizar", () => {
    conciliacionesApi.listar();
    conciliacionesApi.listar({ estado: "Pendiente" });
    conciliacionesApi.obtener("4");
    conciliacionesApi.crear({ saldoExtracto: 1 });
    conciliacionesApi.actualizar("4", { saldoExtracto: 2 });
    conciliacionesApi.cancelar("4");
    conciliacionesApi.marcar("4", "9");
    conciliacionesApi.desmarcar("4", "9");
    conciliacionesApi.finalizar("4");

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/conciliaciones", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/conciliaciones", { params: { estado: "Pendiente" } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(3, "/conciliaciones/4");
    expect(mocks.instance.put).toHaveBeenCalledWith("/conciliaciones/4", { saldoExtracto: 2 });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(1, "/conciliaciones", { saldoExtracto: 1 });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(2, "/conciliaciones/4/cancelar");
    expect(mocks.instance.post).toHaveBeenNthCalledWith(3, "/conciliaciones/4/movimientos", { movimientoId: 9 });
    expect(mocks.instance.delete).toHaveBeenCalledWith("/conciliaciones/4/movimientos/9");
    expect(mocks.instance.post).toHaveBeenNthCalledWith(4, "/conciliaciones/4/finalizar");
  });
});

describe("fiscal", () => {
  it("configuracionFiscalApi obtiene y actualiza la configuración única", () => {
    configuracionFiscalApi.obtener();
    configuracionFiscalApi.actualizar({ regimenIsr: "UTILIDADES" });

    expect(mocks.instance.get).toHaveBeenCalledWith("/configuracion-fiscal");
    expect(mocks.instance.put).toHaveBeenCalledWith("/configuracion-fiscal", { regimenIsr: "UTILIDADES" });
  });

  it("impuestosApi usa sus rutas, envía filtros y normaliza el id", () => {
    impuestosApi.listar();
    impuestosApi.listar({ vigenteEn: "2026-10-09", aplicaA: "VENTAS" });
    impuestosApi.obtener("3");
    impuestosApi.crear({ codigo: "NUEVO" });
    impuestosApi.actualizar("3", { nombre: "x" });
    impuestosApi.desactivar("3");

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/impuestos", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/impuestos", { params: { vigenteEn: "2026-10-09", aplicaA: "VENTAS" } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(3, "/impuestos/3");
    expect(mocks.instance.post).toHaveBeenCalledWith("/impuestos", { codigo: "NUEVO" });
    expect(mocks.instance.put).toHaveBeenCalledWith("/impuestos/3", { nombre: "x" });
    expect(mocks.instance.delete).toHaveBeenCalledWith("/impuestos/3");
  });

  it("librosFiscalesApi consulta ventas y compras por año y mes", () => {
    librosFiscalesApi.ventas(2026, 10);
    librosFiscalesApi.compras(2026, 9);

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/libros/fiscal/ventas", { params: { anio: 2026, mes: 10 } });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/libros/fiscal/compras", { params: { anio: 2026, mes: 9 } });
  });
});

describe("retenciones", () => {
  it("cxpApi consulta la retención previa y resuelve la revisión de una factura", () => {
    cxpApi.retencionPrevia({ proveedorId: 4 });
    cxpApi.resolverRevisionRetencion("9", "Régimen confirmado");

    expect(mocks.instance.post).toHaveBeenNthCalledWith(1, "/cxp/facturas/retencion-previa", { proveedorId: 4 });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(2, "/cxp/facturas/9/revision-retencion", { motivo: "Régimen confirmado" });
  });

  it("cxcApi consulta el IVA retenido sugerido de una factura", () => {
    cxcApi.retencionIvaSugerida("5");

    expect(mocks.instance.get).toHaveBeenCalledWith("/cxc/facturas/5/retencion-iva-sugerida");
  });

  it("parametrosRetencionApi lista con filtros, crea y actualiza con el id normalizado", () => {
    parametrosRetencionApi.listar();
    parametrosRetencionApi.listar({ incluirInactivos: true });
    parametrosRetencionApi.crear({ codigo: "ISR_PROVEEDOR_ROS" });
    parametrosRetencionApi.actualizar("3", { activo: false });

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/retenciones/parametros", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/retenciones/parametros", { params: { incluirInactivos: true } });
    expect(mocks.instance.post).toHaveBeenCalledWith("/retenciones/parametros", { codigo: "ISR_PROVEEDOR_ROS" });
    expect(mocks.instance.put).toHaveBeenCalledWith("/retenciones/parametros/3", { activo: false });
  });

  it("retencionesApi consulta el reporte y registra o anula constancias", () => {
    retencionesApi.reporte();
    retencionesApi.reporte({ anio: 2026, mes: 10, sentido: "EFECTUADA" });
    retencionesApi.registrarConstancia("7", { numero: "A-1" });
    retencionesApi.anularConstancia("2", "Error de captura");

    expect(mocks.instance.get).toHaveBeenNthCalledWith(1, "/retenciones/reporte", { params: {} });
    expect(mocks.instance.get).toHaveBeenNthCalledWith(2, "/retenciones/reporte", { params: { anio: 2026, mes: 10, sentido: "EFECTUADA" } });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(1, "/retenciones/7/constancia", { numero: "A-1" });
    expect(mocks.instance.post).toHaveBeenNthCalledWith(2, "/retenciones/constancias/2/anular", { motivo: "Error de captura" });
  });
});
