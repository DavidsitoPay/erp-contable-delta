import { useCallback, useEffect, useState } from "react";
import { revaluacionesApi } from "../../services/api";
import { MONEDAS_EXTRANJERAS, etiquetaFuente, formatoTasa } from "../../utils/cambio";
import { descargarCsv } from "../../utils/csv";
import { formatoMoneda, mensajeError } from "../../utils/formato";
import { LEYENDA_REVALUACION, csvRevaluacion, etiquetaMes, mesAnterior, nombreArchivoRevaluacion } from "../../utils/revaluacion";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";
import CamposAnioMes from "../CamposAnioMes";
import FormularioMotivo from "../FormularioMotivo";
import SelectMoneda from "../SelectMoneda";
import TablaPartidasRevaluacion from "./TablaPartidasRevaluacion";

const NOTA_TASA =
  "La tasa de cierre es la referencia del último día del mes. Si Banguat no responde, capture la tasa manual en Catálogo > Tipos de cambio.";
const CONFIRMACION_EJECUCION = "¿Ejecutar la revaluación del mes? Se registrarán los asientos de revaluación y su reversa.";
const ENCABEZADOS_HISTORIAL = ["Mes", "Moneda", "Fecha de cierre", "Tasa", "Ganancia", "Pérdida", "Estado"];

function formularioInicial() {
  const { anio, mes } = mesAnterior();
  return { anio: String(anio), mes: String(mes), monedaCodigo: "USD" };
}

function cuerpoDe(form) {
  return { anio: Number(form.anio), mes: Number(form.mes), monedaCodigo: form.monedaCodigo };
}

function ResumenRevaluacion({ revaluacion }) {
  const { tipoCambio, totalGanancia, totalPerdida, fechaCierre } = revaluacion;
  return (
    <div>
      <p>{`Fecha de cierre: ${fechaCierre}`}</p>
      <p>{`Tasa de cierre: ${formatoTasa(tipoCambio.tasa)} (${etiquetaFuente(tipoCambio)})`}</p>
      <p>Ganancia no realizada: {formatoMoneda.format(totalGanancia)}</p>
      <p>Pérdida no realizada: {formatoMoneda.format(totalPerdida)}</p>
    </div>
  );
}

