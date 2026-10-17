import { formatoMoneda } from "../../utils/formato";

function CeldasSaldoPeriodo({ fila, saldo }) {
  return (
    <>
      <td>{fila.periodo.slice(0, 7)}</td>
      <td className="numeric">{formatoMoneda.format(fila.retenido)}</td>
      <td className="numeric">{formatoMoneda.format(fila.enterado)}</td>
      <td className="numeric">{formatoMoneda.format(saldo)}</td>
    </>
  );
}

export default CeldasSaldoPeriodo;
