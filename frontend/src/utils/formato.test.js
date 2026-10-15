import { describe, expect, it } from "vitest";
import { formatoDiferencial, formatoFechaHora, formatoImporte, formatoMoneda, formatoQuetzales, hoyIso, MESES, mensajeError, monto } from "./formato";

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

  it("formatoFechaHora muestra el año de una marca ISO", () => {
    expect(formatoFechaHora("2026-10-09T15:00:00Z")).toMatch(/2026/);
  });

  it("MESES lista los doce meses en orden", () => {
    expect(MESES).toHaveLength(12);
    expect(MESES[0]).toBe("Enero");
    expect(MESES[11]).toBe("Diciembre");
  });
});

describe("formato multimoneda", () => {
  it("formatoImporte antepone el código de moneda salvo GTQ", () => {
    expect(formatoImporte(1120, "USD")).toBe(`USD ${formatoMoneda.format(1120)}`);
    expect(formatoImporte(1120, "GTQ")).toBe(formatoMoneda.format(1120));
    expect(formatoImporte(1120)).toBe(formatoMoneda.format(1120));
    expect(formatoImporte(1120, null)).toBe(formatoMoneda.format(1120));
    expect(formatoImporte(undefined, "USD")).toBe(`USD ${formatoMoneda.format(0)}`);
  });

  it("formatoQuetzales antepone Q", () => {
    expect(formatoQuetzales(8512)).toBe(`Q${formatoMoneda.format(8512)}`);
  });

  it("formatoDiferencial muestra el signo del resultado", () => {
    expect(formatoDiferencial(40)).toBe(`+Q${formatoMoneda.format(40)}`);
    expect(formatoDiferencial(-72)).toBe(`-Q${formatoMoneda.format(72)}`);
    expect(formatoDiferencial(0)).toBe(`Q${formatoMoneda.format(0)}`);
  });
});
