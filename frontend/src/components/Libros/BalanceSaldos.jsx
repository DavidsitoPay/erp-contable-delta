import { useEffect, useState } from "react";
import { librosApi } from "../../services/api";
import { formatoMoneda, mensajeError } from "../../utils/formato";
import { sangriaPorProfundidad } from "../../utils/cuentas";

const aCentavos = (valor) => Math.round(valor * 100);
const dinero = (centavos) => formatoMoneda.format(centavos / 100);
const sinMovimiento = (f) => f.totalDebito === 0 && f.totalCredito === 0;

function saldosEnCentavos(f) {
  const neto = aCentavos(f.totalDebito) - aCentavos(f.totalCredito);
  return { deudor: Math.max(neto, 0), acreedor: Math.max(-neto, 0) };
}

function calcularTotales(filas) {
  return filas
    .filter((f) => f.nivel === 1)
    .reduce(
      (acc, f) => {
        const { deudor, acreedor } = saldosEnCentavos(f);
        return {
          debito: acc.debito + aCentavos(f.totalDebito),
          credito: acc.credito + aCentavos(f.totalCredito),
          deudor: acc.deudor + deudor,
          acreedor: acc.acreedor + acreedor,
        };
      },
      { debito: 0, credito: 0, deudor: 0, acreedor: 0 },
    );
}

function BalanceSaldos() {
  const [filas, setFilas] = useState([]);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(true);
  const [ocultarSinMovimiento, setOcultarSinMovimiento] = useState(false);
  const [nivelMaximo, setNivelMaximo] = useState("");

  useEffect(() => {
    librosApi
      .balanceSaldos()
      .then(({ data }) => setFilas(data))
      .catch((err) => setError(mensajeError(err, "No se pudo cargar el balance de saldos.")))
      .finally(() => setCargando(false));
  }, []);

  const maxNivel = filas.reduce((max, f) => Math.max(max, f.nivel), 1);
  const niveles = Array.from({ length: maxNivel }, (_, i) => i + 1);
  const visibles = filas.filter(
    (f) => !(ocultarSinMovimiento && sinMovimiento(f)) && (nivelMaximo === "" || f.nivel <= Number(nivelMaximo)),
  );
  const totales = calcularTotales(filas);
  const diferencia = totales.debito - totales.credito;

  return (
    <div>
      <h2>Balance de saldos</h2>
      <p>Calculado en tiempo real a partir de los asientos confirmados. Una cuenta sin movimiento aparece con saldo 0.</p>

      {error && <p className="error-chip">{error}</p>}
      {cargando ? (
        <p>Cargando...</p>
      ) : (
        <>
          <div className="catalog-form">
            <label>
              <input
                type="checkbox"
                checked={ocultarSinMovimiento}
                onChange={(e) => setOcultarSinMovimiento(e.target.checked)}
              />
              <span>Ocultar cuentas sin movimiento</span>
            </label>
            <select
              className="select"
              aria-label="Nivel máximo"
              value={nivelMaximo}
              onChange={(e) => setNivelMaximo(e.target.value)}
            >
              <option value="">Todos los niveles</option>
              {niveles.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </div>
          <div className="table-wrap">
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre</th>
                    <th>Naturaleza</th>
                    <th className="numeric">Total débito</th>
                    <th className="numeric">Total crédito</th>
                    <th className="numeric">Saldo deudor</th>
                    <th className="numeric">Saldo acreedor</th>
                  </tr>
                </thead>
                <tbody>
                  {visibles.map((f) => {
                    const saldos = saldosEnCentavos(f);
                    return (
                      <tr key={f.cuentaId} className={f.esHoja ? undefined : "row-parent"}>
                        <td style={{ paddingLeft: sangriaPorProfundidad(f.nivel - 1) }}>{f.codigo}</td>
                        <td>
                          {f.nombre}
                          {f.activa === false ? " (inactiva)" : ""}
                        </td>
                        <td>{f.naturaleza}</td>
                        <td className="numeric">{formatoMoneda.format(f.totalDebito)}</td>
                        <td className="numeric">{formatoMoneda.format(f.totalCredito)}</td>
                        <td className="numeric">{dinero(saldos.deudor)}</td>
                        <td className="numeric">{dinero(saldos.acreedor)}</td>
                      </tr>
                    );
                  })}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={3}>Totales</td>
                    <td className="numeric">{dinero(totales.debito)}</td>
                    <td className="numeric">{dinero(totales.credito)}</td>
                    <td className="numeric">{dinero(totales.deudor)}</td>
                    <td className="numeric">{dinero(totales.acreedor)}</td>
                  </tr>
                  {diferencia !== 0 && (
                    <tr>
                      <td colSpan={3}>Diferencia</td>
                      <td colSpan={4} className="numeric text-danger">
                        {dinero(Math.abs(diferencia))}
                      </td>
                    </tr>
                  )}
                </tfoot>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default BalanceSaldos;
