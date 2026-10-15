import { esMonedaExtranjera, formatoTasa, monedaDeCuenta } from "../../utils/cambio";
import { etiquetaCuenta } from "../../utils/cuentas";
import { formatoImporte, formatoQuetzales } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS_INICIO = ["Fecha", "Cuenta", "Tipo", "Monto"];
const ENCABEZADOS_MONEDA = ["Valor en Q", "Tipo de cambio"];
const ENCABEZADOS_FIN = ["Descripción", "Referencia", "Origen", "Asiento"];

function CeldasMoneda({ movimiento, extranjera }) {
  return (
    <>
      <td className="numeric">{extranjera ? formatoQuetzales(movimiento.montoGtq) : null}</td>
      <td className="numeric">{extranjera ? formatoTasa(movimiento.tipoCambio) : null}</td>
    </>
  );
}

function TablaMovimientos({ movimientos, cuentasPorId, accion }) {
  const conConciliado = movimientos.some((m) => m.conciliado !== undefined);
  const monedaDe = (m) => monedaDeCuenta(cuentasPorId[m.cuentaBancariaId]);
  const conMoneda = movimientos.some((m) => esMonedaExtranjera(monedaDe(m)));
  const encabezados = [
    ...ENCABEZADOS_INICIO,
    ...(conMoneda ? ENCABEZADOS_MONEDA : []),
    ...ENCABEZADOS_FIN,
    ...(conConciliado ? ["Conciliado"] : []),
  ];

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
            <td className="numeric">{formatoImporte(m.monto, monedaDe(m))}</td>
            {conMoneda && <CeldasMoneda movimiento={m} extranjera={esMonedaExtranjera(monedaDe(m))} />}
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
