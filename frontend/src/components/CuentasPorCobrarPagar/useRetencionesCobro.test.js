import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { RETENCION_VACIA } from "../../utils/retenciones";
import { useRetencionesCobro } from "./useRetencionesCobro";

describe("useRetencionesCobro", () => {
  it("cambiar combina los cambios con el valor vacío y con lo ya capturado", () => {
    const { result } = renderHook(() => useRetencionesCobro({}));

    act(() => result.current.cambiar(5, { monto: "10" }));
    act(() => result.current.cambiar(5, { activa: false }));

    expect(result.current.valores[5]).toEqual({ ...RETENCION_VACIA, monto: "10", activa: false });
  });

  it("solicitarSugerencia consulta una sola vez por factura y guarda la respuesta", async () => {
    const api = { retencionIvaSugerida: vi.fn().mockResolvedValue({ data: { sugerido: 300 } }) };
    const { result } = renderHook(() => useRetencionesCobro(api));

    await act(async () => {
      await Promise.all([result.current.solicitarSugerencia(7), result.current.solicitarSugerencia(7)]);
    });

    expect(api.retencionIvaSugerida).toHaveBeenCalledTimes(1);
    expect(api.retencionIvaSugerida).toHaveBeenCalledWith(7);
    expect(result.current.sugerencias[7]).toEqual({ sugerido: 300 });
  });

  it("guarda el error del servidor, o uno genérico, y permite reintentar", async () => {
    const api = {
      retencionIvaSugerida: vi
        .fn()
        .mockRejectedValueOnce({ response: { data: { error: "Factura no encontrada." } } })
        .mockRejectedValueOnce(new Error("Network Error"))
        .mockResolvedValueOnce({ data: { sugerido: 1 } }),
    };
    const { result } = renderHook(() => useRetencionesCobro(api));

    await act(async () => {
      await result.current.solicitarSugerencia(7);
    });
    expect(result.current.sugerencias[7]).toEqual({ error: "Factura no encontrada." });

    await act(async () => {
      await result.current.solicitarSugerencia(7);
    });
    expect(result.current.sugerencias[7]).toEqual({ error: "No se pudo consultar el IVA retenible de la factura." });

    await act(async () => {
      await result.current.solicitarSugerencia(7);
    });
    expect(result.current.sugerencias[7]).toEqual({ sugerido: 1 });
  });

  it("reiniciar descarta capturas y sugerencias y vuelve a permitir la consulta", async () => {
    const api = { retencionIvaSugerida: vi.fn().mockResolvedValue({ data: { sugerido: 3 } }) };
    const { result } = renderHook(() => useRetencionesCobro(api));
    await act(async () => {
      await result.current.solicitarSugerencia(7);
      result.current.cambiar(7, { monto: "3" });
    });

    act(() => result.current.reiniciar());
    expect(result.current.valores).toEqual({});
    expect(result.current.sugerencias).toEqual({});

    await act(async () => {
      await result.current.solicitarSugerencia(7);
    });
    expect(api.retencionIvaSugerida).toHaveBeenCalledTimes(2);
  });
});
