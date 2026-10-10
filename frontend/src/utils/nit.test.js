import { describe, expect, it } from "vitest";
import { mensajeNit, nitValido, normalizarNit } from "./nit";

const INVALIDO = "El NIT no es válido (dígito verificador incorrecto).";

describe("normalizarNit", () => {
  it.each([
    ["1234567-9", "1234567-9"],
    ["12345679", "1234567-9"],
    [" 1234567 9 ", "1234567-9"],
    ["6k", "6-K"],
    ["7", "7"],
  ])("normaliza %s", (entrada, esperado) => {
    expect(normalizarNit(entrada)).toBe(esperado);
  });
});

describe("nitValido", () => {
  it.each(["1234567-9", "12345679", " 1234567-9 ", "6-K", "6-k", "12-4"])("%s es válido", (nit) => {
    expect(nitValido(nit)).toBe(true);
  });

  it.each(["1234567-8", "1234567-K", "abc", "", "7", "12a-4"])("'%s' no es válido", (nit) => {
    expect(nitValido(nit)).toBe(false);
  });

  it("un cuerpo de 13 dígitos no es válido aunque el dígito verificador coincida", () => {
    expect(nitValido("1234567890123-1")).toBe(false);
    expect(mensajeNit("1234567890123-1", "Cliente")).toBe(INVALIDO);
  });
});

describe("mensajeNit", () => {
  it.each([
    ["Cliente", "", ""],
    ["Cliente", "cf", ""],
    ["Cliente", "1234567-9", ""],
    ["Cliente", "1234567-8", INVALIDO],
    ["Proveedor", "", "El NIT del proveedor es obligatorio."],
    ["Proveedor", "CF", "CF solo es válido para clientes."],
    ["Proveedor", "1234567-9", ""],
    ["Proveedor", "1234567-8", INVALIDO],
  ])("%s con NIT '%s'", (tipo, nit, esperado) => {
    expect(mensajeNit(nit, tipo)).toBe(esperado);
  });

  it("acepta C/F como consumidor final igual que CF", () => {
    expect(mensajeNit("C/F", "Cliente")).toBe("");
    expect(mensajeNit("c/f", "Proveedor")).toBe("CF solo es válido para clientes.");
  });
});
