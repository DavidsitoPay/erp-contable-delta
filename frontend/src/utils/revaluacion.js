import { construirCsv } from "./csv";

export const LEYENDA_REVALUACION = "Diferencia no realizada; sin efecto fiscal automático, validar con asesor.";

export const COLUMNAS_PARTIDAS = [
  { clave: "tipo", titulo: "Tipo" },
  { clave: "referencia", titulo: "Referencia" },
  { clave: "saldoOriginal", titulo: "Saldo original", numerica: true },
  { clave: "valorLibrosGtq", titulo: "Valor en libros Q", numerica: true },
  { clave: "valorCierreGtq", titulo: "Valor al cierre Q", numerica: true },
  { clave: "ajusteGtq", titulo: "Ajuste Q", numerica: true },
  { clave: "efecto", titulo: "Efecto" },
];

export function mesAnterior(hoy = new Date()) {
  const mes = hoy.getMonth();
  return mes === 0 ? { anio: hoy.getFullYear() - 1, mes: 12 } : { anio: hoy.getFullYear(), mes };
}

export function etiquetaMes(anio, mes) {
  return `${String(mes).padStart(2, "0")}/${anio}`;
}

export function nombreArchivoRevaluacion({ monedaCodigo, anio, mes }) {
  return `revaluacion-${monedaCodigo}-${anio}-${String(mes).padStart(2, "0")}.csv`;
}

export function csvRevaluacion(revaluacion) {
  const totales = [
    { tipo: "TOTAL", referencia: "Ganancia no realizada", ajusteGtq: revaluacion.totalGanancia },
    { tipo: "TOTAL", referencia: "Pérdida no realizada", ajusteGtq: revaluacion.totalPerdida },
  ];
  return construirCsv(COLUMNAS_PARTIDAS, [...revaluacion.partidas, ...totales]);
}
