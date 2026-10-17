import { formatoMoneda } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

function VistaPreviaPartida({ vista }) {
  return (
    <>
      <TablaCatalogo encabezados={["Cuenta", "Nombre", "Debe", "Haber"]}>
        {vista.lineas.map((l) => (
          <tr key={l.cuentaId}>
            <td>{l.codigo}</td>
            <td>{l.nombre}</td>
            <td className="numeric">{formatoMoneda.format(l.debe)}</td>
            <td className="numeric">{formatoMoneda.format(l.haber)}</td>
          </tr>
        ))}
      </TablaCatalogo>
      <p>{`Saldo por enterar: ${formatoMoneda.format(vista.saldoAntes)} → ${formatoMoneda.format(vista.saldoDespues)}`}</p>
    </>
  );
}

export default VistaPreviaPartida;
