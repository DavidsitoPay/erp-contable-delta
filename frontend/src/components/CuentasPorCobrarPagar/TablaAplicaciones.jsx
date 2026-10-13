import { Fragment } from "react";
import { formatoMoneda, monto } from "../../utils/formato";
import { valorRetencion } from "../../utils/retenciones";
import RetencionIvaCobro from "./RetencionIvaCobro";

function TablaAplicaciones({ facturas, montosAplicados, onMonto, retencionIva }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr><th>Factura</th><th className="numeric">Saldo pendiente</th><th className="numeric">Monto a aplicar</th></tr>
          </thead>
          <tbody>
            {facturas.length === 0 ? (
              <tr><td colSpan="3">No hay facturas con saldo pendiente para esta selección.</td></tr>
            ) : (
              facturas.map((f) => (
                <Fragment key={f.id}>
                  <tr>
                    <td>{f.numero}</td>
                    <td className="numeric">{formatoMoneda.format(f.saldoPendiente)}</td>
                    <td className="numeric">
                      <input
                        type="number"
                        className="input input-money"
                        aria-label={`Monto a aplicar a ${f.numero}`}
                        min="0"
                        max={f.saldoPendiente}
                        step="0.01"
                        placeholder="0.00"
                        value={montosAplicados[f.id] || ""}
                        onChange={(e) => onMonto(f.id, e.target.value)}
                      />
                    </td>
                  </tr>
                  {retencionIva && monto(montosAplicados[f.id]) > 0 && (
                    <tr>
                      <td colSpan="3">
                        <RetencionIvaCobro
                          numeroFactura={f.numero}
                          montoAplicado={monto(montosAplicados[f.id])}
                          valor={valorRetencion(retencionIva.valores, f.id)}
                          sugerencia={retencionIva.sugerencias[f.id]}
                          clienteAgente={retencionIva.clienteAgente}
                          onCambio={(cambios) => retencionIva.onCambio(f.id, cambios)}
                        />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TablaAplicaciones;
