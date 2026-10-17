import { redondear } from "./fiscal";

export const MONEDA_FUNCIONAL = "GTQ";
export const MONEDAS_DOCUMENTO = ["GTQ", "USD", "EUR"];
export const MONEDAS_EXTRANJERAS = ["USD", "EUR"];
export const MONEDAS_CUENTA = ["GTQ", "USD"];

export const AVISO_BANGUAT = "Referencia de Banguat; la captura manual es un respaldo y exige motivo.";
export const AVISO_SIN_PERMISO_TASA = "Solicite a un Contador que capture el tipo de cambio.";

export const monedaDe = (registro) => registro?.moneda ?? MONEDA_FUNCIONAL;
export const monedaDeCuenta = (cuenta) => cuenta?.monedaCodigo ?? MONEDA_FUNCIONAL;
export const esMonedaExtranjera = (codigo) => Boolean(codigo) && codigo !== MONEDA_FUNCIONAL;
export const camposMoneda = (codigo) => (esMonedaExtranjera(codigo) ? { monedaCodigo: codigo } : {});

export function formatoTasa(valor) {
  return Number(valor).toFixed(6);
}

export function convertirAGtq(original, tasa) {
  return redondear(original * tasa);
}

export function tasaValida(valor) {
  return Number.isFinite(valor) && valor > 0 && valor <= 99999.999999 && Math.round(valor * 1e6) / 1e6 === valor;
}

export function etiquetaFuente(tipoCambio) {
  const fuente = tipoCambio.fuente ?? "Sin fuente";
  return tipoCambio.arrastrado ? `${fuente} (arrastrado)` : fuente;
}

export function textoTipoCambio({ tipoCambioAplicado, tipoCambioFuente, tipoCambioArrastrado }) {
  const fuente = etiquetaFuente({ fuente: tipoCambioFuente, arrastrado: tipoCambioArrastrado });
  return `${formatoTasa(tipoCambioAplicado)} (${fuente})`;
}

export function estadoHeredado(origen) {
  return {
    cargando: false,
    consultado: true,
    error: "",
    disponible: true,
    heredado: true,
    tasa: origen.tipoCambioAplicado,
    tipoCambio: {
      tasa: origen.tipoCambioAplicado,
      fuente: origen.tipoCambioFuente,
      arrastrado: Boolean(origen.tipoCambioArrastrado),
      fechaReferencia: null,
    },
    propuesta: null,
    mensaje: "",
  };
}
