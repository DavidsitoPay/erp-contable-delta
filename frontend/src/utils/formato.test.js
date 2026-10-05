import { describe, expect, it } from "vitest";
import { formatoMoneda, hoyIso, mensajeError, monto } from "./formato";

describe("formato", () => {
  it("formatoMoneda usa siempre dos decimales", () => {
    expect(formatoMoneda.format(1234.5)).toMatch(/^1[,.\s ]?234[.,]50$/);
  });

  it("monto convierte texto numérico y devuelve 0 si no es numérico", () => {
    expect(monto("12.5")).toBe(12.5);
    expect(monto("")).toBe(0);
    expect(monto("abc")).toBe(0);
  });

  it("hoyIso devuelve la fecha en formato ISO", () => {
    expect(hoyIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it("mensajeError prefiere el error del servidor y cae al texto por defecto", () => {
    expect(mensajeError({ response: { data: { error: "Fallo" } } }, "Defecto")).toBe("Fallo");
    expect(mensajeError(new Error("x"), "Defecto")).toBe("Defecto");
  });
});
