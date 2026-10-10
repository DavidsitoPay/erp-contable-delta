export const TIPOS_IMPUESTO = [
  { valor: "IVA_GENERAL", etiqueta: "IVA general" },
  { valor: "EXENTO", etiqueta: "Exento" },
  { valor: "NO_AFECTO", etiqueta: "No afecto" },
  { valor: "PEQUENO_CONTRIBUYENTE", etiqueta: "Pequeño contribuyente" },
];

export const AMBITOS_IMPUESTO = [
  { valor: "VENTAS", etiqueta: "Ventas" },
  { valor: "COMPRAS", etiqueta: "Compras" },
  { valor: "AMBOS", etiqueta: "Ventas y compras" },
];

export const REGIMENES_IVA = [
  { valor: "GENERAL", etiqueta: "General" },
  { valor: "PEQUENO_CONTRIBUYENTE", etiqueta: "Pequeño contribuyente" },
  { valor: "EXENTO", etiqueta: "Exento" },
];

export const REGIMENES_ISR = [
  { valor: "UTILIDADES", etiqueta: "Sobre las utilidades de actividades lucrativas (UTILIDADES)" },
  { valor: "SIMPLIFICADO", etiqueta: "Opcional simplificado sobre ingresos (SIMPLIFICADO)" },
];

export const TIPOS_AGENTE_IVA = [
  { valor: "EXPORTADOR_HABITUAL", etiqueta: "Exportador habitual" },
  { valor: "SECTOR_PUBLICO", etiqueta: "Sector público" },
  { valor: "TARJETA_CREDITO", etiqueta: "Emisor de tarjeta de crédito" },
  { valor: "COMBUSTIBLE", etiqueta: "Distribuidor de combustibles" },
  { valor: "CONTRIBUYENTE_ESPECIAL", etiqueta: "Contribuyente especial" },
];

export const TIPOS_BIEN_SERVICIO = [
  { valor: "BIEN", etiqueta: "Bien" },
  { valor: "SERVICIO", etiqueta: "Servicio" },
];

export function etiquetaDe(opciones, valor) {
  return opciones.find((opcion) => opcion.valor === valor)?.etiqueta ?? "—";
}
