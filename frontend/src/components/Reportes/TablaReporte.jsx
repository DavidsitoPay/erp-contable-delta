import { formatoMoneda } from "../../utils/formato";
import { sangriaPorProfundidad } from "../../utils/cuentas";

export function TablaReporte({ children }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>Cuenta</th>
              <th className="numeric">Saldo</th>
            </tr>
          </thead>
          {children}
        </table>
      </div>
    </div>
  );
}

export function FilaMonto({ etiqueta, valor, clase }) {
  return (
    <tr className="row-total">
      <td>{etiqueta}</td>
      <td className={clase ? `numeric ${clase}` : "numeric"}>{formatoMoneda.format(valor)}</td>
    </tr>
  );
}

export function SeccionReporte({ titulo, seccion, mostrarCeros, total = seccion.total, etiquetaTotal = `Total ${titulo}`, children }) {
  const cuentas = mostrarCeros ? seccion.cuentas : seccion.cuentas.filter((c) => c.saldo !== 0);
  return (
    <tbody>
      <tr className="row-section">
        <th colSpan={2} scope="colgroup">
          {titulo}
        </th>
      </tr>
      {cuentas.map((c) => (
        <tr key={c.cuentaId} className={c.esHoja ? undefined : "row-parent"}>
          <td style={{ paddingLeft: sangriaPorProfundidad(c.nivel - 1) }}>{`${c.codigo} - ${c.nombre}`}</td>
          <td className="numeric">{formatoMoneda.format(c.saldo)}</td>
        </tr>
      ))}
      {children}
      <FilaMonto etiqueta={etiquetaTotal} valor={total} />
    </tbody>
  );
}
