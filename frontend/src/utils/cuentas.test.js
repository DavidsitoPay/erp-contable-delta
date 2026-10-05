import { describe, expect, it } from "vitest";
import { cuentasHoja, cuentasHojaDeTipo, etiquetaCuenta, etiquetaSubcuentas, indexarPorId, sangriaPorProfundidad } from "./cuentas";

const cuentas = [
  { id: 1, tipo: "Activo", activa: true, cuentaPadreId: null },
  { id: 2, tipo: "Activo", activa: true, cuentaPadreId: 1 },
  { id: 3, tipo: "Capital", activa: true, cuentaPadreId: null },
  { id: 4, tipo: "Capital", activa: false, cuentaPadreId: null },
];

describe("cuentas", () => {
  it("cuentasHoja excluye cuentas con hijos e inactivas", () => {
    expect(cuentasHoja(cuentas).map((c) => c.id)).toEqual([2, 3]);
  });

  it("cuentasHojaDeTipo filtra por tipo", () => {
    expect(cuentasHojaDeTipo(cuentas, "Capital").map((c) => c.id)).toEqual([3]);
  });

  it("indexarPorId arma un mapa por id", () => {
    expect(indexarPorId([{ id: 5, a: 1 }])).toEqual({ 5: { id: 5, a: 1 } });
  });

  it("etiquetaCuenta combina banco y número y tolera cuentas ausentes", () => {
    expect(etiquetaCuenta({ banco: "BAC", numero: "123" })).toBe("BAC 123");
    expect(etiquetaCuenta(undefined)).toBe("");
  });

  it("sangriaPorProfundidad crece 1.25rem por nivel desde 0.75rem", () => {
    expect(sangriaPorProfundidad(0)).toBe("0.75rem");
    expect(sangriaPorProfundidad(1)).toBe("2rem");
    expect(sangriaPorProfundidad(2)).toBe("3.25rem");
  });

  it("etiquetaSubcuentas usa singular solo para 1", () => {
    expect(etiquetaSubcuentas(1)).toBe("1 subcuenta");
    expect(etiquetaSubcuentas(3)).toBe("3 subcuentas");
  });
});
