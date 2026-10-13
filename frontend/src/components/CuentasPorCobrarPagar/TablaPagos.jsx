import { formatoMoneda } from "../../utils/formato";

function textoAplicacion(aplicacion) {
  const retenido = Number(aplicacion.retencionIva ?? 0);
  const detalle = retenido > 0 ? `, IVA retenido ${formatoMoneda.format(retenido)}` : "";
  return `${aplicacion.numero} (${formatoMoneda.format(aplicacion.montoAplicado)}${detalle})`;
}

function TablaPagos({ pagos, etiquetaContraparte, campoContraparteNombre, conNeto = false }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>{etiquetaContraparte}</th>
              <th>Fecha</th>
              <th className="numeric">Monto</th>
              {conNeto && <th className="numeric">Neto cobrado</th>}
              <th>Método</th>
              <th>Aplicado a</th>
            </tr>
          </thead>
          <tbody>
            {pagos.length === 0 ? (
              <tr><td colSpan={conNeto ? 6 : 5}>No hay pagos registrados todavía.</td></tr>
            ) : (
              pagos.map((p) => (
                <tr key={p.id}>
                  <td>{p[campoContraparteNombre]}</td>
                  <td>{p.fecha}</td>
                  <td className="numeric">{formatoMoneda.format(p.montoTotal)}</td>
                  {conNeto && <td className="numeric">{formatoMoneda.format(p.montoNeto ?? p.montoTotal)}</td>}
                  <td>{p.metodoPago}</td>
                  <td>{p.aplicaciones.map(textoAplicacion).join(", ")}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TablaPagos;
