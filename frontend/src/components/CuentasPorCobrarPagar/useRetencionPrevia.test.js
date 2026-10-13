import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useRetencionPrevia } from "./useRetencionPrevia";

const previa = { estado: "Retenida", retencion: 2200, neto: 42600 };
const datos = { activo: true, proveedorId: "5", fecha: "2026-10-05", montoBase: 40000, montoTotal: 44800 };

async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("useRetencionPrevia", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each([
    ["está inactivo", { activo: false }],
    ["no hay proveedor", { proveedorId: "" }],
    ["no hay fecha", { fecha: "" }],
    ["el total es cero", { montoTotal: 0 }],
  ])("no consulta cuando %s", async (_caso, cambios) => {
    const api = { retencionPrevia: vi.fn() };
    const { result } = renderHook(() => useRetencionPrevia({ api, ...datos, ...cambios }));

    await avanzar(500);

    expect(api.retencionPrevia).not.toHaveBeenCalled();
    expect(result.current.previa).toBeNull();
  });

  it("espera 300 ms sin cambios, consulta una vez con el cuerpo esperado y publica la vista previa", async () => {
    const api = { retencionPrevia: vi.fn().mockResolvedValue({ data: previa }) };
    const { result, rerender } = renderHook((props) => useRetencionPrevia(props), { initialProps: { api, ...datos, montoTotal: 100 } });

    await avanzar(200);
    rerender({ api, ...datos });
    await avanzar(299);
    expect(api.retencionPrevia).not.toHaveBeenCalled();
    await avanzar(1);

    expect(api.retencionPrevia).toHaveBeenCalledTimes(1);
    expect(api.retencionPrevia).toHaveBeenCalledWith({ proveedorId: 5, fecha: "2026-10-05", montoBase: 40000, montoTotal: 44800 });
    expect(result.current.previa).toEqual(previa);
    expect(result.current.cargando).toBe(false);
  });

  it("un 409 publica el error y bloquea; otro error publica el mensaje sin bloquear", async () => {
    const api = {
      retencionPrevia: vi
        .fn()
        .mockRejectedValueOnce({ response: { status: 409, data: { error: "Falta el parámetro vigente." } } })
        .mockRejectedValueOnce(new Error("Network Error")),
    };
    const { result, rerender } = renderHook((props) => useRetencionPrevia(props), { initialProps: { api, ...datos } });

    await avanzar(300);
    expect(result.current).toMatchObject({ error: "Falta el parámetro vigente.", bloqueada: true, previa: null });

    rerender({ api, ...datos, montoTotal: 50000 });
    await avanzar(300);
    expect(result.current).toMatchObject({ error: "No se pudo calcular la retención de ISR.", bloqueada: false });
  });

  it("ignora la respuesta de una consulta que ya fue reemplazada", async () => {
    let resolverPrimera;
    const api = {
      retencionPrevia: vi
        .fn()
        .mockReturnValueOnce(new Promise((resolver) => { resolverPrimera = resolver; }))
        .mockResolvedValueOnce({ data: { ...previa, retencion: 1 } }),
    };
    const { result, rerender } = renderHook((props) => useRetencionPrevia(props), { initialProps: { api, ...datos } });

    await avanzar(300);
    rerender({ api, ...datos, montoTotal: 50000 });
    await avanzar(300);
    expect(result.current.previa.retencion).toBe(1);

    await act(async () => {
      resolverPrimera({ data: previa });
    });

    expect(result.current.previa.retencion).toBe(1);
  });

  it("al quedar sin datos descarta el resultado anterior", async () => {
    const api = { retencionPrevia: vi.fn().mockResolvedValue({ data: previa }) };
    const { result, rerender } = renderHook((props) => useRetencionPrevia(props), { initialProps: { api, ...datos } });
    await avanzar(300);
    expect(result.current.previa).toEqual(previa);

    rerender({ api, ...datos, proveedorId: "" });

    expect(result.current.previa).toBeNull();
  });
});
