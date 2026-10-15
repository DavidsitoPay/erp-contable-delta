import { esMonedaExtranjera, estadoHeredado, monedaDe } from "../../utils/cambio";
import { useTipoCambio } from "../TiposCambio/useTipoCambio";

export function useCambioDocumento({ monedaCodigo, fecha, origen, esNotaCredito }) {
  const moneda = esNotaCredito ? monedaDe(origen) : monedaCodigo;
  const consultado = useTipoCambio({ moneda, fecha, activo: !esNotaCredito });
  const requerido = esMonedaExtranjera(moneda);
  const estado = esNotaCredito && requerido ? estadoHeredado(origen) : consultado;
  const tasa = requerido ? estado.tasa : 1;

  return { moneda, estado, tasa, bloqueado: requerido && !Number.isFinite(tasa) };
}
