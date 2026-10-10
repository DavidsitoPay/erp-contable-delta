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

export const COLUMNAS_CSV = [
  ...COLUMNAS_TABLA,
  { clave: "numeroInterno", titulo: "Número interno" },
  { clave: "referenciaSerie", titulo: "Referencia serie" },
  { clave: "referenciaNumero", titulo: "Referencia número" },
  { clave: "referenciaUuid", titulo: "Referencia UUID" },
  { clave: "estado", titulo: "Estado" },
];

export function nombreArchivoCsv(tipo, anio, mes) {
  return `libro-${tipo}-${anio}-${String(mes).padStart(2, "0")}.csv`;
}

export function csvLibro(reporte) {
  return construirCsv(COLUMNAS_CSV, [...reporte.filas, { nombre: "TOTAL", ...reporte.totales }]);
}
