import { describe, expect, it } from "vitest";
import { ETIQUETAS_TRAMO, TRAMOS, csvSaldos, nombreArchivoSaldos } from "./saldosMoneda";

const reporte = {
  filas: [{ numero: "F-100", tercero: "Cliente SA", fecha: "2026-09-12", fechaVencimiento: "2026-10-12", moneda: "USD", saldoOriginal: 1000, tipoCambioLibros: 7.6, saldoGtq: 7600, diasVencido: 19, tramo: "1-30" }],
};

describe("saldosMoneda", () => {
  it("nombreArchivoSaldos incluye el tipo en minúsculas y el corte", () => {
    expect(nombreArchivoSaldos("CxC", "2026-10-31")).toBe("saldos-cxc-2026-10-31.csv");
  });

  it("csvSaldos escribe encabezado y fila con tasa y tramo legibles", () => {
    const lineas = csvSaldos(reporte).split("\r\n");

    expect(lineas[0]).toBe("Número,Cliente o proveedor,Fecha,Vencimiento,Moneda,Saldo original,Tipo de cambio de libros,Saldo Q,Días vencido,Tramo");
    expect(lineas[1]).toBe("F-100,Cliente SA,2026-09-12,2026-10-12,USD,1000.00,7.600000,7600.00,19,1-30 días");
  });

  it("todos los tramos tienen etiqueta", () => {
    expect(TRAMOS).toHaveLength(5);
    TRAMOS.forEach((tramo) => expect(ETIQUETAS_TRAMO[tramo]).toBeTruthy());
  });
});
