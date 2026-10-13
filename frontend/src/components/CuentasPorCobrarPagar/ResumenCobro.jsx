import { formatoMoneda } from "../../utils/formato";

function ResumenCobro({ resumen }) {
  return (
    <div>
      <p>Total aplicado: {formatoMoneda.format(resumen.totalAplicado)}</p>
      <p>IVA retenido: {formatoMoneda.format(resumen.ivaRetenido)}</p>
      <p>Neto a recibir en banco: {formatoMoneda.format(resumen.neto)}</p>
      {resumen.neto <= 0 && <p className="error-chip">El cobro neto (total aplicado menos IVA retenido) debe ser mayor a cero.</p>}
    </div>
  );
}

export default ResumenCobro;
