import { MONEDA_FUNCIONAL, convertirAGtq, esMonedaExtranjera, monedaDe } from "./cambio";
import { redondear } from "./fiscal";

const CENTAVO = 0.01;

export function errorMonedasPago(monedaCuenta, facturas) {
  const monedas = [...new Set(facturas.map(monedaDe))];
  if (monedas.length > 1) {
    return `Un cobro o pago aplica facturas de una sola moneda; separe las facturas en ${monedas.join(" y ")}.`;
  }
  if (monedas.length === 0 || !esMonedaExtranjera(monedaCuenta) || monedas[0] === monedaCuenta) return "";
  if (monedas[0] === MONEDA_FUNCIONAL) {
    return `RN-27: una factura en quetzales no puede cobrarse ni pagarse con una cuenta en ${monedaCuenta} en esta versión.`;
  }
  return `La moneda de las facturas (${monedas[0]}) no coincide con la de la cuenta bancaria (${monedaCuenta}).`;
}

export function requiereEfectivo(monedaCuenta, facturas) {
  return !esMonedaExtranjera(monedaCuenta) && facturas.some((f) => esMonedaExtranjera(monedaDe(f)));
}

export function librosEstimados(montoAplicado, factura) {
  if (montoAplicado === factura.saldoPendiente) return factura.saldoPendienteGtq;
  const libros = convertirAGtq(montoAplicado, factura.tipoCambioAplicado);
  return libros >= factura.saldoPendienteGtq ? Math.max(redondear(factura.saldoPendienteGtq - CENTAVO), 0) : libros;
}

export function resultadoEstimado(esCobro, libros, valorGtq) {
  return redondear(esCobro ? valorGtq - libros : libros - valorGtq);
}

function sumar(valores) {
  return valores.reduce((total, valor) => redondear(total + valor), 0);
}

export function resumenLiquidacion(esCobro, lineas) {
  const detalle = lineas.map(({ factura, montoAplicado, valorGtq }) => ({
    montoAplicado,
    valorGtq,
    resultado: resultadoEstimado(esCobro, librosEstimados(montoAplicado, factura), valorGtq),
  }));
  return {
    totalAplicado: sumar(detalle.map((d) => d.montoAplicado)),
    totalValor: sumar(detalle.map((d) => d.valorGtq)),
    diferencial: sumar(detalle.map((d) => d.resultado)),
  };
}

export function diferenciaTransferencia(origenEsGtq, montoOrigen, montoDestino, tasa) {
  const valorUsd = convertirAGtq(origenEsGtq ? montoDestino : montoOrigen, tasa);
  const valorGtq = origenEsGtq ? montoOrigen : montoDestino;
  return redondear(origenEsGtq ? valorUsd - valorGtq : valorGtq - valorUsd);
}
