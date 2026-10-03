import { useEffect, useState } from "react";
import { centrosCostoApi, librosApi, periodosApi } from "../../services/api";

const formatoMoneda = new Intl.NumberFormat("es-GT", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

function LibroDiario() {
  const [periodos, setPeriodos] = useState([]);
  const [centros, setCentros] = useState([]);
  const [periodoId, setPeriodoId] = useState("");
  const [asientos, setAsientos] = useState([]);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  useEffect(() => {
    periodosApi.listar().then(({ data }) => setPeriodos(data));
    // incluirInactivos=true: un asiento ya registrado pudo haber usado un
    // centro de costo que luego se desactivó, y el libro diario es un
    // reporte histórico — debe seguir mostrando su nombre, no "—".
    centrosCostoApi.listar(true).then(({ data }) => setCentros(data));
  }, []);

  const nombreCentroCosto = (id) => centros.find((c) => c.id === id)?.nombre ?? null;

  useEffect(() => {
    if (!periodoId) {
      setAsientos([]);
      return;
    }
    setError("");
    setCargando(true);
    librosApi
      .diario(Number(periodoId))
      .then(({ data }) => setAsientos(data))
      .catch((err) => setError(err.response?.data?.error || "No se pudo cargar el libro diario."))
      .finally(() => setCargando(false));
  }, [periodoId]);

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
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

export default LibroDiario;
