import { etiquetaCuenta } from "../../utils/cuentas";
import { formatoMoneda } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS_BASE = ["Fecha", "Cuenta", "Tipo", "Monto", "Descripción", "Referencia", "Origen", "Asiento"];

function TablaMovimientos({ movimientos, cuentasPorId, accion }) {
  const conConciliado = movimientos.some((m) => m.conciliado !== undefined);
  const encabezados = conConciliado ? [...ENCABEZADOS_BASE, "Conciliado"] : ENCABEZADOS_BASE;

  return (
    <TablaCatalogo encabezados={encabezados}>
      {movimientos.length === 0 ? (
        <tr><td colSpan={encabezados.length + 1}>No hay movimientos.</td></tr>
      ) : (
        movimientos.map((m) => (
          <tr key={m.id}>
            <td>{m.fecha}</td>
            <td>{etiquetaCuenta(cuentasPorId[m.cuentaBancariaId])}</td>
            <td>{m.tipo}</td>
            <td className="numeric">{formatoMoneda.format(m.monto)}</td>
            <td>{m.descripcion}</td>
            <td>{m.referencia}</td>
            <td>{m.origen}</td>
            <td>{m.asientoId}</td>
            {conConciliado && <td>{m.conciliado ? "Sí" : "No"}</td>}
            <td>
              {accion && (
                <button type="button" className="btn btn-outline btn-sm" aria-label={`${accion.etiqueta} movimiento ${m.id}`} onClick={() => accion.onClick(m)}>
                  {accion.etiqueta}
                </button>
              )}
            </td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

export default TablaMovimientos;
