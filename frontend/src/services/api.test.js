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

import api, { checkHealth, contrapartesApi, cuentasApi, cxcApi, cxpApi, librosApi, login } from "./api";

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

  it("codifica el id en la URL al actualizar y desactivar", () => {
    cuentasApi.actualizar("a/b", { nombre: "x" });
    cuentasApi.desactivar("a/b");

    expect(mocks.instance.put).toHaveBeenCalledWith("/cuentas/a%2Fb", { nombre: "x" });
    expect(mocks.instance.delete).toHaveBeenCalledWith("/cuentas/a%2Fb");
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
});
