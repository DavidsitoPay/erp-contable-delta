import { formatoMoneda } from "../../utils/formato";

const COLUMNAS = 10;

function TablaFacturas({ facturas, etiquetaContraparte, campoContraparteNombre }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>Número</th>
              <th>{etiquetaContraparte}</th>
              <th>Fecha</th>
              <th>Vencimiento</th>
              <th className="numeric">Base</th>
              <th className="numeric">IVA</th>
              <th className="numeric">Monto total</th>
              <th className="numeric">Saldo pendiente</th>
              <th>DTE</th>
              <th>Estado</th>
            </tr>
          </thead>
          <tbody>
            {facturas.length === 0 ? (
              <tr><td colSpan={COLUMNAS}>No hay facturas registradas todavía.</td></tr>
            ) : (
              facturas.map((f) => (
                <tr key={f.id}>
                  <td>
                    {f.numero}
                    {f.calculoLegado && <small className="nota-fiscal">(legado)</small>}
                  </td>
                  <td>{f[campoContraparteNombre]}</td>
                  <td>{f.fecha}</td>
                  <td>{f.fechaVencimiento}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoBase)}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoIva)}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoTotal)}</td>
                  <td className="numeric">{formatoMoneda.format(f.saldoPendiente)}</td>
                  <td>{f.dteSerie ? `${f.dteSerie}-${f.dteNumero}` : "—"}</td>
                  <td>{f.estado}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TablaFacturas;
