import { TIPOS_BIEN_SERVICIO } from "../../utils/etiquetasFiscales";
import { impuestoDeLinea } from "../../utils/fiscal";
import { formatoMoneda } from "../../utils/formato";
import OpcionesSelect from "../OpcionesSelect";

function LineasFactura({ lineas, resumen, permitidos, defecto, hojas, centros, etiquetaCuentaLinea, onActualizar, onQuitar }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>Descripción</th>
              <th>Cantidad</th>
              <th>Precio unitario (IVA incluido)</th>
              <th>Impuesto</th>
              <th>Tipo</th>
              <th>{etiquetaCuentaLinea}</th>
              <th>Centro de costo</th>
              <th className="numeric">Base</th>
              <th className="numeric">IVA</th>
              <th className="numeric">Total línea</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {lineas.map((l, index) => {
              const impuesto = impuestoDeLinea(l, permitidos, defecto);
              const calculo = resumen.lineas[index];
              return (
                <tr key={l.id}>
                  <td><input className="input" aria-label="Descripción de línea" value={l.descripcion} onChange={(e) => onActualizar(index, "descripcion", e.target.value)} /></td>
                  <td className="numeric"><input type="number" className="input input-money" aria-label="Cantidad" min="0" step="0.01" value={l.cantidad} onChange={(e) => onActualizar(index, "cantidad", e.target.value)} /></td>
                  <td className="numeric"><input type="number" className="input input-money" aria-label="Precio unitario" min="0" step="0.01" value={l.precioUnitario} onChange={(e) => onActualizar(index, "precioUnitario", e.target.value)} /></td>
                  <td>
                    <select className="select" aria-label="Impuesto de la línea" value={impuesto ? String(impuesto.id) : ""} onChange={(e) => onActualizar(index, "impuestoId", e.target.value)} disabled={permitidos.length === 0}>
                      {permitidos.length === 0 && <option value="">Sin impuestos vigentes</option>}
                      {permitidos.map((i) => <option key={i.id} value={i.id}>{i.nombre}</option>)}
                    </select>
                  </td>
                  <td>
                    <select className="select" aria-label="Tipo de bien o servicio" value={l.tipoBienServicio} onChange={(e) => onActualizar(index, "tipoBienServicio", e.target.value)}>
                      <OpcionesSelect opciones={TIPOS_BIEN_SERVICIO} />
                    </select>
                  </td>
                  <td>
                    <select className="select" aria-label="Cuenta de la línea" value={l.cuentaContableId} onChange={(e) => onActualizar(index, "cuentaContableId", e.target.value)}>
                      <option value="">Selecciona cuenta</option>
                      {hojas.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
                    </select>
                  </td>
                  <td>
                    <select className="select" aria-label="Centro de costo de la línea" value={l.centroCostoId} onChange={(e) => onActualizar(index, "centroCostoId", e.target.value)}>
                      <option value="">(ninguno)</option>
                      {centros.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
                    </select>
                  </td>
                  <td className="numeric">{formatoMoneda.format(calculo.base)}</td>
                  <td className="numeric">{formatoMoneda.format(calculo.iva)}</td>
                  <td className="numeric">{formatoMoneda.format(calculo.montoLinea)}</td>
                  <td>
                    <button type="button" className="btn btn-danger-outline btn-sm" onClick={() => onQuitar(index)} disabled={lineas.length <= 1}>Quitar</button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default LineasFactura;
