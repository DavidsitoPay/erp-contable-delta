import { formatoTasa } from "./cambio";
import { construirCsv } from "./csv";

export const TRAMOS = ["NO_VENCIDO", "1-30", "31-60", "61-90", "91+"];
export const ETIQUETAS_TRAMO = { NO_VENCIDO: "No vencido", "1-30": "1-30 días", "31-60": "31-60 días", "61-90": "61-90 días", "91+": "Más de 90 días" };

export const COLUMNAS_SALDOS = [
  { clave: "numero", titulo: "Número" },
  { clave: "tercero", titulo: "Cliente o proveedor" },
  { clave: "fecha", titulo: "Fecha" },
  { clave: "fechaVencimiento", titulo: "Vencimiento" },
  { clave: "moneda", titulo: "Moneda" },
  { clave: "saldoOriginal", titulo: "Saldo original", numerica: true },
  { clave: "tipoCambioLibros", titulo: "Tipo de cambio de libros" },
  { clave: "saldoGtq", titulo: "Saldo Q", numerica: true },
  { clave: "diasVencido", titulo: "Días vencido" },
  { clave: "tramo", titulo: "Tramo" },
];

export function nombreArchivoSaldos(tipo, corte) {
  return `saldos-${tipo.toLowerCase()}-${corte}.csv`;
}

export function csvSaldos(reporte) {
  const filas = reporte.filas.map((fila) => ({
    ...fila,
    tipoCambioLibros: formatoTasa(fila.tipoCambioLibros),
    tramo: ETIQUETAS_TRAMO[fila.tramo] ?? fila.tramo,
  }));
  return construirCsv(COLUMNAS_SALDOS, filas);
}
