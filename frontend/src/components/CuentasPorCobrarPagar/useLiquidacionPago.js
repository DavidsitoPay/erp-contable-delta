import { useState } from "react";
import { convertirAGtq, esMonedaExtranjera, monedaDe, monedaDeCuenta } from "../../utils/cambio";
import { monto } from "../../utils/formato";
import { errorMonedasPago, requiereEfectivo, resumenLiquidacion } from "../../utils/liquidacion";
import { useTipoCambio } from "../TiposCambio/useTipoCambio";

export function useLiquidacionPago({ cuenta, fecha, facturas, montosAplicados, esCobro }) {
  const [efectivos, setEfectivos] = useState({});
  const monedaCuenta = monedaDeCuenta(cuenta);
  const aplicadas = facturas.filter((f) => monto(montosAplicados[f.id]) > 0);
  const errorMonedas = errorMonedasPago(monedaCuenta, aplicadas);
  const cambioPago = useTipoCambio({ moneda: monedaCuenta, fecha });
  const pideEfectivo = requiereEfectivo(monedaCuenta, aplicadas);
  const hayExtranjera = esMonedaExtranjera(monedaCuenta) || aplicadas.some((f) => esMonedaExtranjera(monedaDe(f)));

  const lineas = aplicadas.map((factura) => {
    const montoAplicado = monto(montosAplicados[factura.id]);
    const valorGtq = pideEfectivo ? monto(efectivos[factura.id]) : convertirAGtq(montoAplicado, cambioPago.tasa ?? 0);
    return { factura, montoAplicado, valorGtq };
  });

  const efectivoCompleto = !pideEfectivo || lineas.every((l) => l.valorGtq > 0);
  const calculable = hayExtranjera && lineas.length > 0 && errorMonedas === "" && efectivoCompleto && !cambioPago.bloqueado;

  function setEfectivo(documentoId, valor) {
    setEfectivos((previos) => ({ ...previos, [documentoId]: valor }));
  }

  function conEfectivo(aplicaciones) {
    if (!pideEfectivo) return aplicaciones;
    return aplicaciones.map((a) => ({ ...a, valorEfectivoGtq: monto(efectivos[a.documentoId]) }));
  }

  return {
    monedaCuenta,
    moneda: aplicadas.length > 0 ? monedaDe(aplicadas[0]) : monedaCuenta,
    errorMonedas,
    cambioPago,
    resumen: calculable ? resumenLiquidacion(esCobro, lineas) : null,
    listo: errorMonedas === "" && !cambioPago.bloqueado && efectivoCompleto,
    tabla: { pideEfectivo, efectivos, onEfectivo: setEfectivo, tasaPago: cambioPago.tasa },
    conEfectivo,
    reiniciar: () => setEfectivos({}),
  };
}
