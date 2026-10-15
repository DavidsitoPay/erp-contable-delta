import { esMonedaExtranjera, monedaDeCuenta } from "../../utils/cambio";
import { formatoDiferencial, monto } from "../../utils/formato";
import { diferenciaTransferencia } from "../../utils/liquidacion";
import PanelTipoCambio from "../TiposCambio/PanelTipoCambio";

function TransferenciaMoneda({ origen, destino, fecha, montoOrigen, montoDestino, onMontoDestino, cambio }) {
  const monedaDestino = monedaDeCuenta(destino);
  const origenEsGtq = !esMonedaExtranjera(monedaDeCuenta(origen));
  const estimable = Number.isFinite(cambio.tasa) && monto(montoOrigen) > 0 && monto(montoDestino) > 0;
  const diferencia = estimable ? diferenciaTransferencia(origenEsGtq, monto(montoOrigen), monto(montoDestino), cambio.tasa) : null;

  return (
    <>
      <input
        type="number"
        className="input input-money"
        aria-label={`Monto destino en ${monedaDestino}`}
        min="0.01"
        step="0.01"
        placeholder="0.00"
        value={montoDestino}
        onChange={(e) => onMontoDestino(e.target.value)}
        required
      />
      <PanelTipoCambio estado={cambio} moneda="USD" fecha={fecha} titulo="Referencia USD de la fecha" />
      {diferencia !== null && (
        <p className="nota-fiscal">{`Diferencia vs. referencia: ${formatoDiferencial(diferencia)}; se asienta a ganancia o pérdida realizada.`}</p>
      )}
    </>
  );
}

export default TransferenciaMoneda;
