import { formatoTasa } from "./cambio";
import { construirCsv } from "./csv";

export const COLUMNAS_TABLA = [
  { clave: "fecha", titulo: "Fecha" },
  { clave: "tipoDocumento", titulo: "Tipo" },
  { clave: "serie", titulo: "Serie" },
  { clave: "numero", titulo: "Número" },
  { clave: "nit", titulo: "NIT" },
  { clave: "nombre", titulo: "Nombre" },
  { clave: "baseBienes", titulo: "Base bienes", numerica: true },
  { clave: "baseServicios", titulo: "Base servicios", numerica: true },
  { clave: "exento", titulo: "Exento / no afecto", numerica: true },
  { clave: "iva", titulo: "IVA", numerica: true },
  { clave: "total", titulo: "Total", numerica: true },
];

export const COLUMNAS_MONEDA = [
  { clave: "moneda", titulo: "Moneda" },
  { clave: "importeOriginal", titulo: "Importe original" },
  { clave: "tipoCambio", titulo: "Tipo de cambio" },
];

export const COLUMNAS_CSV = [
  ...COLUMNAS_TABLA,
  { clave: "numeroInterno", titulo: "Número interno" },
  { clave: "referenciaSerie", titulo: "Referencia serie" },
  { clave: "referenciaNumero", titulo: "Referencia número" },
  { clave: "referenciaUuid", titulo: "Referencia UUID" },
  { clave: "estado", titulo: "Estado" },
  ...COLUMNAS_MONEDA,
];

export function valoresMoneda(fila) {
  return {
    moneda: fila.moneda ?? "GTQ",
    importeOriginal: Number(fila.importeOriginal ?? fila.total),
    tipoCambio: Number(fila.tipoCambio ?? 1),
  };
}

export function nombreArchivoCsv(tipo, anio, mes) {
  return `libro-${tipo}-${anio}-${String(mes).padStart(2, "0")}.csv`;
}

export function csvLibro(reporte) {
  const filas = reporte.filas.map((fila) => {
    const { moneda, importeOriginal, tipoCambio } = valoresMoneda(fila);
    return { ...fila, moneda, importeOriginal: importeOriginal.toFixed(2), tipoCambio: formatoTasa(tipoCambio) };
  });
  return construirCsv(COLUMNAS_CSV, [...filas, { nombre: "TOTAL", ...reporte.totales }]);
}
