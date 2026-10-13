export const formatoMoneda = new Intl.NumberFormat("es-GT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function monto(valor) {
  const n = Number.parseFloat(valor);
  return Number.isFinite(n) ? n : 0;
}

export function hoyIso() {
  return new Date().toISOString().slice(0, 10);
}

export function mensajeError(err, porDefecto) {
  return err.response?.data?.error || porDefecto;
}

export function formatoFechaHora(valor) {
  return new Date(valor).toLocaleString("es-GT");
}
export const MESES = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
