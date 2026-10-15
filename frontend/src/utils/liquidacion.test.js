import { describe, expect, it } from "vitest";
import {
  diferenciaTransferencia,
  errorMonedasPago,
  librosEstimados,
  requiereEfectivo,
  resultadoEstimado,
  resumenLiquidacion,
} from "./liquidacion";

describe("errorMonedasPago", () => {
  it("acepta facturas de la moneda de la cuenta o cuenta GTQ con facturas USD", () => {
    expect(errorMonedasPago("GTQ", [{ moneda: "USD" }])).toBe("");
    expect(errorMonedasPago("GTQ", [])).toBe("");
    expect(errorMonedasPago("USD", [{ moneda: "USD" }])).toBe("");
  });

  it("rechaza factura en quetzales contra cuenta en dólares (RN-27)", () => {
    expect(errorMonedasPago("USD", [{}])).toMatch(/^RN-27/);
  });

  it("rechaza moneda distinta a la de la cuenta", () => {
    expect(errorMonedasPago("USD", [{ moneda: "EUR" }])).toContain("no coincide con la de la cuenta bancaria (USD)");
  });

  it("rechaza facturas de varias monedas en un mismo cobro", () => {
    const mensaje = errorMonedasPago("GTQ", [{ moneda: "USD" }, { moneda: "EUR" }]);

    expect(mensaje).toContain("una sola moneda");
    expect(mensaje).toContain("USD y EUR");
  });
});

describe("requiereEfectivo", () => {
  it("exige tipo de cambio cuando una cuenta GTQ cobra facturas extranjeras", () => {
    expect(requiereEfectivo("GTQ", [{ moneda: "USD" }])).toBe(true);
    expect(requiereEfectivo("GTQ", [{}])).toBe(false);
    expect(requiereEfectivo("USD", [{ moneda: "USD" }])).toBe(false);
  });
});

describe("librosEstimados", () => {
  it("convierte el monto parcial con el tipo de cambio de la factura", () => {
    expect(librosEstimados(400, { saldoPendiente: 1120, saldoPendienteGtq: 8512, tipoCambioAplicado: 7.6 })).toBe(3040);
  });

  it("usa el saldo en libros completo al cerrar la factura", () => {
    expect(librosEstimados(720, { saldoPendiente: 720, saldoPendienteGtq: 5472, tipoCambioAplicado: 7.6 })).toBe(5472);
  });

  it("convierte un pago parcial con tipo de cambio con decimales", () => {
    expect(librosEstimados(33.33, { saldoPendiente: 100, saldoPendienteGtq: 764.16, tipoCambioAplicado: 7.64157 })).toBe(254.69);
  });

  it("no supera el saldo en libros menos un centavo", () => {
    expect(librosEstimados(10, { saldoPendiente: 50, saldoPendienteGtq: 5, tipoCambioAplicado: 7.6 })).toBe(4.99);
    expect(librosEstimados(10, { saldoPendiente: 50, saldoPendienteGtq: 0, tipoCambioAplicado: 7.6 })).toBe(0);
  });
});

describe("resultadoEstimado", () => {
  it("cobro: valor recibido menos libros", () => {
    expect(resultadoEstimado(true, 3040, 3080)).toBe(40);
  });

  it("pago: libros menos valor pagado", () => {
    expect(resultadoEstimado(false, 7220, 7315)).toBe(-95);
  });
});

describe("resumenLiquidacion", () => {
  it("suma montos, valores y diferencial de un cobro", () => {
    const resumen = resumenLiquidacion(true, [
      { factura: { saldoPendiente: 1120, saldoPendienteGtq: 8512, tipoCambioAplicado: 7.6 }, montoAplicado: 400, valorGtq: 3080 },
    ]);

    expect(resumen).toEqual({ totalAplicado: 400, totalValor: 3080, diferencial: 40 });
  });

  it("calcula diferencial negativo en un pago", () => {
    const resumen = resumenLiquidacion(false, [
      { factura: { saldoPendiente: 950, saldoPendienteGtq: 7220, tipoCambioAplicado: 7.6 }, montoAplicado: 950, valorGtq: 7315 },
    ]);

    expect(resumen.diferencial).toBe(-95);
  });
});

describe("diferenciaTransferencia", () => {
  it("compra de dólares: valor en quetzales menos valor a tasa de referencia", () => {
    expect(diferenciaTransferencia(true, 7700, 1000, 7.64157)).toBe(-58.43);
  });

  it("venta de dólares: valor recibido en quetzales menos valor a tasa de referencia", () => {
    expect(diferenciaTransferencia(false, 1000, 7600, 7.64157)).toBe(-41.57);
  });
});
