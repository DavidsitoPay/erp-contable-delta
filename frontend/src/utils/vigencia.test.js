import { describe, expect, it } from "vitest";
import { hastaONulo, sinVigencia } from "./vigencia";

describe("vigencia", () => {
  it("hastaONulo devuelve null cuando la vigencia final está vacía", () => {
    expect(hastaONulo({ vigenteHasta: "" })).toBeNull();
    expect(hastaONulo({ vigenteHasta: "2030-12-31" })).toBe("2030-12-31");
  });

  it("sinVigencia deja en blanco ambas fechas y conserva el resto", () => {
    expect(sinVigencia({ codigo: "X", vigenteDesde: "2020-01-01", vigenteHasta: "2021-01-01" })).toEqual({
      codigo: "X",
      vigenteDesde: "",
      vigenteHasta: "",
    });
  });
});
