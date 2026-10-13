import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useAccionCatalogo } from "./useAccionCatalogo";

async function ejecutarAccion(result, accion) {
  let resultado;
  await act(async () => {
    resultado = await result.current.ejecutar(accion, "No se pudo guardar.");
  });
  return resultado;
}

describe("useAccionCatalogo", () => {
  it("ejecuta la acción, recarga y devuelve true", async () => {
    const recargar = vi.fn().mockResolvedValue(undefined);
    const accion = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() => useAccionCatalogo(recargar));

    expect(await ejecutarAccion(result, accion)).toBe(true);

    expect(accion).toHaveBeenCalledTimes(1);
    expect(recargar).toHaveBeenCalledTimes(1);
    expect(result.current.error).toBe("");
    expect(result.current.cargando).toBe(false);
  });

  it("si la acción falla muestra el error del servidor, no recarga y devuelve false", async () => {
    const recargar = vi.fn();
    const accion = vi.fn().mockRejectedValue({ response: { data: { error: "Vigencia traslapada." } } });
    const { result } = renderHook(() => useAccionCatalogo(recargar));

    expect(await ejecutarAccion(result, accion)).toBe(false);

    expect(result.current.error).toBe("Vigencia traslapada.");
    expect(recargar).not.toHaveBeenCalled();
    expect(result.current.cargando).toBe(false);
  });

  it("usa el mensaje por defecto cuando no hay respuesta del servidor", async () => {
    const { result } = renderHook(() => useAccionCatalogo(vi.fn()));

    await ejecutarAccion(result, vi.fn().mockRejectedValue(new Error("Network Error")));

    expect(result.current.error).toBe("No se pudo guardar.");
  });
});
