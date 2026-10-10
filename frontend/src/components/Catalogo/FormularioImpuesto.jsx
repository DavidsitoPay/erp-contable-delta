import { AMBITOS_IMPUESTO, TIPOS_IMPUESTO } from "../../utils/etiquetasFiscales";
import CampoFormulario from "../CampoFormulario";
import OpcionesSelect from "../OpcionesSelect";
import { FormularioCatalogo } from "./CatalogoCrud";

const TITULOS = { crear: "Agregar impuesto", version: "Nueva versión del impuesto", editar: "Editar impuesto" };
const BOTONES = { crear: "Agregar impuesto", version: "Crear nueva versión", editar: "Guardar cambios" };
const TIPOS_SIN_TASA = new Set(["EXENTO", "NO_AFECTO"]);

function FormularioImpuesto({ form, onCambio, modo, enUso, cargando, error, onSubmit, onCancelar }) {
  const bloqueado = modo === "editar" && enUso;
  const cambiar = (campo) => (e) => onCambio({ ...form, [campo]: e.target.value });

  function cambiarTipo(e) {
    const tipo = e.target.value;
    onCambio({ ...form, tipo, generaCredito: tipo === "IVA_GENERAL", tasa: TIPOS_SIN_TASA.has(tipo) ? "0" : form.tasa });
  }

  return (
    <>
      <h3>{TITULOS[modo]}</h3>
      <FormularioCatalogo onSubmit={onSubmit} cargando={cargando} error={error} etiquetaBoton={BOTONES[modo]}>
        <CampoFormulario id="imp-codigo" etiqueta="Código">
          <input id="imp-codigo" className="input" maxLength={30} value={form.codigo} onChange={cambiar("codigo")} disabled={modo !== "crear"} required />
        </CampoFormulario>
        <CampoFormulario id="imp-nombre" etiqueta="Nombre">
          <input id="imp-nombre" className="input" maxLength={100} value={form.nombre} onChange={cambiar("nombre")} required />
        </CampoFormulario>
        <CampoFormulario id="imp-tipo" etiqueta="Tipo">
          <select id="imp-tipo" className="select" value={form.tipo} onChange={cambiarTipo} disabled={bloqueado}>
            <OpcionesSelect opciones={TIPOS_IMPUESTO} />
          </select>
        </CampoFormulario>
        <CampoFormulario id="imp-tasa" etiqueta="Tasa (%)">
          <input id="imp-tasa" type="number" className="input input-money" min="0" max="100" step="0.0001" value={form.tasa} onChange={cambiar("tasa")} disabled={bloqueado || TIPOS_SIN_TASA.has(form.tipo)} required />
        </CampoFormulario>
        <CampoFormulario id="imp-aplica" etiqueta="Aplica a">
          <select id="imp-aplica" className="select" value={form.aplicaA} onChange={cambiar("aplicaA")} disabled={bloqueado}>
            <OpcionesSelect opciones={AMBITOS_IMPUESTO} />
          </select>
        </CampoFormulario>
        <label>
          <input type="checkbox" checked={form.generaCredito} onChange={(e) => onCambio({ ...form, generaCredito: e.target.checked })} disabled={bloqueado || form.tipo !== "IVA_GENERAL"} />
          <span>Genera crédito fiscal</span>
        </label>
        <CampoFormulario id="imp-articulo" etiqueta="Referencia legal">
          <input id="imp-articulo" className="input" maxLength={200} value={form.articuloLegal} onChange={cambiar("articuloLegal")} required />
        </CampoFormulario>
        <CampoFormulario id="imp-desde" etiqueta="Vigente desde">
          <input id="imp-desde" type="date" className="input" value={form.vigenteDesde} onChange={cambiar("vigenteDesde")} disabled={bloqueado} required />
        </CampoFormulario>
        <CampoFormulario id="imp-hasta" etiqueta="Vigente hasta (opcional)">
          <input id="imp-hasta" type="date" className="input" min={form.vigenteDesde} value={form.vigenteHasta} onChange={cambiar("vigenteHasta")} />
        </CampoFormulario>
        {modo !== "crear" && (
          <button type="button" className="btn btn-outline" onClick={onCancelar}>
            Cancelar
          </button>
        )}
      </FormularioCatalogo>
      {bloqueado && <p className="warning-chip">Ya fue usado en documentos; cree una nueva versión para cambiar la tasa</p>}
    </>
  );
}

export default FormularioImpuesto;
