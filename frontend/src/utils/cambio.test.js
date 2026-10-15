import { describe, expect, it } from "vitest";
import {
  camposMoneda,
  convertirAGtq,
  esMonedaExtranjera,
  estadoHeredado,
  etiquetaFuente,
  formatoTasa,
  monedaDe,
  monedaDeCuenta,
  tasaValida,
  textoTipoCambio,
} from "./cambio";

describe("cambio", () => {
  it("monedaDe y monedaDeCuenta usan GTQ por defecto", () => {
    expect(monedaDe({ moneda: "USD" })).toBe("USD");
    expect(monedaDe({})).toBe("GTQ");
    expect(monedaDe(undefined)).toBe("GTQ");
    expect(monedaDeCuenta({ monedaCodigo: "USD" })).toBe("USD");
    expect(monedaDeCuenta(undefined)).toBe("GTQ");
  });

  it("esMonedaExtranjera distingue quetzales de otras monedas", () => {
    expect(esMonedaExtranjera("USD")).toBe(true);
    expect(esMonedaExtranjera("GTQ")).toBe(false);
    expect(esMonedaExtranjera("")).toBe(false);
    expect(esMonedaExtranjera(undefined)).toBe(false);
  });

  it("camposMoneda solo agrega monedaCodigo para moneda extranjera", () => {
    expect(camposMoneda("USD")).toEqual({ monedaCodigo: "USD" });
    expect(camposMoneda("GTQ")).toEqual({});
    expect(camposMoneda(undefined)).toEqual({});
  });

  it("formatoTasa muestra seis decimales", () => {
    expect(formatoTasa(7.6)).toBe("7.600000");
    expect(formatoTasa("7.64136")).toBe("7.641360");
  });

  it("convertirAGtq redondea a centavos", () => {
    expect(convertirAGtq(1120, 7.6)).toBe(8512);
    expect(convertirAGtq(100, 7.64157)).toBe(764.16);
  });

  it("tasaValida exige positiva, máximo seis decimales y rango", () => {
    expect(tasaValida(7.64136)).toBe(true);
    expect(tasaValida(0)).toBe(false);
    expect(tasaValida(-1)).toBe(false);
    expect(tasaValida(Number.NaN)).toBe(false);
    expect(tasaValida(7.1234567)).toBe(false);
    expect(tasaValida(100000)).toBe(false);
  });

  it("etiquetaFuente indica arrastrado y omite la fuente vacía", () => {
    expect(etiquetaFuente({ fuente: "Banguat", arrastrado: false })).toBe("Banguat");
    expect(etiquetaFuente({ fuente: "Banguat", arrastrado: true })).toBe("Banguat (arrastrado)");
    expect(etiquetaFuente({ fuente: "Manual", arrastrado: false })).toBe("Manual");
    expect(etiquetaFuente({ arrastrado: false })).toBe("Sin fuente");
  });

  it("textoTipoCambio combina tasa y fuente", () => {
    expect(textoTipoCambio({ tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat", tipoCambioArrastrado: false })).toBe("7.600000 (Banguat)");
  });

  it("estadoHeredado marca el estado como heredado y disponible", () => {
    const estado = estadoHeredado({ tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat" });

    expect(estado.disponible).toBe(true);
    expect(estado.heredado).toBe(true);
    expect(estado.tasa).toBe(7.6);
    expect(estado.tipoCambio.arrastrado).toBe(false);
  });
});
