import { construirCsv } from "./csv";
import { SENTIDOS_RETENCION, etiquetaDe } from "./etiquetasFiscales";
import { redondear } from "./fiscal";
import { formatoMoneda, monto } from "./formato";

export const LEYENDA_REFERENCIAL = "Referencial, validar con asesor";
export const AVISO_CONSTANCIA = "Insumo para declarar; el ERP registra los datos de la constancia, no la emite.";

export const AJUSTE_VACIO = { activo: false, monto: "", motivo: "" };
export const RETENCION_VACIA = { activa: null, monto: "", confirma: false, constanciaNumero: "", constanciaFecha: "" };

const IMPUESTO_CSV = { ISR_ROS: "ISR", IVA_CLIENTE: "IVA", ISR_NORES: "ISR no residentes" };

export function descripcionEscala({ tasaBase, tramoLimite, tasaExcedente }) {
  if (tramoLimite === null || tramoLimite === undefined) return `${tasaBase} %`;
  return `${tasaBase} % hasta ${formatoMoneda.format(tramoLimite)} y ${tasaExcedente} % sobre el excedente`;
}

export const permiteAjuste = (previa) => Boolean(previa) && previa.estado !== "NoAplica";

export function ajusteValido(ajuste, total) {
  const importe = monto(ajuste.monto);
  return ajuste.monto !== "" && importe >= 0 && importe < total && ajuste.motivo.trim() !== "";
}

export function payloadAjuste(ajuste) {
  return { monto: monto(ajuste.monto), motivo: ajuste.motivo.trim() };
}

export function netoAPagar(previa, ajusteActivo, ajuste, total) {
  return ajusteActivo ? redondear(total - monto(ajuste.monto)) : previa.neto;
}

export const valorRetencion = (valores, documentoId) => valores[documentoId] ?? RETENCION_VACIA;

export const retencionActiva = (valor, clienteAgente) => valor.activa ?? Boolean(clienteAgente);

export function errorRetencionIva(valor, montoAplicado, sugerencia, clienteAgente) {
  const importe = monto(valor.monto);
  if (sugerencia?.yaRetenida || importe <= 0) return "";
  if (Math.round(importe * 100) / 100 !== importe) return "El IVA retenido debe tener máximo 2 decimales.";
  if (importe > montoAplicado) return "El IVA retenido no puede exceder el monto aplicado a la factura.";
  if (sugerencia?.ivaRetenible !== undefined && importe > sugerencia.ivaRetenible) {
    return `El IVA retenido no puede exceder el IVA retenible de la factura (${formatoMoneda.format(sugerencia.ivaRetenible)}).`;
  }
  if (!clienteAgente && !valor.confirma) return "Confirme que el cliente retuvo IVA aunque no está registrado como agente de retención.";
  if (valor.constanciaNumero.trim() !== "" && valor.constanciaFecha === "") return "Indique la fecha de la constancia.";
  return "";
}

export function retencionAEnviar(valor, sugerencia, clienteAgente) {
  const importe = monto(valor.monto);
  if (!retencionActiva(valor, clienteAgente) || importe <= 0 || sugerencia?.yaRetenida) return null;
  const numero = valor.constanciaNumero.trim();
  return {
    monto: importe,
    confirmaClienteNoAgente: !clienteAgente && valor.confirma,
    ...(numero ? { constancia: { numero, fecha: valor.constanciaFecha } } : {}),
  };
}

export function aplicacionesConRetencion(aplicaciones, valores, sugerencias, clienteAgente) {
  return aplicaciones.map((aplicacion) => {
    const retencionIva = retencionAEnviar(valorRetencion(valores, aplicacion.documentoId), sugerencias[aplicacion.documentoId], clienteAgente);
    return retencionIva ? { ...aplicacion, retencionIva } : aplicacion;
  });
}

export function hayErrorRetencionIva(aplicaciones, valores, sugerencias, clienteAgente) {
  return aplicaciones.some((aplicacion) => {
    const valor = valorRetencion(valores, aplicacion.documentoId);
    const error = errorRetencionIva(valor, aplicacion.montoAplicado, sugerencias[aplicacion.documentoId], clienteAgente);
    return retencionActiva(valor, clienteAgente) && error !== "";
  });
}

