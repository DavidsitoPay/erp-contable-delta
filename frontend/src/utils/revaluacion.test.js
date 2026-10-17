import { describe, expect, it } from "vitest";
import { LEYENDA_REVALUACION, csvRevaluacion, etiquetaMes, mesAnterior, nombreArchivoRevaluacion } from "./revaluacion";

describe("revaluacion", () => {
  it("mesAnterior devuelve el mes previo, cruzando el año en enero", () => {
    expect(mesAnterior(new Date(2026, 9, 13))).toEqual({ anio: 2026, mes: 9 });
    expect(mesAnterior(new Date(2026, 0, 5))).toEqual({ anio: 2025, mes: 12 });
  });

  it("etiquetaMes y nombreArchivoRevaluacion usan dos dígitos de mes", () => {
    expect(etiquetaMes(2026, 3)).toBe("03/2026");
    expect(nombreArchivoRevaluacion({ monedaCodigo: "USD", anio: 2026, mes: 10 })).toBe("revaluacion-USD-2026-10.csv");
  });

  it("csvRevaluacion incluye las partidas y los totales de ganancia y pérdida", () => {
    const revaluacion = {
      partidas: [{ tipo: "CxC", referencia: "F-100", saldoOriginal: 1000, valorLibrosGtq: 7600, valorCierreGtq: 7650, ajusteGtq: 50, efecto: "Ganancia" }],
      totalGanancia: 55,
      totalPerdida: 100,
    };

    const lineas = csvRevaluacion(revaluacion).split("\r\n");

    expect(lineas[0]).toBe("Tipo,Referencia,Saldo original,Valor en libros Q,Valor al cierre Q,Ajuste Q,Efecto");
    expect(lineas[1]).toBe("CxC,F-100,1000.00,7600.00,7650.00,50.00,Ganancia");
    expect(lineas[2]).toBe("TOTAL,Ganancia no realizada,0.00,0.00,0.00,55.00,");
    expect(lineas[3]).toBe("TOTAL,Pérdida no realizada,0.00,0.00,0.00,100.00,");
  });

  it("la leyenda coincide con el texto del API", () => {
    expect(LEYENDA_REVALUACION).toBe("Diferencia no realizada; sin efecto fiscal automático, validar con asesor.");
  });
});
