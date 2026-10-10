import { describe, expect, it } from "vitest";
import {
  AVISO_DTE,
  calcularLinea,
  calcularResumen,
  dtePayload,
  dteValido,
  fechaLocalAIso,
  impuestoDeLinea,
  impuestoPorDefecto,
  impuestosPermitidos,
  redondear,
  uuidValido,
} from "./fiscal";

const iva12 = { id: 1, tipo: "IVA_GENERAL", tasa: 12, generaCredito: true };
const exento = { id: 2, tipo: "EXENTO", tasa: 0, generaCredito: false };
const pequeno = { id: 3, tipo: "PEQUENO_CONTRIBUYENTE", tasa: 5, generaCredito: false };
const ivaSinCredito = { id: 4, tipo: "IVA_GENERAL", tasa: 12, generaCredito: false };
const noAfecto = { id: 5, tipo: "NO_AFECTO", tasa: 0, generaCredito: false };
const catalogo = [iva12, exento, pequeno, ivaSinCredito, noAfecto];
const UUID = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
const DTE_VACIO = { dteUuid: "", dteSerie: "", dteNumero: "", dteFechaCertificacion: "" };

function dteCon(cambios = {}) {
  return { dteUuid: UUID, dteSerie: "A1", dteNumero: "100", dteFechaCertificacion: "2026-10-09T10:15", ...cambios };
}

describe("redondear", () => {
  it.each([
    [1.005, 1.01],
    [0.125, 0.13],
    [-0.125, -0.13],
    [10.714285714285714, 10.71],
    [15.0075, 15.01],
    [99.999, 100],
    [0, 0],
  ])("redondear(%s) = %s", (entrada, esperado) => {
    expect(redondear(entrada)).toBe(esperado);
  });
});

describe("calcularLinea", () => {
  it.each([
    ["112 con IVA general", 1, 112, iva12, 112, 12, 100],
    ["100 con IVA general", 1, 100, iva12, 100, 10.71, 89.29],
    ["274.50 con IVA general", 1, 274.5, iva12, 274.5, 29.41, 245.09],
    ["2 x 100 con IVA general", 2, 100, iva12, 200, 21.43, 178.57],
    ["50.50 exento", 1, 50.5, exento, 50.5, 0, 50.5],
    ["80 no afecto", 1, 80, noAfecto, 80, 0, 80],
    ["100 pequeño contribuyente", 1, 100, pequeno, 100, 0, 100],
    ["3 x 33.333 exento", 3, 33.333, exento, 100, 0, 100],
    ["1.5 x 10.005 exento", 1.5, 10.005, exento, 15.01, 0, 15.01],
  ])("%s", (_nombre, cantidad, precioUnitario, impuesto, total, iva, base) => {
    const r = calcularLinea({ cantidad, precioUnitario, impuesto });

    expect(r.montoLinea).toBe(total);
    expect(r.iva).toBe(iva);
    expect(r.base).toBe(base);
  });

  it("marca el IVA como acreditable solo con IVA general con crédito", () => {
    expect(calcularLinea({ cantidad: 1, precioUnitario: 112, impuesto: iva12 }).ivaAcreditable).toBe(true);
    expect(calcularLinea({ cantidad: 1, precioUnitario: 112, impuesto: ivaSinCredito }).ivaAcreditable).toBe(false);
    expect(calcularLinea({ cantidad: 1, precioUnitario: 112, impuesto: exento }).ivaAcreditable).toBe(false);
  });

  it("sin impuesto seleccionado no calcula IVA", () => {
    const r = calcularLinea({ cantidad: 1, precioUnitario: 100, impuesto: null });

    expect(r).toEqual({ montoLinea: 100, base: 100, iva: 0, ivaAcreditable: false });
  });
});

describe("calcularResumen", () => {
  it("suma líneas ya redondeadas: 2 x 100 con IVA y 50.50 exento", () => {
    const r = calcularResumen([
      { cantidad: 2, precioUnitario: 100, impuesto: iva12 },
      { cantidad: 1, precioUnitario: 50.5, impuesto: exento },
    ]);

    expect(r).toMatchObject({ total: 250.5, base: 229.07, iva: 21.43, ivaAcreditable: 21.43, ivaCosto: 0 });
    expect(r.lineas).toHaveLength(2);
    expect(redondear(r.base + r.iva)).toBe(r.total);
  });

  it("el IVA sin crédito viaja como IVA a costo", () => {
    const r = calcularResumen([{ cantidad: 2, precioUnitario: 100, impuesto: ivaSinCredito }]);

    expect(r).toMatchObject({ total: 200, iva: 21.43, ivaAcreditable: 0, ivaCosto: 21.43 });
  });

  it("sin líneas todo es cero", () => {
    expect(calcularResumen([])).toMatchObject({ total: 0, base: 0, iva: 0, ivaAcreditable: 0, ivaCosto: 0 });
  });
});

