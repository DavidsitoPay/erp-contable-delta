import { describe, expect, it } from "vitest";
import { CONCEPTOS_PARAMETRO, REGIMENES_ISR_CONTRAPARTE, REGIMENES_IVA, TIPOS_AGENTE_IVA_CLIENTE, TIPOS_IMPUESTO, etiquetaDe } from "./etiquetasFiscales";

describe("etiquetasFiscales", () => {
  it("etiquetaDe devuelve la etiqueta de un valor conocido", () => {
    expect(etiquetaDe(REGIMENES_IVA, "PEQUENO_CONTRIBUYENTE")).toBe("Pequeño contribuyente");
    expect(etiquetaDe(TIPOS_IMPUESTO, "NO_AFECTO")).toBe("No afecto");
  });

  it.each([undefined, null, "DESCONOCIDO"])("etiquetaDe devuelve guion para %s", (valor) => {
    expect(etiquetaDe(REGIMENES_IVA, valor)).toBe("—");
  });

  it("el régimen de ISR de las contrapartes ofrece cinco valores con No definido primero", () => {
    expect(REGIMENES_ISR_CONTRAPARTE.map((r) => r.valor)).toEqual(["NO_DEFINIDO", "UTILIDADES", "SIMPLIFICADO", "PAGO_DIRECTO", "EXENTO"]);
    expect(etiquetaDe(REGIMENES_ISR_CONTRAPARTE, "NO_DEFINIDO")).toBe("No definido");
  });

  it("el tipo de agente de IVA del cliente ofrece sector público y agente privado", () => {
    expect(TIPOS_AGENTE_IVA_CLIENTE.map((t) => t.valor)).toEqual(["SECTOR_PUBLICO", "AGENTE_PRIVADO"]);
  });

  it("el concepto del parámetro de retención incluye el plazo de la constancia", () => {
    expect(etiquetaDe(CONCEPTOS_PARAMETRO, "PLAZO_CONSTANCIA")).toBe("Plazo de la constancia");
  });
});
