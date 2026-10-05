import { useEffect, useState } from "react";
import { cuentasApi, librosApi, periodosApi } from "../../services/api";
import { formatoMoneda } from "../../utils/formato";
import { etiquetaSubcuentas } from "../../utils/cuentas";

function LibroMayor() {
  const [cuentas, setCuentas] = useState([]);
  const [periodos, setPeriodos] = useState([]);
  const [cuentaId, setCuentaId] = useState("");
  const [periodoId, setPeriodoId] = useState("");
  const [resultado, setResultado] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  useEffect(() => {
    void cuentasApi.listar(true).then(({ data }) => setCuentas(data));
    void periodosApi.listar().then(({ data }) => setPeriodos(data));
  }, []);

  useEffect(() => {
    if (!cuentaId) {
      setResultado(null);
      return;
    }
    setError("");
    setCargando(true);
    librosApi
      .mayor(Number(cuentaId), periodoId ? Number(periodoId) : undefined)
      .then(({ data }) => setResultado(data))
      .catch((err) => setError(err.response?.data?.error || "No se pudo cargar el libro mayor."))
      .finally(() => setCargando(false));
  }, [cuentaId, periodoId]);

  const consolidado = resultado?.cuenta.esHoja === false;

  return (
    <div>
      <h2>Libro mayor</h2>

      <div className="catalog-form">
        <select className="select" aria-label="Cuenta" value={cuentaId} onChange={(e) => setCuentaId(e.target.value)}>
          <option value="">Selecciona cuenta</option>
          {cuentas.map((c) => (
            <option key={c.id} value={c.id}>
              {c.codigo} - {c.nombre}
            </option>
          ))}
        </select>
        <select className="select" aria-label="Periodo (opcional)" value={periodoId} onChange={(e) => setPeriodoId(e.target.value)}>
          <option value="">Todos los periodos</option>
          {periodos.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nombre}
            </option>
          ))}
        </select>
      </div>

      {error && <p className="error-chip">{error}</p>}
      {cargando && <p>Cargando...</p>}

      {resultado && (
        <>
          <p>
            <strong>
              {resultado.cuenta.codigo} - {resultado.cuenta.nombre}
            </strong>{" "}
            ({resultado.cuenta.naturaleza})
          </p>
          {consolidado && <p>Consolidado de {etiquetaSubcuentas(resultado.cuenta.subcuentas)}</p>}

          {resultado.movimientos.length === 0 ? (
            <p>Esta cuenta no tiene movimientos confirmados{periodoId ? " en el periodo seleccionado" : ""}.</p>
          ) : (
            <div className="table-wrap">
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Asiento</th>
                      {consolidado && <th>Cuenta</th>}
                      <th className="numeric">Débito</th>
                      <th className="numeric">Crédito</th>
                      <th className="numeric">Saldo acumulado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {resultado.movimientos.map((m, i) => (
                      <tr key={`${m.asientoId}-${m.cuentaId}-${i}`}>
                        <td>{m.fecha}</td>
                        <td>{m.asientoNumero}</td>
                        {consolidado && <td>{`${m.cuentaCodigo} - ${m.cuentaNombre}`}</td>}
                        <td className="numeric">{m.debito > 0 ? formatoMoneda.format(m.debito) : ""}</td>
                        <td className="numeric">{m.credito > 0 ? formatoMoneda.format(m.credito) : ""}</td>
                        <td className="numeric">{formatoMoneda.format(m.saldoAcumulado)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default LibroMayor;
