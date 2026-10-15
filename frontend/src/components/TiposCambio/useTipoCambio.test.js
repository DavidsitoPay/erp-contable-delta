import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import { useTipoCambio } from "./useTipoCambio";

vi.mock("../../services/api", () => ({ tiposCambioApi: { consultar: vi.fn() } }));

const disponible = { data: { disponible: true, tipoCambio: { id: 1, tasa: 7.6, fuente: "Banguat", arrastrado: false }, propuesta: null, mensaje: null } };

async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("useTipoCambio", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    tiposCambioApi.consultar.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each([
    ["la moneda es GTQ", { moneda: "GTQ", fecha: "2026-10-12" }],
    ["está inactivo", { moneda: "USD", fecha: "2026-10-12", activo: false }],
  ])("no consulta cuando %s", async (_caso, parametros) => {
    const { result } = renderHook(() => useTipoCambio(parametros));

    await avanzar(500);

    expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
    expect(result.current.tasa).toBeNull();
    expect(result.current.bloqueado).toBe(false);
  });

  it("bloquea una moneda extranjera sin fecha", () => {
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "" }));

    expect(result.current.bloqueado).toBe(true);
  });

  it("espera 300 ms y luego publica la tasa disponible", async () => {
    tiposCambioApi.consultar.mockResolvedValue(disponible);
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-10-12" }));

    await avanzar(299);
    expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
    await avanzar(1);

    expect(tiposCambioApi.consultar).toHaveBeenCalledTimes(1);
    expect(tiposCambioApi.consultar).toHaveBeenCalledWith("USD", "2026-10-12");
    expect(result.current.tasa).toBe(7.6);
    expect(result.current.bloqueado).toBe(false);
    expect(result.current.cargando).toBe(false);
    expect(result.current.consultado).toBe(true);
    expect(result.current.mensaje).toBe("");
  });

  it("mientras espera informa que está cargando y bloqueado", async () => {
    tiposCambioApi.consultar.mockResolvedValue(disponible);
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-10-12" }));

    expect(result.current.cargando).toBe(true);
    expect(result.current.bloqueado).toBe(true);
  });

  it("publica la propuesta y el mensaje cuando Banguat no responde", async () => {
    const propuesta = { fecha: "2026-10-09", tasa: 7.64036, fuente: "Banguat" };
    const mensaje = "Banguat no respondió; capture la tasa manual o reintente.";
    tiposCambioApi.consultar.mockResolvedValue({ data: { disponible: false, tipoCambio: null, propuesta, mensaje } });
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-10-12" }));

    await avanzar(300);

    expect(result.current.tasa).toBeNull();
    expect(result.current.bloqueado).toBe(true);
    expect(result.current.consultado).toBe(true);
    expect(result.current.propuesta).toEqual(propuesta);
    expect(result.current.mensaje).toBe(mensaje);
  });

  it("publica el error del servidor y bloquea", async () => {
    tiposCambioApi.consultar.mockRejectedValue({ response: { status: 409, data: { error: "No hay tipo de cambio para fechas futuras." } } });
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-12-01" }));

    await avanzar(300);

    expect(result.current.error).toBe("No hay tipo de cambio para fechas futuras.");
    expect(result.current.bloqueado).toBe(true);
    expect(result.current.consultado).toBe(false);
  });

  it("publica un mensaje genérico cuando la red falla", async () => {
    tiposCambioApi.consultar.mockRejectedValue(new Error("Network Error"));
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-10-12" }));

    await avanzar(300);

    expect(result.current.error).toBe("No se pudo consultar el tipo de cambio.");
  });

  it("recargar vuelve a consultar", async () => {
    tiposCambioApi.consultar.mockResolvedValue(disponible);
    const { result } = renderHook(() => useTipoCambio({ moneda: "USD", fecha: "2026-10-12" }));
    await avanzar(300);

    act(() => result.current.recargar());
    await avanzar(300);

    expect(tiposCambioApi.consultar).toHaveBeenCalledTimes(2);
  });

  it("ignora la respuesta de una consulta que ya fue reemplazada", async () => {
    let resolverPrimera;
    tiposCambioApi.consultar
      .mockImplementationOnce(() => new Promise((resolver) => { resolverPrimera = resolver; }))
      .mockResolvedValueOnce({ data: { disponible: true, tipoCambio: { id: 2, tasa: 7.7, fuente: "Banguat", arrastrado: false }, propuesta: null, mensaje: null } });
    const { result, rerender } = renderHook((props) => useTipoCambio(props), { initialProps: { moneda: "USD", fecha: "2026-10-12" } });
    await avanzar(300);

    rerender({ moneda: "USD", fecha: "2026-10-13" });
    await avanzar(300);
    await act(async () => {
      resolverPrimera({ data: disponible.data });
    });

    expect(result.current.tasa).toBe(7.7);
  });
});
