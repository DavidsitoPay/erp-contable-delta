import { describe, expect, it } from "vitest";
import { cuentasHoja, cuentasHojaDeTipo, etiquetaCuenta, indexarPorId } from "./cuentas";

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
});
