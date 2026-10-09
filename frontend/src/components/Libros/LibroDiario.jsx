import { useCallback, useEffect, useState } from "react";
import { centrosCostoApi, librosApi, periodosApi } from "../../services/api";
import { perfilActual, puedeReversarAsientos } from "../../utils/perfiles";
import DialogoReversa from "./DialogoReversa";

const formatoMoneda = new Intl.NumberFormat("es-GT", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

function EstadoAsiento({ asiento, puedeReversar, onReversar }) {
  return (
    <>
      {asiento.estado === "Anulado" && (
        <span className="badge badge-warning">
          Anulado — reversado por {asiento.reversadoPorNumero}
        </span>
      )}
      {asiento.reversaDeId && <span className="badge">Reversa de {asiento.reversaDeNumero}</span>}
      {puedeReversar && asiento.reversible && (
        <button type="button" className="btn btn-outline btn-sm" onClick={() => onReversar(asiento)}>
          Reversar
        </button>
      )}
    </>
  );
}

function LibroDiario() {
  const [periodos, setPeriodos] = useState([]);
  const [centros, setCentros] = useState([]);
  const [periodoId, setPeriodoId] = useState("");
  const [asientos, setAsientos] = useState([]);
  const [error, setError] = useState("");
  const [exito, setExito] = useState("");
  const [cargando, setCargando] = useState(false);
  const [aReversar, setAReversar] = useState(null);
  const [puedeReversar] = useState(() => puedeReversarAsientos(perfilActual()));

  useEffect(() => {
    void periodosApi.listar().then(({ data }) => setPeriodos(data));
    // incluirInactivos=true: un asiento ya registrado pudo haber usado un
    // centro de costo que luego se desactivó, y el libro diario es un
    // reporte histórico — debe seguir mostrando su nombre, no "—".
    void centrosCostoApi.listar(true).then(({ data }) => setCentros(data));
  }, []);

  const nombreCentroCosto = (id) => centros.find((c) => c.id === id)?.nombre ?? null;

  const cargarDiario = useCallback((id) => {
    setError("");
    setCargando(true);
    return librosApi
      .diario(Number(id))
      .then(({ data }) => setAsientos(data))
      .catch((err) => setError(err.response?.data?.error || "No se pudo cargar el libro diario."))
      .finally(() => setCargando(false));
  }, []);

  useEffect(() => {
    setExito("");
    if (!periodoId) {
      setAsientos([]);
      return;
    }
    void cargarDiario(periodoId);
  }, [periodoId, cargarDiario]);

  const alReversado = ({ originalNumero, reversaNumero }) => {
    setAReversar(null);
    setExito(`Asiento ${originalNumero} anulado. Reversa ${reversaNumero} registrada.`);
    void cargarDiario(periodoId);
  };

  return (
    <div>
      <h2>Libro diario</h2>

      <div className="catalog-form">
        <select className="select" aria-label="Periodo" value={periodoId} onChange={(e) => setPeriodoId(e.target.value)}>
          <option value="">Selecciona periodo</option>
          {periodos.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nombre}
            </option>
          ))}
        </select>
      </div>

      {error && <p className="error-chip">{error}</p>}
      {exito && <p className="success-chip">{exito}</p>}
      {cargando && <p>Cargando...</p>}

      {!cargando && periodoId && asientos.length === 0 && !error && (
        <p>No hay asientos confirmados en este periodo.</p>
      )}

      {asientos.length > 0 && (
        <div className="table-wrap">
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Asiento</th>
                  <th>Cuenta</th>
                  <th>Centro de costo</th>
                  <th className="numeric">Débito</th>
                  <th className="numeric">Crédito</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {asientos.map((a) =>
                  a.lineas.map((l, i) => (
                    <tr key={`${a.id}-${i}`}>
                      <td>{i === 0 ? a.fecha : ""}</td>
                      <td>{i === 0 ? a.numero : ""}</td>
                      <td>
                        {l.cuentaCodigo} - {l.cuentaNombre}
                      </td>
                      <td>{nombreCentroCosto(l.centroCostoId) ?? "—"}</td>
                      <td className="numeric">{l.debito > 0 ? formatoMoneda.format(l.debito) : ""}</td>
                      <td className="numeric">{l.credito > 0 ? formatoMoneda.format(l.credito) : ""}</td>
                      <td>
                        {i === 0 && (
                          <EstadoAsiento asiento={a} puedeReversar={puedeReversar} onReversar={setAReversar} />
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {aReversar && (
        <DialogoReversa asiento={aReversar} onCancelar={() => setAReversar(null)} onReversado={alReversado} />
      )}
    </div>
  );
}

export default LibroDiario;
