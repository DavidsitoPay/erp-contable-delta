import { useEffect, useState } from "react";
import { periodosApi } from "../../services/api";
import { mensajeError } from "../../utils/formato";

function masReciente(periodos) {
  return periodos.reduce((ultimo, p) => (ultimo === null || p.fechaFin > ultimo.fechaFin ? p : ultimo), null);
}

function useReporte(obtener, mensajeFallo) {
  const [periodos, setPeriodos] = useState([]);
  const [periodoId, setPeriodoId] = useState("");
  const [reporte, setReporte] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    periodosApi
      .listar()
      .then(({ data }) => {
        setPeriodos(data);
        const reciente = masReciente(data);
        if (reciente) {
          setPeriodoId(String(reciente.id));
        } else {
          setCargando(false);
        }
      })
      .catch((err) => {
        setError(mensajeError(err, "No se pudieron cargar los periodos."));
        setCargando(false);
      });
  }, []);

  useEffect(() => {
    if (!periodoId) return undefined;
    let vigente = true;
    setError("");
    setCargando(true);
    obtener(Number(periodoId))
      .then(({ data }) => {
        if (vigente) setReporte(data);
      })
      .catch((err) => {
        if (!vigente) return;
        setReporte(null);
        setError(mensajeError(err, mensajeFallo));
      })
      .finally(() => {
        if (vigente) setCargando(false);
      });
    return () => {
      vigente = false;
    };
  }, [periodoId, obtener, mensajeFallo]);

  return { periodos, periodoId, setPeriodoId, reporte, error, cargando };
}

function FuenteReporte({ reporte }) {
  const cerrado = reporte.fuente === "Cierre";
  return (
    <div className="reporte-fuente">
      <span className={cerrado ? "badge badge-ok" : "badge badge-warning"}>
        {cerrado ? `Periodo cerrado — cierre ${reporte.periodo.cierres}` : "Cifras preliminares (periodo abierto)"}
      </span>
      {reporte.incluyePeriodosAbiertos && (
        <p className="warning-chip">Incluye periodos abiertos anteriores: las cifras pueden cambiar al cerrarlos.</p>
      )}
    </div>
  );
}

function PaginaReporte({ titulo, obtener, mensajeFallo, children }) {
  const { periodos, periodoId, setPeriodoId, reporte, error, cargando } = useReporte(obtener, mensajeFallo);
  const [mostrarCeros, setMostrarCeros] = useState(false);

  return (
    <div>
      <h2>{titulo}</h2>

      <div className="catalog-form no-print">
        <select className="select" aria-label="Periodo" value={periodoId} onChange={(e) => setPeriodoId(e.target.value)}>
          {periodos.length === 0 && <option value="">Sin periodos</option>}
          {periodos.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nombre}
            </option>
          ))}
        </select>
        <label>
          <input type="checkbox" checked={mostrarCeros} onChange={(e) => setMostrarCeros(e.target.checked)} />
          <span>Mostrar cuentas en cero</span>
        </label>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => window.print()}>
          Imprimir
        </button>
      </div>

      {error && <p className="error-chip">{error}</p>}
      {cargando && <p>Cargando...</p>}
      {reporte && !cargando && (
        <>
          <FuenteReporte reporte={reporte} />
          {children(reporte, mostrarCeros)}
        </>
      )}
    </div>
  );
}

export default PaginaReporte;
