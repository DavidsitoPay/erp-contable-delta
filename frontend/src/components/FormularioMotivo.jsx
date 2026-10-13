import { useState } from "react";
import { mensajeError } from "../utils/formato";
import { FormularioCatalogo } from "./Catalogo/CatalogoCrud";
import CampoFormulario from "./CampoFormulario";

function FormularioMotivo({ titulo, etiquetaConfirmar, mensajeFallo, onConfirmar, onCancelar }) {
  const [motivo, setMotivo] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      await onConfirmar(motivo.trim());
    } catch (err) {
      setError(mensajeError(err, mensajeFallo));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <FormularioCatalogo onSubmit={handleSubmit} cargando={enviando || motivo.trim() === ""} error={error} etiquetaBoton={etiquetaConfirmar}>
      <CampoFormulario id="motivo-accion" etiqueta={titulo}>
        <input id="motivo-accion" className="input" maxLength={300} value={motivo} onChange={(e) => setMotivo(e.target.value)} required />
      </CampoFormulario>
      <button type="button" className="btn btn-outline" onClick={onCancelar} disabled={enviando}>
        Cancelar
      </button>
    </FormularioCatalogo>
  );
}

export default FormularioMotivo;
