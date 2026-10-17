import { BASES_UMBRAL, CONCEPTOS_PARAMETRO } from "../../utils/etiquetasFiscales";
import { admiteTramo, esNoResidente, esPlazo } from "../../utils/parametrosRetencion";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import CampoFormulario from "../CampoFormulario";
import CamposVigencia from "../CamposVigencia";
import OpcionesSelect from "../OpcionesSelect";

function CampoNumerico({ id, etiqueta, valor, onChange, requerido = true, ...limites }) {
  return (
    <CampoFormulario id={id} etiqueta={etiqueta}>
      <input id={id} type="number" className="input input-money" value={valor} onChange={onChange} required={requerido} {...limites} />
    </CampoFormulario>
  );
}

function CamposTasa({ form, cambiar }) {
  return (
    <>
      <CampoNumerico id="par-tasa" etiqueta="Tasa base (%)" valor={form.tasaBase} onChange={cambiar("tasaBase")} min="0" max="100" step="0.0001" />
      {admiteTramo(form) && (
        <>
          <CampoNumerico id="par-tramo" etiqueta="Tramo límite (opcional)" valor={form.tramoLimite} onChange={cambiar("tramoLimite")} requerido={false} min="0" step="0.01" />
          <CampoNumerico id="par-excedente" etiqueta="Tasa sobre el excedente (%)" valor={form.tasaExcedente} onChange={cambiar("tasaExcedente")} requerido={form.tramoLimite !== ""} min="0" max="100" step="0.0001" />
        </>
      )}
      {!esNoResidente(form) && (
        <>
          <CampoNumerico id="par-umbral" etiqueta="Umbral mínimo" valor={form.umbralMinimo} onChange={cambiar("umbralMinimo")} min="0" step="0.01" />
          <CampoFormulario id="par-sobre" etiqueta="Umbral sobre">
            <select id="par-sobre" className="select" value={form.umbralSobre} onChange={cambiar("umbralSobre")}>
              <OpcionesSelect opciones={BASES_UMBRAL} />
            </select>
          </CampoFormulario>
        </>
      )}
    </>
  );
}

function FormularioParametroRetencion({ form, onCambio, cargando, onSubmit, onLimpiar }) {
  const cambiar = (campo) => (e) => onCambio({ ...form, [campo]: e.target.value });

  return (
    <>
      <h3>Nueva versión de parámetro</h3>
      <FormularioCatalogo onSubmit={onSubmit} cargando={cargando} etiquetaBoton="Guardar versión">
        <CampoFormulario id="par-concepto" etiqueta="Concepto">
          <select id="par-concepto" className="select" value={form.concepto} onChange={cambiar("concepto")}>
            <OpcionesSelect opciones={CONCEPTOS_PARAMETRO} />
          </select>
        </CampoFormulario>
        <CampoFormulario id="par-codigo" etiqueta="Código">
          <input id="par-codigo" className="input" maxLength={40} value={form.codigo} onChange={cambiar("codigo")} required />
        </CampoFormulario>
        <CampoFormulario id="par-nombre" etiqueta="Nombre">
          <input id="par-nombre" className="input" maxLength={120} value={form.nombre} onChange={cambiar("nombre")} required />
        </CampoFormulario>
        {esPlazo(form) ? (
          <CampoNumerico id="par-dia" etiqueta="Día límite del mes siguiente" valor={form.diaLimite} onChange={cambiar("diaLimite")} min="1" max="28" step="1" />
        ) : (
          <CamposTasa form={form} cambiar={cambiar} />
        )}
        <CampoFormulario id="par-articulo" etiqueta="Referencia legal">
          <input id="par-articulo" className="input" maxLength={200} value={form.articuloLegal} onChange={cambiar("articuloLegal")} required />
        </CampoFormulario>
        <CampoFormulario id="par-nota" etiqueta="Nota">
          <input id="par-nota" className="input" maxLength={300} value={form.nota} onChange={cambiar("nota")} />
        </CampoFormulario>
        <CamposVigencia prefijo="par" form={form} cambiar={cambiar} />
        <button type="button" className="btn btn-outline" onClick={onLimpiar}>
          Limpiar
        </button>
      </FormularioCatalogo>
    </>
  );
}

export default FormularioParametroRetencion;
