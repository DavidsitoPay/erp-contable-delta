import { useState } from "react";
import { retencionesApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import { CONCEPTOS_RETENCION, ESTADOS_CONSTANCIA, SENTIDOS_RETENCION } from "../../utils/etiquetasFiscales";
import { mensajeError } from "../../utils/formato";
import { AVISO_CONSTANCIA, csvRetenciones, filtrosReporte, nombreArchivoCsv } from "../../utils/retenciones";
import CamposAnioMes from "../CamposAnioMes";
import FormularioMotivo from "../FormularioMotivo";
import OpcionesSelect from "../OpcionesSelect";
import FormularioConstancia from "./FormularioConstancia";
import ResolverAjusteDialog from "./ResolverAjusteDialog";
import ResumenRetenciones from "./ResumenRetenciones";
import TablaRetenciones from "./TablaRetenciones";

function filtrosIniciales() {
  const hoy = new Date();
  return { anio: String(hoy.getFullYear()), mes: String(hoy.getMonth() + 1), sentido: "", concepto: "", nit: "", estadoConstancia: "" };
}

function SelectFiltro({ etiqueta, todos, opciones, valor, onChange }) {
  return (
    <select className="select" aria-label={etiqueta} value={valor} onChange={onChange}>
      <option value="">{todos}</option>
      <OpcionesSelect opciones={opciones} />
    </select>
  );
}

function ReporteRetenciones() {
  const [filtros, setFiltros] = useState(filtrosIniciales);
  const [consulta, setConsulta] = useState(null);
  const [reporte, setReporte] = useState(null);
  const [accion, setAccion] = useState(null);
  const [avisos, setAvisos] = useState([]);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const cambiar = (campo) => (e) => setFiltros({ ...filtros, [campo]: e.target.value });
  const fijar = (campo) => (valor) => setFiltros({ ...filtros, [campo]: valor });

  async function consultar(criterios) {
    setError("");
    setCargando(true);
    try {
      const { data } = await retencionesApi.reporte(filtrosReporte(criterios));
      setReporte(data);
      setConsulta(criterios);
    } catch (err) {
      setReporte(null);
      setError(mensajeError(err, "No se pudo consultar el reporte de retenciones."));
    } finally {
      setCargando(false);
    }
  }

  function handleConsultar(e) {
    e.preventDefault();
    setAccion(null);
    setAvisos([]);
    void consultar(filtros);
  }

  async function terminarAccion(advertencias = []) {
    setAccion(null);
    setAvisos(advertencias);
    await consultar(consulta);
  }

  async function anularConstancia(motivo) {
    await retencionesApi.anularConstancia(accion.fila.constanciaId, motivo);
    await terminarAccion();
  }

  function handleExportar() {
    descargarCsv(nombreArchivoCsv(consulta.anio, consulta.mes), csvRetenciones(reporte));
  }

  return (
    <div>
      <h2>Reporte de retenciones</h2>
      <p className="warning-chip">{AVISO_CONSTANCIA}</p>

      <form className="catalog-form no-print" onSubmit={handleConsultar}>
        <CamposAnioMes anio={filtros.anio} mes={filtros.mes} onAnio={fijar("anio")} onMes={fijar("mes")} />
        <SelectFiltro etiqueta="Sentido" todos="Todos los sentidos" opciones={SENTIDOS_RETENCION} valor={filtros.sentido} onChange={cambiar("sentido")} />
        <SelectFiltro etiqueta="Impuesto" todos="Todos los impuestos" opciones={CONCEPTOS_RETENCION} valor={filtros.concepto} onChange={cambiar("concepto")} />
        <input className="input" placeholder="NIT" aria-label="NIT" value={filtros.nit} onChange={cambiar("nit")} />
        <SelectFiltro etiqueta="Estado de la constancia" todos="Todos los estados" opciones={ESTADOS_CONSTANCIA} valor={filtros.estadoConstancia} onChange={cambiar("estadoConstancia")} />
        <button type="submit" className="btn btn-primary" disabled={cargando}>Consultar</button>
        {reporte && <button type="button" className="btn btn-outline" onClick={handleExportar}>Exportar CSV</button>}
      </form>

      {avisos.map((texto) => (
        <p key={texto} className="warning-chip">{texto}</p>
      ))}
      {accion?.tipo === "registrar" && (
        <FormularioConstancia fila={accion.fila} onRegistrada={terminarAccion} onCancelar={() => setAccion(null)} />
      )}
      {accion?.tipo === "anular" && (
        <FormularioMotivo
          titulo={`Motivo de anulación de la constancia ${accion.fila.constanciaNumero}`}
          etiquetaConfirmar="Anular constancia"
          mensajeFallo="No se pudo anular la constancia."
          onConfirmar={anularConstancia}
          onCancelar={() => setAccion(null)}
        />
      )}
      {accion?.tipo === "ajuste" && (
        <ResolverAjusteDialog retencionId={accion.fila.retencionId} onResuelto={terminarAccion} onCancelar={() => setAccion(null)} />
      )}

      {error && <p className="error-chip">{error}</p>}
      {cargando && <p>Cargando...</p>}
      {reporte && !cargando && (
        <>
          <TablaRetenciones filas={reporte.filas} onAccion={setAccion} />
          <ResumenRetenciones reporte={reporte} />
        </>
      )}
    </div>
  );
}

export default ReporteRetenciones;
