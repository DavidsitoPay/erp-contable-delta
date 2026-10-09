const BOM = "﻿";

function celda(valor) {
  const texto = valor === null || valor === undefined ? "" : String(valor);
  return /[",\r\n]/.test(texto) ? `"${texto.replaceAll('"', '""')}"` : texto;
}

function valorDeColumna(columna, fila) {
  const valor = fila[columna.clave];
  return columna.numerica ? Number(valor ?? 0).toFixed(2) : valor;
}

export function construirCsv(columnas, filas) {
  const encabezado = columnas.map((c) => celda(c.titulo)).join(",");
  const cuerpo = filas.map((fila) => columnas.map((c) => celda(valorDeColumna(c, fila))).join(","));
  return [encabezado, ...cuerpo].join("\r\n");
}

export function descargarCsv(nombre, contenido) {
  const url = URL.createObjectURL(new Blob([BOM + contenido], { type: "text/csv;charset=utf-8" }));
  const enlace = document.createElement("a");
  enlace.href = url;
  enlace.download = nombre;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
