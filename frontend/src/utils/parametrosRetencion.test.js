import { describe, expect, it } from "vitest";
import { formatoMoneda } from "./formato";
import {
  FORMULARIO_PARAMETRO_VACIO,
  admiteTramo,
  descripcionTasa,
  descripcionUmbral,
  esPlazo,
  formularioDesdeParametro,
  payloadActualizacion,
  payloadCreacion,
} from "./parametrosRetencion";

const isr = {
  codigo: "ISR_PROVEEDOR_ROS",
  concepto: "ISR_PROVEEDOR_ROS",
  nombre: "ISR proveedores",
  tasaBase: 5,
  tramoLimite: 30000,
  tasaExcedente: 7,
  umbralMinimo: 2500,
  umbralSobre: "BASE",
  diaLimite: null,
  articuloLegal: "Dto. 10-2012 art. 48",
  nota: "Nota del asesor",
  vigenteDesde: "2014-01-01",
  vigenteHasta: null,
  activo: true,
};
const plazo = { ...isr, codigo: "PLAZO_CONSTANCIA_ISR", concepto: "PLAZO_CONSTANCIA", tasaBase: null, tramoLimite: null, tasaExcedente: null, umbralMinimo: 0, diaLimite: 5 };

describe("parametrosRetencion", () => {
  it("el formulario vacío inicia en ISR con la leyenda referencial", () => {
    expect(FORMULARIO_PARAMETRO_VACIO.concepto).toBe("ISR_PROVEEDOR_ROS");
    expect(FORMULARIO_PARAMETRO_VACIO.nota).toBe("Referencial, validar con asesor");
    expect(admiteTramo(FORMULARIO_PARAMETRO_VACIO)).toBe(true);
    expect(esPlazo(FORMULARIO_PARAMETRO_VACIO)).toBe(false);
  });

  it("formularioDesdeParametro convierte a texto, deja las vigencias en blanco y conserva el código", () => {
    expect(formularioDesdeParametro(isr)).toEqual({
      codigo: "ISR_PROVEEDOR_ROS",
      concepto: "ISR_PROVEEDOR_ROS",
      nombre: "ISR proveedores",
      tasaBase: "5",
      tramoLimite: "30000",
      tasaExcedente: "7",
      umbralMinimo: "2500",
      umbralSobre: "BASE",
      diaLimite: "",
      articuloLegal: "Dto. 10-2012 art. 48",
      nota: "Nota del asesor",
      vigenteDesde: "",
      vigenteHasta: "",
    });
    expect(formularioDesdeParametro({ ...plazo, nota: null })).toMatchObject({ tasaBase: "", diaLimite: "5", umbralMinimo: "0", nota: "Referencial, validar con asesor" });
  });

  it("payloadCreacion arma el cuerpo de ISR con tramo", () => {
    const form = { ...formularioDesdeParametro(isr), codigo: " ISR_PROVEEDOR_ROS ", nombre: " ISR 2027 ", vigenteDesde: "2027-01-01" };

    expect(payloadCreacion(form)).toEqual({
      codigo: "ISR_PROVEEDOR_ROS",
      concepto: "ISR_PROVEEDOR_ROS",
      nombre: "ISR 2027",
      tasaBase: 5,
      tramoLimite: 30000,
      tasaExcedente: 7,
      umbralMinimo: 2500,
      umbralSobre: "BASE",
      diaLimite: null,
      articuloLegal: "Dto. 10-2012 art. 48",
      nota: "Nota del asesor",
      vigenteDesde: "2027-01-01",
      vigenteHasta: null,
    });
  });

  it("payloadCreacion omite el tramo si está vacío o si el concepto no lo admite", () => {
    const sinTramo = { ...formularioDesdeParametro(isr), tramoLimite: "", tasaExcedente: "", vigenteDesde: "2027-01-01" };
    const iva = { ...sinTramo, concepto: "IVA_RETENIDO_CLIENTE", tramoLimite: "100", tasaExcedente: "3", umbralSobre: "TOTAL", nota: "  " };

    expect(payloadCreacion(sinTramo)).toMatchObject({ tramoLimite: null, tasaExcedente: null });
    expect(payloadCreacion(iva)).toMatchObject({ tramoLimite: null, tasaExcedente: null, umbralSobre: "TOTAL", nota: "Referencial, validar con asesor" });
  });

  it("payloadCreacion del plazo envía solo el día límite", () => {
    const form = { ...formularioDesdeParametro(plazo), vigenteDesde: "2027-01-01", vigenteHasta: "2027-12-31" };

    expect(payloadCreacion(form)).toMatchObject({ concepto: "PLAZO_CONSTANCIA", tasaBase: null, umbralMinimo: 0, diaLimite: 5, vigenteHasta: "2027-12-31" });
  });

  it("payloadActualizacion conserva nota, activo y vigencia final y aplica los cambios", () => {
    expect(payloadActualizacion(isr, { vigenteHasta: "2026-12-31" })).toEqual({ nota: "Nota del asesor", activo: true, vigenteHasta: "2026-12-31" });
    expect(payloadActualizacion({ ...isr, vigenteHasta: "2030-01-01" }, { activo: false })).toEqual({ nota: "Nota del asesor", activo: false, vigenteHasta: "2030-01-01" });
  });

  it("describe la tasa, el plazo y el umbral", () => {
    expect(descripcionTasa(isr)).toBe(`5 % hasta ${formatoMoneda.format(30000)} y 7 % sobre el excedente`);
    expect(descripcionTasa({ ...isr, tramoLimite: null, tasaExcedente: null, tasaBase: 25 })).toBe("25 %");
    expect(descripcionTasa(plazo)).toBe("Día 5 del mes siguiente");
    expect(descripcionUmbral({ ...isr, umbralMinimo: 30000, umbralSobre: "TOTAL" })).toBe(`${formatoMoneda.format(30000)} · Total de la factura`);
    expect(descripcionUmbral(plazo)).toBe("—");
  });
});
