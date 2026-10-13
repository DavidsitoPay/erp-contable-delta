import { useState } from "react";
import { retencionesApi } from "../../services/api";
import { FORMULARIOS_CONSTANCIA } from "../../utils/etiquetasFiscales";
import { mensajeError } from "../../utils/formato";
import { payloadConstancia } from "../../utils/retenciones";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import CampoFormulario from "../CampoFormulario";
import OpcionesSelect from "../OpcionesSelect";

function FormularioConstancia({ fila, onRegistrada, onCancelar }) {
  const [form, setForm] = useState({ numero: "", fecha: fila.fechaDocumento, formulario: "", fechaPagoFormulario: "", periodoDeclarado: "" });
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");
  const cambiar = (campo) => (e) => setForm({ ...form, [campo]: e.target.value });

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      const { data } = await retencionesApi.registrarConstancia(fila.retencionId, payloadConstancia(form));
      await onRegistrada(data?.advertencias ?? []);
    } catch (err) {
      setError(mensajeError(err, "No se pudo registrar la constancia."));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <>
      <h3>{`Registrar constancia de ${fila.nombre} (${fila.documentoNumero})`}</h3>
      <FormularioCatalogo onSubmit={handleSubmit} cargando={enviando} error={error} etiquetaBoton="Registrar constancia">
        <CampoFormulario id="con-numero" etiqueta="Número de constancia">
          <input id="con-numero" className="input" maxLength={30} value={form.numero} onChange={cambiar("numero")} required />
        </CampoFormulario>
        <CampoFormulario id="con-fecha" etiqueta="Fecha de la constancia">
          <input id="con-fecha" type="date" className="input" value={form.fecha} onChange={cambiar("fecha")} required />
        </CampoFormulario>
        <CampoFormulario id="con-formulario" etiqueta="Formulario">
          <select id="con-formulario" className="select" value={form.formulario} onChange={cambiar("formulario")}>
            <option value="">Sin formulario</option>
            <OpcionesSelect opciones={FORMULARIOS_CONSTANCIA} />
          </select>
        </CampoFormulario>
        <CampoFormulario id="con-pago" etiqueta="Fecha de pago del formulario">
          <input id="con-pago" type="date" className="input" value={form.fechaPagoFormulario} onChange={cambiar("fechaPagoFormulario")} />
        </CampoFormulario>
        <CampoFormulario id="con-periodo" etiqueta="Período declarado">
          <input id="con-periodo" type="month" className="input" value={form.periodoDeclarado} onChange={cambiar("periodoDeclarado")} />
        </CampoFormulario>
        <button type="button" className="btn btn-outline" onClick={onCancelar}>
          Cancelar
        </button>
      </FormularioCatalogo>
    </>
  );
}

export default FormularioConstancia;
