import { describe, expect, it } from "vitest";
import { COLUMNAS_CSV, COLUMNAS_TABLA, csvLibro, nombreArchivoCsv } from "./libroFiscal";

const reporte = {
  filas: [
    {
      fecha: "2026-10-03",
      tipoDocumento: "NotaCredito",
      serie: "A1",
      numero: "uuid-nc",
      numeroInterno: "NC-1",
      nit: "CF",
      nombre: "Casa, S.A.",
      estado: "Vigente",
      baseBienes: 0,
      baseServicios: -89.29,
      exento: 0,
      iva: -10.71,
      total: -100,
      referenciaSerie: "A1",
      referenciaNumero: "999",
      referenciaUuid: "uuid-origen",
    },
  ],
  totales: { baseBienes: 0, baseServicios: -89.29, exento: 0, iva: -10.71, total: -100 },
};

describe("libroFiscal", () => {
  it("las columnas del CSV contienen las de la tabla y agregan referencia y estado", () => {
    expect(COLUMNAS_CSV.slice(0, COLUMNAS_TABLA.length)).toEqual(COLUMNAS_TABLA);
    expect(COLUMNAS_CSV.map((c) => c.clave).slice(COLUMNAS_TABLA.length)).toEqual([
      "numeroInterno",
      "referenciaSerie",
      "referenciaNumero",
      "referenciaUuid",
      "estado",
    ]);
  });

  it("nombreArchivoCsv rellena el mes a dos dígitos", () => {
    expect(nombreArchivoCsv("ventas", 2026, 10)).toBe("libro-ventas-2026-10.csv");
    expect(nombreArchivoCsv("compras", 2026, 3)).toBe("libro-compras-2026-03.csv");
  });

  it("csvLibro genera encabezado, filas con montos negativos y la fila de totales", () => {
    const lineas = csvLibro(reporte).split("\r\n");

    expect(lineas[0]).toBe(
      "Fecha,Tipo,Serie,Número,NIT,Nombre,Base bienes,Base servicios,Exento / no afecto,IVA,Total,Número interno,Referencia serie,Referencia número,Referencia UUID,Estado"
    );
    expect(lineas[1]).toBe('2026-10-03,NotaCredito,A1,uuid-nc,CF,"Casa, S.A.",0.00,-89.29,0.00,-10.71,-100.00,NC-1,A1,999,uuid-origen,Vigente');
    expect(lineas[2]).toBe(",,,,,TOTAL,0.00,-89.29,0.00,-10.71,-100.00,,,,,");
    expect(lineas).toHaveLength(3);
  });
});
