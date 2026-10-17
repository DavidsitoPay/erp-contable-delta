import { CONCEPTOS_RETENCION, SENTIDOS_RETENCION, etiquetaDe } from "../../utils/etiquetasFiscales";
import { formatoMoneda } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const AVISO_COBRO_OTRO_MES = "La diferencia se explica por cobros registrados en un mes distinto al de la factura.";
const AVISO_SIN_EXPLICACION = "La conciliación tiene diferencias que no se explican por cobros en otro mes; revise los asientos de la cuenta.";

function ResumenRetenciones({ reporte }) {
  const hayDiferencia = reporte.conciliacion.some((c) => Math.abs(c.diferencia) >= 0.005);
  const hayCobrosEnOtroMes = reporte.filas.some((f) => f.periodoContableDistinto);

  return (
    <>
      <h3>Totales por impuesto</h3>
      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Sentido</th>
                <th>Impuesto</th>
                <th className="numeric">Cantidad</th>
                <th className="numeric">Base</th>
                <th className="numeric">Retención</th>
                <th className="numeric">Sin constancia</th>
              </tr>
            </thead>
            <tbody>
              {reporte.totales.map((t) => (
                <tr key={`${t.sentido}-${t.concepto}`}>
                  <td>{etiquetaDe(SENTIDOS_RETENCION, t.sentido)}</td>
                  <td>{etiquetaDe(CONCEPTOS_RETENCION, t.concepto)}</td>
                  <td className="numeric">{t.cantidad}</td>
                  <td className="numeric">{formatoMoneda.format(t.base)}</td>
                  <td className="numeric">{formatoMoneda.format(t.monto)}</td>
                  <td className="numeric">{t.sinConstancia}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {reporte.totalesTipoRenta?.length > 0 && (
        <>
          <h3>Totales por tipo de renta</h3>
          <TablaCatalogo encabezados={["Tipo de renta", "Cantidad", "Base (GTQ)", "Retención (GTQ)"]}>
            {reporte.totalesTipoRenta.map((t) => (
              <tr key={t.tipoRentaCodigo}>
                <td>{t.tipoRentaNombre}</td>
                <td className="numeric">{t.cantidad}</td>
                <td className="numeric">{formatoMoneda.format(t.base)}</td>
                <td className="numeric">{formatoMoneda.format(t.monto)}</td>
              </tr>
            ))}
          </TablaCatalogo>
        </>
      )}

      {reporte.saldosEnterar?.length > 0 && (
        <>
          <h3>Saldo por enterar por período</h3>
          <TablaCatalogo encabezados={["Período", "Retenido", "Enterado", "Saldo por enterar"]}>
            {reporte.saldosEnterar.map((s) => (
              <tr key={s.periodo}>
                <td>{s.periodo.slice(0, 7)}</td>
                <td className="numeric">{formatoMoneda.format(s.retenido)}</td>
                <td className="numeric">{formatoMoneda.format(s.enterado)}</td>
                <td className="numeric">{formatoMoneda.format(s.saldoPorEnterar)}</td>
              </tr>
            ))}
          </TablaCatalogo>
        </>
      )}

      <h3>Conciliación con las cuentas de retención</h3>
      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Impuesto</th>
                <th>Cuenta</th>
                <th className="numeric">Total del reporte</th>
                <th className="numeric">Movimiento contable</th>
                <th className="numeric">Diferencia</th>
              </tr>
            </thead>
            <tbody>
              {reporte.conciliacion.map((c) => (
                <tr key={c.concepto}>
                  <td>{etiquetaDe(CONCEPTOS_RETENCION, c.concepto)}</td>
                  <td>{c.cuentaCodigo}</td>
                  <td className="numeric">{formatoMoneda.format(c.totalReporte)}</td>
                  <td className="numeric">{formatoMoneda.format(c.movimientoContable)}</td>
                  <td className="numeric">{formatoMoneda.format(c.diferencia)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      {hayDiferencia && <p className="warning-chip">{hayCobrosEnOtroMes ? AVISO_COBRO_OTRO_MES : AVISO_SIN_EXPLICACION}</p>}
    </>
  );
}

export default ResumenRetenciones;