function HistorialRevaluaciones({ filas, onDescargar, onAnular }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS_HISTORIAL}>
      {filas.length === 0 ? (
        <tr><td colSpan={ENCABEZADOS_HISTORIAL.length + 1}>No hay revaluaciones registradas.</td></tr>
      ) : (
        filas.map((r) => (
          <tr key={r.id}>
            <td>{etiquetaMes(r.anio, r.mes)}</td>
            <td>{r.monedaCodigo}</td>
            <td>{r.fechaCierre}</td>
            <td className="numeric">{formatoTasa(r.tasa)}</td>
            <td className="numeric">{formatoMoneda.format(r.totalGanancia)}</td>
            <td className="numeric">{formatoMoneda.format(r.totalPerdida)}</td>
            <td>{r.estado}</td>
            <td>
              <button type="button" className="btn btn-outline btn-sm" aria-label={`Descargar CSV de la revaluación ${etiquetaMes(r.anio, r.mes)} ${r.monedaCodigo}`} onClick={() => onDescargar(r)}>
                Descargar CSV
              </button>
              {r.estado === "Vigente" && (
                <button type="button" className="btn btn-danger-outline btn-sm" aria-label={`Anular la revaluación ${etiquetaMes(r.anio, r.mes)} ${r.monedaCodigo}`} onClick={() => onAnular(r)}>
                  Anular
                </button>
              )}
            </td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

function Revaluaciones() {
  const [form, setForm] = useState(formularioInicial);
  const [vista, setVista] = useState(null);
  const [historial, setHistorial] = useState([]);
  const [error, setError] = useState("");
  const [aviso, setAviso] = useState("");
  const [trabajando, setTrabajando] = useState(false);
  const [aAnular, setAAnular] = useState(null);

  const cargarHistorial = useCallback(async () => {
    try {
      const { data } = await revaluacionesApi.listar();
      setHistorial(data);
    } catch (err) {
      setError(mensajeError(err, "No se pudo cargar el historial de revaluaciones."));
    }
  }, []);

  useEffect(() => {
    void cargarHistorial();
  }, [cargarHistorial]);

  function cambiarForm(cambios) {
    setForm({ ...form, ...cambios });
    setVista(null);
  }

  async function trabajar(tarea, mensajeFallo) {
    setError("");
    setAviso("");
    setTrabajando(true);
    try {
      await tarea();
    } catch (err) {
      setError(mensajeError(err, mensajeFallo));
    } finally {
      setTrabajando(false);
    }
  }

  function handleVistaPrevia(e) {
    e.preventDefault();
    return trabajar(async () => {
      const { data } = await revaluacionesApi.vistaPrevia(cuerpoDe(form));
      setVista(data);
    }, "No se pudo calcular la vista previa.");
  }

  function handleEjecutar() {
    if (!confirm(CONFIRMACION_EJECUCION)) return;
    void trabajar(async () => {
      const { data } = await revaluacionesApi.ejecutar(cuerpoDe(form));
      setVista(null);
      setAviso(`Revaluación ${etiquetaMes(data.anio, data.mes)} ${data.monedaCodigo} registrada (asientos ${data.asientoId} y ${data.asientoReversaId}).`);
      await cargarHistorial();
    }, "No se pudo ejecutar la revaluación.");
  }

  function handleDescargar(fila) {
    return trabajar(async () => {
      const { data } = await revaluacionesApi.obtener(fila.id);
      descargarCsv(nombreArchivoRevaluacion(data), csvRevaluacion(data));
    }, "No se pudo descargar la revaluación.");
  }

  async function anular(motivo) {
    await revaluacionesApi.anular(aAnular.id, motivo);
    setAAnular(null);
    setAviso("Revaluación anulada.");
    await cargarHistorial();
  }

  return (
    <div>
      <h2>Revaluación de fin de mes</h2>
      <p className="warning-chip">{LEYENDA_REVALUACION}</p>
      <small className="nota-fiscal">{NOTA_TASA}</small>

      <form className="catalog-form no-print" onSubmit={handleVistaPrevia}>
        <CamposAnioMes anio={form.anio} mes={form.mes} onAnio={(anio) => cambiarForm({ anio })} onMes={(mes) => cambiarForm({ mes })} />
        <SelectMoneda valor={form.monedaCodigo} onChange={(monedaCodigo) => cambiarForm({ monedaCodigo })} opciones={MONEDAS_EXTRANJERAS} />
        <button type="submit" className="btn btn-primary" disabled={trabajando}>Vista previa</button>
        <button type="button" className="btn btn-outline" disabled={vista === null || trabajando} onClick={handleEjecutar}>
          Ejecutar revaluación
        </button>
      </form>

      {error && <p className="error-chip">{error}</p>}
      {aviso && <p className="success-chip">{aviso}</p>}
      {vista && (
        <>
          <ResumenRevaluacion revaluacion={vista} />
          <TablaPartidasRevaluacion partidas={vista.partidas} moneda={form.monedaCodigo} />
        </>
      )}

      {aAnular && (
        <FormularioMotivo
          titulo={`Motivo de la anulación de la revaluación ${etiquetaMes(aAnular.anio, aAnular.mes)} ${aAnular.monedaCodigo}`}
          etiquetaConfirmar="Anular revaluación"
          mensajeFallo="No se pudo anular la revaluación."
          onConfirmar={anular}
          onCancelar={() => setAAnular(null)}
        />
      )}

      <h3>Revaluaciones registradas</h3>
      <HistorialRevaluaciones filas={historial} onDescargar={handleDescargar} onAnular={setAAnular} />
    </div>
  );
}

export default Revaluaciones;
