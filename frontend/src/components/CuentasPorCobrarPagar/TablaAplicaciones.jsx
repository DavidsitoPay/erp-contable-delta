import { Fragment } from "react";
import { convertirAGtq, esMonedaExtranjera, monedaDe } from "../../utils/cambio";
import { formatoImporte, formatoQuetzales, monto } from "../../utils/formato";
import { valorRetencion } from "../../utils/retenciones";
import RetencionIvaCobro from "./RetencionIvaCobro";

function CeldasLiquidacion({ factura, montoAplicado, liquidacion }) {
  const extranjera = esMonedaExtranjera(monedaDe(factura));
  return (
    <>
      {liquidacion.pideEfectivo && (
        <td className="numeric">
          {extranjera && (
            <input
              type="number"
              className="input input-money"
              aria-label={`Efectivo en Q de ${factura.numero}`}
              min="0"
              step="0.01"
              placeholder="0.00"
              value={liquidacion.efectivos[factura.id] || ""}
              onChange={(e) => liquidacion.onEfectivo(factura.id, e.target.value)}
            />
          )}
        </td>
      )}
      {Boolean(liquidacion.tasaPago) && (
        <td className="numeric">{extranjera ? formatoQuetzales(convertirAGtq(montoAplicado, liquidacion.tasaPago)) : null}</td>
      )}
    </>
  );
}

function TablaAplicaciones({ facturas, montosAplicados, onMonto, retencionIva, liquidacion = null }) {
  const columnas = 3 + (liquidacion?.pideEfectivo ? 1 : 0) + (liquidacion?.tasaPago ? 1 : 0);

  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>Factura</th>
              <th className="numeric">Saldo pendiente</th>
              <th className="numeric">Monto a aplicar</th>
              {liquidacion?.pideEfectivo && <th className="numeric">Efectivo en Q</th>}
              {Boolean(liquidacion?.tasaPago) && <th className="numeric">Valor en Q</th>}
            </tr>
          </thead>
          <tbody>
            {facturas.length === 0 ? (
              <tr><td colSpan={columnas}>No hay facturas con saldo pendiente para esta selección.</td></tr>
            ) : (
              facturas.map((f) => (
                <Fragment key={f.id}>
                  <tr>
                    <td>{f.numero}</td>
                    <td className="numeric">{formatoImporte(f.saldoPendiente, monedaDe(f))}</td>
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
                    {liquidacion && <CeldasLiquidacion factura={f} montoAplicado={monto(montosAplicados[f.id])} liquidacion={liquidacion} />}
                  </tr>
                  {retencionIva && !esMonedaExtranjera(monedaDe(f)) && monto(montosAplicados[f.id]) > 0 && (
                    <tr>
                      <td colSpan={columnas}>
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