describe("impuestosPermitidos", () => {
  it.each([
    [undefined, [1, 2, 3, 4, 5]],
    ["GENERAL", [1, 2, 4, 5]],
    ["PEQUENO_CONTRIBUYENTE", [2, 3, 5]],
    ["EXENTO", [2, 5]],
  ])("régimen %s", (regimen, ids) => {
    expect(impuestosPermitidos(catalogo, regimen).map((i) => i.id)).toEqual(ids);
  });
});

describe("impuestoPorDefecto", () => {
  it("prefiere el IVA general con crédito", () => {
    expect(impuestoPorDefecto([exento, iva12], "GENERAL")).toBe(iva12);
  });

  it("para pequeño contribuyente prefiere su impuesto", () => {
    expect(impuestoPorDefecto([exento, pequeno], "PEQUENO_CONTRIBUYENTE")).toBe(pequeno);
  });

  it("sin preferido devuelve el primero y sin impuestos devuelve null", () => {
    expect(impuestoPorDefecto([exento, noAfecto], "EXENTO")).toBe(exento);
    expect(impuestoPorDefecto([], "GENERAL")).toBeNull();
  });
});

describe("impuestoDeLinea", () => {
  it("usa el impuesto elegido si sigue permitido y si no el predeterminado", () => {
    expect(impuestoDeLinea({ impuestoId: "4" }, catalogo, iva12)).toBe(ivaSinCredito);
    expect(impuestoDeLinea({ impuestoId: "" }, catalogo, iva12)).toBe(iva12);
    expect(impuestoDeLinea({ impuestoId: "99" }, catalogo, iva12)).toBe(iva12);
  });
});

describe("DTE", () => {
  it("uuidValido acepta la forma canónica y rechaza otras", () => {
    expect(uuidValido(UUID)).toBe(true);
    expect(uuidValido(`  ${UUID}  `)).toBe(true);
    expect(uuidValido("123")).toBe(false);
  });

  it("fechaLocalAIso devuelve null si falta o es inválida", () => {
    expect(fechaLocalAIso("")).toBeNull();
    expect(fechaLocalAIso("no-es-fecha")).toBeNull();
  });

  it("fechaLocalAIso agrega segundos y el desfase local sin cambiar el instante", () => {
    const iso = fechaLocalAIso("2026-10-09T10:15");

    expect(iso).toMatch(/^2026-10-09T10:15:00[+-]\d{2}:\d{2}$/);
    expect(new Date(iso).getTime()).toBe(new Date("2026-10-09T10:15").getTime());
  });

  it("fechaLocalAIso conserva los segundos si ya vienen", () => {
    expect(fechaLocalAIso("2026-10-09T10:15:30")).toMatch(/^2026-10-09T10:15:30[+-]\d{2}:\d{2}$/);
  });

  it.each([
    ["obligatorio completo", dteCon(), true, true],
    ["obligatorio sin fecha", dteCon({ dteFechaCertificacion: "" }), true, false],
    ["obligatorio con UUID inválido", dteCon({ dteUuid: "abc" }), true, false],
    ["obligatorio vacío", DTE_VACIO, true, false],
    ["opcional vacío", DTE_VACIO, false, true],
    ["opcional con solo serie", dteCon({ dteUuid: "", dteNumero: "" }), false, false],
    ["opcional con trío y sin fecha", dteCon({ dteFechaCertificacion: "" }), false, true],
  ])("dteValido: %s", (_nombre, dte, obligatorio, esperado) => {
    expect(dteValido(dte, obligatorio)).toBe(esperado);
  });

  it("dtePayload recorta, convierte vacíos en null y formatea la fecha", () => {
    expect(dtePayload({ ...DTE_VACIO, dteSerie: " " })).toEqual({
      dteUuid: null,
      dteSerie: null,
      dteNumero: null,
      dteFechaCertificacion: null,
    });

    const lleno = dtePayload(dteCon({ dteSerie: " A1 " }));
    expect(lleno).toMatchObject({ dteUuid: UUID, dteSerie: "A1", dteNumero: "100" });
    expect(lleno.dteFechaCertificacion).toMatch(/^2026-10-09T10:15:00/);
  });

  it("el aviso aclara que no se certifica ni se conecta con la SAT", () => {
    expect(AVISO_DTE).toMatch(/no certifica documentos tributarios electrónicos ni se conecta con la SAT/);
  });
});
