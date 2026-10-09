export const AVISO_DTE =
  "Delta ERP registra los datos del DTE (UUID, serie, número y fecha de certificación) emitidos por un certificador autorizado; no certifica documentos tributarios electrónicos ni se conecta con la SAT.";

const UUID_CANONICO = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CAMPOS_TRIO_DTE = ["dteUuid", "dteSerie", "dteNumero"];

export function uuidValido(valor) {
  return UUID_CANONICO.test(valor.trim());
}

export function redondear(valor) {
  const centavos = Number((Math.abs(valor) * 100).toPrecision(15));
  return (Math.sign(valor) * Math.round(centavos)) / 100;
}

export function calcularLinea({ cantidad, precioUnitario, impuesto }) {
  const montoLinea = redondear(cantidad * precioUnitario);
  const gravado = impuesto?.tipo === "IVA_GENERAL";
  const iva = gravado ? redondear((montoLinea * impuesto.tasa) / (100 + impuesto.tasa)) : 0;
  return {
    montoLinea,
    base: redondear(montoLinea - iva),
    iva,
    ivaAcreditable: gravado && Boolean(impuesto.generaCredito) && iva > 0,
  };
}

function sumar(valores) {
  return valores.reduce((acumulado, valor) => redondear(acumulado + valor), 0);
}

export function calcularResumen(entradas) {
  const lineas = entradas.map(calcularLinea);
  const iva = sumar(lineas.map((l) => l.iva));
  const ivaAcreditable = sumar(lineas.filter((l) => l.ivaAcreditable).map((l) => l.iva));
  return {
    lineas,
    total: sumar(lineas.map((l) => l.montoLinea)),
    base: sumar(lineas.map((l) => l.base)),
    iva,
    ivaAcreditable,
    ivaCosto: redondear(iva - ivaAcreditable),
  };
}

const PERMITE_POR_REGIMEN = {
  PEQUENO_CONTRIBUYENTE: (tipo) => tipo !== "IVA_GENERAL",
  EXENTO: (tipo) => tipo === "EXENTO" || tipo === "NO_AFECTO",
};

function permiteGeneral(tipo) {
  return tipo !== "PEQUENO_CONTRIBUYENTE";
}

export function impuestosPermitidos(impuestos, regimenIva) {
  if (!regimenIva) return impuestos;
  const permite = PERMITE_POR_REGIMEN[regimenIva] ?? permiteGeneral;
  return impuestos.filter((i) => permite(i.tipo));
}

export function impuestoPorDefecto(permitidos, regimenIva) {
  const preferido =
    regimenIva === "PEQUENO_CONTRIBUYENTE"
      ? (i) => i.tipo === "PEQUENO_CONTRIBUYENTE"
      : (i) => i.tipo === "IVA_GENERAL" && i.generaCredito;
  return permitidos.find(preferido) ?? permitidos[0] ?? null;
}

export function impuestoDeLinea(linea, permitidos, defecto) {
  return permitidos.find((i) => String(i.id) === String(linea.impuestoId)) ?? defecto;
}

export function fechaLocalAIso(valor) {
  if (!valor || Number.isNaN(new Date(valor).getTime())) return null;
  const minutos = -new Date(valor).getTimezoneOffset();
  const signo = minutos >= 0 ? "+" : "-";
  const absoluto = Math.abs(minutos);
  const horas = String(Math.floor(absoluto / 60)).padStart(2, "0");
  const resto = String(absoluto % 60).padStart(2, "0");
  const base = valor.length === 16 ? `${valor}:00` : valor;
  return `${base}${signo}${horas}:${resto}`;
}

export function dteValido(dte, obligatorio) {
  const llenos = CAMPOS_TRIO_DTE.filter((campo) => dte[campo].trim() !== "");
  if (llenos.length === 0 && !obligatorio) return true;
  const trioCompleto = llenos.length === CAMPOS_TRIO_DTE.length;
  const fechaCompleta = !obligatorio || dte.dteFechaCertificacion !== "";
  return trioCompleto && fechaCompleta && uuidValido(dte.dteUuid);
}

export function dtePayload(dte) {
  return {
    dteUuid: dte.dteUuid.trim() || null,
    dteSerie: dte.dteSerie.trim() || null,
    dteNumero: dte.dteNumero.trim() || null,
    dteFechaCertificacion: fechaLocalAIso(dte.dteFechaCertificacion),
  };
}
