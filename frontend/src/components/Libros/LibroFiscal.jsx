import { useState } from "react";
import { librosFiscalesApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import { AVISO_DTE } from "../../utils/fiscal";
import { formatoMoneda, mensajeError } from "../../utils/formato";
import { COLUMNAS_TABLA, csvLibro, nombreArchivoCsv } from "../../utils/libroFiscal";

const MESES = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
const COLUMNAS_ETIQUETA = COLUMNAS_TABLA.filter((c) => !c.numerica).length;

function etiquetaTipo(fila) {
  return fila.estado && fila.estado !== "Vigente" ? `${fila.tipoDocumento} (${fila.estado})` : fila.tipoDocumento;
}

function celdaDe(columna, fila) {
  if (columna.numerica) return <td key={columna.clave} className="numeric">{formatoMoneda.format(fila[columna.clave])}</td>;
  if (columna.clave === "tipoDocumento") return <td key={columna.clave}>{etiquetaTipo(fila)}</td>;
  if (columna.clave === "numero") {
    return (
      <td key={columna.clave}>
        {fila.numero}
        {fila.legado && <small className="nota-fiscal">(legado)</small>}
      </td>
    );
  }
  return <td key={columna.clave}>{fila[columna.clave] ?? "—"}</td>;
}

function TablaLibroFiscal({ reporte }) {
  const numericas = COLUMNAS_TABLA.filter((c) => c.numerica);
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              {COLUMNAS_TABLA.map((c) => (
                <th key={c.clave} className={c.numerica ? "numeric" : undefined}>{c.titulo}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {reporte.filas.length === 0 ? (
              <tr><td colSpan={COLUMNAS_TABLA.length}>No hay documentos en el mes seleccionado.</td></tr>
            ) : (
              reporte.filas.map((fila) => (
                <tr key={`${fila.tipoDocumento}-${fila.numeroInterno}`}>{COLUMNAS_TABLA.map((c) => celdaDe(c, fila))}</tr>
              ))
            )}
          </tbody>
          <tfoot>
            <tr className="row-total">
              <td colSpan={COLUMNAS_ETIQUETA}>Totales</td>
              {numericas.map((c) => (
                <td key={c.clave} className="numeric">{formatoMoneda.format(reporte.totales[c.clave])}</td>
              ))}
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}

function LibroFiscal() {
  const hoy = new Date();
  const [tipo, setTipo] = useState("ventas");
  const [anio, setAnio] = useState(String(hoy.getFullYear()));
  const [mes, setMes] = useState(String(hoy.getMonth() + 1));
  const [consulta, setConsulta] = useState(null);
  const [reporte, setReporte] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  function cambiarTipo(nuevoTipo) {
    setTipo(nuevoTipo);
    setReporte(null);
    setError("");
  }

  async function handleConsultar(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      const { data } = await librosFiscalesApi[tipo](Number(anio), Number(mes));
      setReporte(data);
      setConsulta({ tipo, anio, mes });
    } catch (err) {
      setReporte(null);
      setError(mensajeError(err, "No se pudo consultar el libro."));
    } finally {
      setCargando(false);
    }
  }

  function handleExportar() {
    descargarCsv(nombreArchivoCsv(consulta.tipo, consulta.anio, consulta.mes), csvLibro(reporte));
  }

  return (
    <div>
      <h2>Libro de compras y ventas</h2>
      <p className="warning-chip">{AVISO_DTE}</p>

      <div className="lineas-toolbar">
        <button type="button" className={`btn btn-sm ${tipo === "ventas" ? "btn-primary" : "btn-outline"}`} onClick={() => cambiarTipo("ventas")}>
          Ventas
        </button>
        <button type="button" className={`btn btn-sm ${tipo === "compras" ? "btn-primary" : "btn-outline"}`} onClick={() => cambiarTipo("compras")}>
          Compras
        </button>
      </div>

      <form className="catalog-form no-print" onSubmit={handleConsultar}>
        <input type="number" className="input" aria-label="Año" min="2000" max="2100" value={anio} onChange={(e) => setAnio(e.target.value)} required />
        <select className="select" aria-label="Mes" value={mes} onChange={(e) => setMes(e.target.value)}>
          {MESES.map((nombre, indice) => (
            <option key={nombre} value={indice + 1}>{nombre}</option>
          ))}
        </select>
        <button type="submit" className="btn btn-primary" disabled={cargando}>Consultar</button>
        {reporte && (
          <button type="button" className="btn btn-outline" onClick={handleExportar}>Exportar CSV</button>
        )}
      </form>

      {error && <p className="error-chip">{error}</p>}
      {cargando && <p>Cargando...</p>}
      {reporte && !cargando && (
        <>
          <p>{`Contribuyente: ${reporte.contribuyente?.nombre ?? "—"} (NIT ${reporte.contribuyente?.nit ?? "—"})`}</p>
          {reporte.advertencias.map((texto) => (
            <p key={texto} className="warning-chip">{texto}</p>
          ))}
          <TablaLibroFiscal reporte={reporte} />
        </>
      )}
    </div>
  );
}

export default LibroFiscal;
