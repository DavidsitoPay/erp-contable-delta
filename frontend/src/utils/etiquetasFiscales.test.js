import { describe, expect, it } from "vitest";
import { REGIMENES_IVA, TIPOS_IMPUESTO, etiquetaDe } from "./etiquetasFiscales";

describe("etiquetasFiscales", () => {
  it("etiquetaDe devuelve la etiqueta de un valor conocido", () => {
    expect(etiquetaDe(REGIMENES_IVA, "PEQUENO_CONTRIBUYENTE")).toBe("Pequeño contribuyente");
    expect(etiquetaDe(TIPOS_IMPUESTO, "NO_AFECTO")).toBe("No afecto");
  });

  it.each([undefined, null, "DESCONOCIDO"])("etiquetaDe devuelve guion para %s", (valor) => {
    expect(etiquetaDe(REGIMENES_IVA, valor)).toBe("—");
  });
});
