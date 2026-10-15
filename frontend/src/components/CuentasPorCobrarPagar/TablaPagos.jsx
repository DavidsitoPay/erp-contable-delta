import { esMonedaExtranjera, monedaDe } from "../../utils/cambio";
import { formatoDiferencial, formatoImporte, formatoQuetzales } from "../../utils/formato";

function textoAplicacion(aplicacion, moneda) {
  const retenido = Number(aplicacion.retencionIva ?? 0);
  const detalle = retenido > 0 ? `, IVA retenido ${formatoImporte(retenido)}` : "";
  return `${aplicacion.numero} (${formatoImporte(aplicacion.montoAplicado, moneda)}${detalle})`;
}

function diferencialDe(pago) {
  return pago.aplicaciones.reduce((suma, a) => suma + Number(a.diferencialGtq ?? 0), 0);
}

function TablaPagos({ pagos, etiquetaContraparte, campoContraparteNombre, conNeto = false }) {
  const conMoneda = pagos.some((p) => esMonedaExtranjera(monedaDe(p)));
  const columnas = 5 + (conNeto ? 1 : 0) + (conMoneda ? 3 : 0);

  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>{etiquetaContraparte}</th>
              <th>Fecha</th>
              {conMoneda && <th>Moneda</th>}
              <th className="numeric">Monto</th>
              {conMoneda && <th className="numeric">Valor en Q</th>}
              {conMoneda && <th className="numeric">Diferencial</th>}
              {conNeto && <th className="numeric">Neto cobrado</th>}
              <th>Método</th>
              <th>Aplicado a</th>
            </tr>
          </thead>
          <tbody>
            {pagos.length === 0 ? (
              <tr><td colSpan={columnas}>No hay pagos registrados todavía.</td></tr>
            ) : (
              pagos.map((p) => {
                const moneda = monedaDe(p);
                return (
                  <tr key={p.id}>
                    <td>{p[campoContraparteNombre]}</td>
                    <td>{p.fecha}</td>
                    {conMoneda && <td>{moneda}</td>}
                    <td className="numeric">{formatoImporte(p.montoTotal, moneda)}</td>
                    {conMoneda && <td className="numeric">{formatoQuetzales(p.montoTotalGtq ?? p.montoTotal)}</td>}
                    {conMoneda && <td className="numeric">{formatoDiferencial(diferencialDe(p))}</td>}
                    {conNeto && <td className="numeric">{formatoImporte(p.montoNeto ?? p.montoTotal, moneda)}</td>}
                    <td>{p.metodoPago}</td>
                    <td>{p.aplicaciones.map((a) => textoAplicacion(a, moneda)).join(", ")}</td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TablaPagos;
