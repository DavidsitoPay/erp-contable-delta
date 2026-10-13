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

export const REGIMENES_ISR_CONTRAPARTE = [
  { valor: "NO_DEFINIDO", etiqueta: "No definido" },
  ...REGIMENES_ISR,
  { valor: "PAGO_DIRECTO", etiqueta: "Pago directo (PAGO_DIRECTO)" },
  { valor: "EXENTO", etiqueta: "Exento (EXENTO)" },
];

export const TIPOS_AGENTE_IVA_CLIENTE = [
  { valor: "SECTOR_PUBLICO", etiqueta: "Sector público" },
  { valor: "AGENTE_PRIVADO", etiqueta: "Agente privado" },
];

export const CONCEPTOS_PARAMETRO = [
  { valor: "ISR_PROVEEDOR_ROS", etiqueta: "ISR a proveedores del régimen simplificado" },
  { valor: "IVA_RETENIDO_CLIENTE", etiqueta: "IVA retenido por clientes" },
  { valor: "PLAZO_CONSTANCIA", etiqueta: "Plazo de la constancia" },
];

export const BASES_UMBRAL = [
  { valor: "BASE", etiqueta: "Base sin IVA" },
  { valor: "TOTAL", etiqueta: "Total de la factura" },
];

export const SENTIDOS_RETENCION = [
  { valor: "EFECTUADA", etiqueta: "Efectuada" },
  { valor: "RECIBIDA", etiqueta: "Recibida" },
];

export const CONCEPTOS_RETENCION = [
  { valor: "ISR_ROS", etiqueta: "ISR a proveedores" },
  { valor: "IVA_CLIENTE", etiqueta: "IVA retenido por clientes" },
];

export const ESTADOS_CONSTANCIA = [
  { valor: "PENDIENTE", etiqueta: "Pendiente" },
  { valor: "VENCIDA", etiqueta: "Vencida" },
  { valor: "EMITIDA", etiqueta: "Emitida" },
  { valor: "ANULADA", etiqueta: "Anulada" },
];

export const FORMULARIOS_CONSTANCIA = [
  { valor: "SAT-1331", etiqueta: "SAT-1331" },
  { valor: "SAT-1352", etiqueta: "SAT-1352" },
  { valor: "SAT-2340", etiqueta: "SAT-2340" },
];

export function etiquetaDe(opciones, valor) {
  return opciones.find((opcion) => opcion.valor === valor)?.etiqueta ?? "—";
}
