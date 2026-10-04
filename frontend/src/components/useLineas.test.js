import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { useLineas } from "./useLineas";

const crear = () => ({ valor: "" });

describe("useLineas", () => {
  it("arranca con el minimo de lineas", () => {
    const { result } = renderHook(() => useLineas(crear, 2));
    expect(result.current.lineas).toHaveLength(2);
  });

  it("agrega y actualiza lineas", () => {
    const { result } = renderHook(() => useLineas(crear, 1));
    act(() => result.current.agregarLinea());
    act(() => result.current.actualizarLinea(1, "valor", "x"));
    expect(result.current.lineas.map((l) => l.valor)).toEqual(["", "x"]);
  });

  it("no baja del minimo al quitar", () => {
    const { result } = renderHook(() => useLineas(crear, 1));
    act(() => result.current.quitarLinea(0));
    expect(result.current.lineas).toHaveLength(1);
    act(() => result.current.agregarLinea());
    act(() => result.current.quitarLinea(0));
    expect(result.current.lineas).toHaveLength(1);
  });

  it("reinicia al minimo", () => {
    const { result } = renderHook(() => useLineas(crear, 2));
    act(() => result.current.agregarLinea());
    act(() => result.current.reiniciarLineas());
    expect(result.current.lineas).toHaveLength(2);
  });
});
