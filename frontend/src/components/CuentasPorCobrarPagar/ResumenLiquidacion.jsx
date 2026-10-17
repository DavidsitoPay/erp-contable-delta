import { formatoDiferencial, formatoImporte, formatoQuetzales } from "../../utils/formato";

function ResumenLiquidacion({ resumen, moneda }) {
  if (!resumen) return null;
  return (
    <div>
      <p>Total aplicado: {formatoImporte(resumen.totalAplicado, moneda)}</p>
      <p>Valor en Q: {formatoQuetzales(resumen.totalValor)}</p>
      <p>Diferencial estimado: {formatoDiferencial(resumen.diferencial)} (el sistema recalcula)</p>
    </div>
  );
}

export default ResumenLiquidacion;
