import { describe, expect, it } from "vitest";
import { FORMULARIO_VACIO, formularioDesdeImpuesto, payloadCierre, payloadCreacion, payloadEdicion } from "./formularioImpuesto";

const impuesto = {
  id: 1,
  codigo: "IVA_GENERAL",
  nombre: "IVA general 12 %",
  tipo: "IVA_GENERAL",
  tasa: 12,
  aplicaA: "AMBOS",
  generaCredito: true,
  articuloLegal: "Decreto 27-92, art. 10",
  vigenteDesde: "2001-01-01",
  vigenteHasta: null,
  activo: true,
  enUso: false,
};

describe("formularioImpuesto", () => {
  it("el formulario vacío ofrece IVA general con crédito para ambos ámbitos", () => {
    expect(FORMULARIO_VACIO).toMatchObject({ tipo: "IVA_GENERAL", aplicaA: "AMBOS", generaCredito: true, codigo: "" });
  });

  it("precarga todos los campos al editar y vacía las vigencias en una nueva versión", () => {
    expect(formularioDesdeImpuesto(impuesto, "editar")).toEqual({
      codigo: "IVA_GENERAL",
      nombre: "IVA general 12 %",
      tipo: "IVA_GENERAL",
      tasa: "12",
      aplicaA: "AMBOS",
      generaCredito: true,
      articuloLegal: "Decreto 27-92, art. 10",
      vigenteDesde: "2001-01-01",
      vigenteHasta: "",
    });
    expect(formularioDesdeImpuesto({ ...impuesto, vigenteHasta: "2030-01-01" }, "version")).toMatchObject({
      codigo: "IVA_GENERAL",
      vigenteDesde: "",
      vigenteHasta: "",
    });
  });

  it("payloadCreacion recorta textos, convierte la tasa y deja la vigencia final en null si falta", () => {
    const form = { ...FORMULARIO_VACIO, codigo: " NUEVO ", nombre: " Nuevo ", tasa: "5", articuloLegal: " Art. 1 ", vigenteDesde: "2026-01-01" };

    expect(payloadCreacion(form)).toEqual({
      codigo: "NUEVO",
      nombre: "Nuevo",
      tipo: "IVA_GENERAL",
      tasa: 5,
      aplicaA: "AMBOS",
      generaCredito: true,
      articuloLegal: "Art. 1",
      vigenteDesde: "2026-01-01",
      vigenteHasta: null,
    });
  });

  it("payloadEdicion envía el cuerpo completo y conserva los campos sensibles cuando el impuesto está en uso", () => {
    const form = { ...formularioDesdeImpuesto(impuesto, "editar"), nombre: "Renombrado", tasa: "99" };

    expect(payloadEdicion(form, { ...impuesto, enUso: true })).toEqual({
      nombre: "Renombrado",
      tipo: "IVA_GENERAL",
      tasa: 12,
      aplicaA: "AMBOS",
      generaCredito: true,
      articuloLegal: "Decreto 27-92, art. 10",
      nota: null,
      vigenteDesde: "2001-01-01",
      vigenteHasta: null,
      activo: true,
    });
  });

  it("payloadEdicion toma tipo, tasa, ámbito, crédito y vigencia inicial del formulario cuando no está en uso", () => {
    const form = { ...formularioDesdeImpuesto(impuesto, "editar"), tasa: "10" };

    expect(payloadEdicion(form, { ...impuesto, nota: "Nota vigente" })).toEqual({
      nombre: "IVA general 12 %",
      tipo: "IVA_GENERAL",
      tasa: 10,
      aplicaA: "AMBOS",
      generaCredito: true,
      articuloLegal: "Decreto 27-92, art. 10",
      nota: "Nota vigente",
      vigenteDesde: "2001-01-01",
      vigenteHasta: null,
      activo: true,
    });
  });

  it("payloadCierre conserva el resto del impuesto y fija la vigencia final", () => {
    expect(payloadCierre({ ...impuesto, enUso: true }, "2026-12-31")).toEqual({
      nombre: "IVA general 12 %",
      tipo: "IVA_GENERAL",
      tasa: 12,
      aplicaA: "AMBOS",
      generaCredito: true,
      articuloLegal: "Decreto 27-92, art. 10",
      nota: null,
      vigenteDesde: "2001-01-01",
      vigenteHasta: "2026-12-31",
      activo: true,
    });
  });
});
