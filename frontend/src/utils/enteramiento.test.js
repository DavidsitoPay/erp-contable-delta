import { describe, expect, it } from "vitest";
import { FORMULARIOS_ENTERAMIENTO, cuentasGtq, enteramientoValido, payloadEnteramiento, solicitudEnteramiento } from "./enteramiento";

const formularioValido = { formulario: "SAT-1331", fechaPago: "2026-11-10", boleta: " 123 ", monto: "150.5", cuentaBancariaId: "4" };

describe("enteramiento", () => {
  it("excluye SAT-2340 de los formularios de enteramiento", () => {
    expect(FORMULARIOS_ENTERAMIENTO.map((f) => f.valor)).toEqual(["SAT-1331", "SAT-1352"]);
  });

  it("filtra cuentas en GTQ y conserva las que no informan moneda", () => {
    const cuentas = [{ id: 1 }, { id: 2, monedaCodigo: "GTQ" }, { id: 3, monedaCodigo: "USD" }];

    expect(cuentasGtq(cuentas).map((c) => c.id)).toEqual([1, 2]);
  });

  it("exige formulario, fecha, boleta, cuenta y monto positivo", () => {
    expect(enteramientoValido(formularioValido)).toBe(true);
    expect(enteramientoValido({ ...formularioValido, formulario: "" })).toBe(false);
    expect(enteramientoValido({ ...formularioValido, fechaPago: "" })).toBe(false);
    expect(enteramientoValido({ ...formularioValido, boleta: "   " })).toBe(false);
    expect(enteramientoValido({ ...formularioValido, monto: "0" })).toBe(false);
    expect(enteramientoValido({ ...formularioValido, monto: "-5" })).toBe(false);
    expect(enteramientoValido({ ...formularioValido, cuentaBancariaId: "" })).toBe(false);
  });

  it("arma la solicitud de vista previa con monto y cuenta numéricos", () => {
    expect(solicitudEnteramiento("2026-10-01", formularioValido)).toEqual({ periodo: "2026-10-01", monto: 150.5, cuentaBancariaId: 4 });
  });

  it("arma el payload de registro con la boleta sin espacios", () => {
    expect(payloadEnteramiento("2026-10-01", formularioValido)).toEqual({
      periodo: "2026-10-01",
      monto: 150.5,
      cuentaBancariaId: 4,
      formulario: "SAT-1331",
      fechaPago: "2026-11-10",
      boleta: "123",
    });
  });
});
