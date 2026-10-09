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

import api, { checkHealth, conciliacionesApi, contrapartesApi, cuentasApi, cuentasBancariasApi, cxcApi, cxpApi, librosApi, login, movimientosTesoreriaApi, periodosApi, reportesApi } from "./api";

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
