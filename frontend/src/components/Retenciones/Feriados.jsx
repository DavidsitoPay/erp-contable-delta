import { useCallback, useEffect, useState } from "react";
import { feriadosApi } from "../../services/api";
import { mensajeError } from "../../utils/formato";
import { perfilActual, puedeAdministrarConfiguracionFiscal } from "../../utils/perfiles";
import { FormularioCatalogo, TablaCatalogo } from "../Catalogo/CatalogoCrud";
import CampoFormulario from "../CampoFormulario";

const FORMULARIO_VACIO = { fecha: "", nombre: "", medioDia: false };

function Feriados() {
  const [anio, setAnio] = useState(() => String(new Date().getFullYear()));
  const [feriados, setFeriados] = useState([]);
  const [form, setForm] = useState(FORMULARIO_VACIO);
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");
  const puedeEditar = puedeAdministrarConfiguracionFiscal(perfilActual());

  const cargar = useCallback(async () => {
    try {
      const { data } = await feriadosApi.listar(anio);
      setFeriados(data);
      setError("");
    } catch (err) {
      setError(mensajeError(err, "No se pudo cargar los feriados."));
    }
  }, [anio]);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  async function agregar(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      await feriadosApi.crear({ fecha: form.fecha, nombre: form.nombre.trim(), medioDia: form.medioDia });
      setForm(FORMULARIO_VACIO);
      await cargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo agregar el feriado."));
    } finally {
      setEnviando(false);
    }
  }

  async function eliminar(fecha) {
    setError("");
    try {
      await feriadosApi.eliminar(fecha);
      await cargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo eliminar el feriado."));
    }
  }

  return (
    <>
      <h2>Feriados</h2>
      <CampoFormulario id="fer-anio" etiqueta="Año">
        <input id="fer-anio" type="number" className="input" value={anio} onChange={(e) => setAnio(e.target.value)} />
      </CampoFormulario>
      {error && <p className="error-chip">{error}</p>}
      <TablaCatalogo encabezados={["Fecha", "Nombre", "Tipo", ""]}>
        {feriados.length === 0 && (
          <tr>
            <td colSpan={4}>No hay feriados cargados para este año.</td>
          </tr>
        )}
        {feriados.map((f) => (
          <tr key={f.fecha}>
            <td>{f.fecha}</td>
            <td>{f.nombre}</td>
            <td>{f.medioDia ? "Medio día (hábil)" : "Inhábil"}</td>
            <td>
              {puedeEditar && (
                <button type="button" className="btn btn-outline" aria-label={`Eliminar feriado ${f.fecha}`} onClick={() => eliminar(f.fecha)}>
                  Eliminar
                </button>
              )}
            </td>
          </tr>
        ))}
      </TablaCatalogo>

      {puedeEditar && (
        <>
          <h3>Agregar feriado</h3>
          <FormularioCatalogo onSubmit={agregar} cargando={enviando || form.fecha === "" || form.nombre.trim() === ""} etiquetaBoton="Agregar feriado">
            <CampoFormulario id="fer-fecha" etiqueta="Fecha">
              <input id="fer-fecha" type="date" className="input" value={form.fecha} onChange={(e) => setForm({ ...form, fecha: e.target.value })} required />
            </CampoFormulario>
            <CampoFormulario id="fer-nombre" etiqueta="Nombre">
              <input id="fer-nombre" className="input" maxLength={100} value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
            </CampoFormulario>
            <label>
              <input type="checkbox" checked={form.medioDia} onChange={(e) => setForm({ ...form, medioDia: e.target.checked })} /> Medio día (hábil)
            </label>
          </FormularioCatalogo>
        </>
      )}
    </>
  );
}

export default Feriados;
