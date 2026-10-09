import { describe, expect, it } from "vitest";
import { perfilActual, puedeAdministrarConfiguracionFiscal, puedeGestionarFiscal, puedeGestionarTesoreria, puedeReversarAsientos } from "./perfiles";

describe("perfiles", () => {
  it.each([
    ["Administrador del sistema", true],
    ["Contador", true],
    ["Vendedor", false],
    [null, false],
  ])("puedeGestionarTesoreria(%s) = %s", (perfil, esperado) => {
    expect(puedeGestionarTesoreria(perfil)).toBe(esperado);
  });

  it.each([
    ["Administrador del sistema", true],
    ["Contador", false],
    [null, false],
  ])("puedeReversarAsientos(%s) = %s", (perfil, esperado) => {
    expect(puedeReversarAsientos(perfil)).toBe(esperado);
  });

  it.each([
    ["Administrador del sistema", true, true],
    ["Contador", true, false],
    ["Vendedor", false, false],
    [null, false, false],
  ])("fiscal(%s): gestionar=%s, administrar configuración=%s", (perfil, gestiona, administra) => {
    expect(puedeGestionarFiscal(perfil)).toBe(gestiona);
    expect(puedeAdministrarConfiguracionFiscal(perfil)).toBe(administra);
  });

  it("perfilActual lee el perfil guardado", () => {
    localStorage.setItem("delta_usuario", JSON.stringify({ perfil: "Contador" }));
    expect(perfilActual()).toBe("Contador");
  });

  it("perfilActual devuelve null sin usuario o con JSON inválido", () => {
    expect(perfilActual()).toBeNull();
    localStorage.setItem("delta_usuario", "{no-json");
    expect(perfilActual()).toBeNull();
  });
});