export function resumenCobro(aplicaciones) {
  const totalAplicado = redondear(aplicaciones.reduce((suma, a) => suma + a.montoAplicado, 0));
  const ivaRetenido = redondear(aplicaciones.reduce((suma, a) => suma + (a.retencionIva?.monto ?? 0), 0));
  return { totalAplicado, ivaRetenido, neto: redondear(totalAplicado - ivaRetenido) };
}

export const COLUMNAS_CSV = [
  { clave: "sentido", titulo: "Sentido" },
  { clave: "impuesto", titulo: "Impuesto" },
  { clave: "nit", titulo: "NIT" },
  { clave: "nombre", titulo: "Nombre" },
  { clave: "documento", titulo: "Documento" },
  { clave: "dteSerie", titulo: "Serie DTE" },
  { clave: "dteNumero", titulo: "Numero DTE" },
  { clave: "dteUuid", titulo: "UUID" },
  { clave: "fechaDocumento", titulo: "Fecha documento" },
  { clave: "fechaOperacion", titulo: "Fecha pago o cobro" },
  { clave: "base", titulo: "Base", numerica: true },
  { clave: "tasa", titulo: "Tasa" },
  { clave: "monto", titulo: "Retencion", numerica: true },
  { clave: "constanciaNumero", titulo: "Constancia" },
  { clave: "constanciaFecha", titulo: "Fecha constancia" },
  { clave: "formulario", titulo: "Formulario" },
  { clave: "estadoConstancia", titulo: "Estado constancia" },
  { clave: "tipoRentaCodigo", titulo: "Tipo de renta (codigo)" },
  { clave: "tipoRentaNombre", titulo: "Tipo de renta" },
  { clave: "monedaCodigo", titulo: "Moneda" },
  { clave: "montoOrigen", titulo: "Monto origen" },
  { clave: "tipoCambio", titulo: "Tipo de cambio" },
  { clave: "tipoCambioFecha", titulo: "Fecha tipo de cambio" },
  { clave: "enteradoGtq", titulo: "Enterado", numerica: true },
  { clave: "saldoPorEnterarGtq", titulo: "Saldo por enterar", numerica: true },
];

export function nombreArchivoCsv(anio, mes) {
  return `retenciones-${anio}-${String(mes).padStart(2, "0")}.csv`;
}

export function csvRetenciones(reporte) {
  const filas = reporte.filas.map((fila) => ({
    ...fila,
    sentido: etiquetaDe(SENTIDOS_RETENCION, fila.sentido),
    impuesto: IMPUESTO_CSV[fila.concepto] ?? "",
    documento: fila.documentoNumero,
    tasa: fila.tasaEfectiva ?? fila.tasa,
  }));
  return construirCsv(COLUMNAS_CSV, filas);
}

export function filtrosReporte({ anio, mes, sentido, concepto, nit, estadoConstancia }) {
  const opcionales = { sentido, concepto, nit: nit.trim(), estadoConstancia };
  return {
    anio: Number(anio),
    mes: Number(mes),
    ...Object.fromEntries(Object.entries(opcionales).filter(([, valor]) => valor !== "")),
  };
}

export function payloadConstancia(form) {
  return {
    numero: form.numero.trim(),
    fecha: form.fecha,
    formulario: form.formulario || null,
    fechaPagoFormulario: form.fechaPagoFormulario || null,
    periodoDeclarado: form.periodoDeclarado ? `${form.periodoDeclarado}-01` : null,
  };
}

export const tipoRentaInicial = (proveedor) => proveedor?.tipoRentaNrCodigo ?? "";

export const aplicacionesConTipoRenta = (aplicaciones, tipos, porDefecto) =>
  aplicaciones.map((a) => {
    const codigo = tipos[a.documentoId] || porDefecto;
    return codigo ? { ...a, tipoRentaCodigo: codigo } : a;
  });
