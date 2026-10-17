import { useEffect, useState } from "react";
import { ajustesRetencionApi } from "../../services/api";
import { formatoMoneda, mensajeError } from "../../utils/formato";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import CampoFormulario from "../CampoFormulario";

const coincide = (retencionId, documentoId) => (a) =>
  (retencionId === undefined || a.retencionId === retencionId) && (documentoId === undefined || a.notaCreditoId === documentoId || a.documentoOrigenId === documentoId);

function ResolverAjusteDialog({ retencionId, documentoId, onResuelto, onCancelar }) {
  const [ajuste, setAjuste] = useState(null);
  const [form, setForm] = useState({ motivo: "", referencia: "" });
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");
  const cambiar = (campo) => (e) => setForm({ ...form, [campo]: e.target.value });

  useEffect(() => {
    ajustesRetencionApi.listar("PENDIENTE")
      .then(({ data }) => {
        const encontrado = data.find(coincide(retencionId, documentoId));
        if (encontrado) setAjuste(encontrado);
        else setError("El ajuste ya no está pendiente.");
      })
      .catch((err) => setError(mensajeError(err, "No se pudo cargar el ajuste pendiente.")));
  }, [retencionId, documentoId]);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      await ajustesRetencionApi.resolver(ajuste.id, { motivo: form.motivo.trim(), referencia: form.referencia.trim() });
      await onResuelto();
    } catch (err) {
      setError(mensajeError(err, "No se pudo resolver el ajuste."));
    } finally {
      setEnviando(false);
    }
  }

  if (!ajuste) {
    return (
      <>
        {error ? <p className="error-chip">{error}</p> : <p>Cargando ajuste...</p>}
        <button type="button" className="btn btn-outline" onClick={onCancelar}>Cancelar</button>
      </>
    );
  }
  return (
    <>
      <h3>{`Ajuste de retención pendiente: NC ${ajuste.notaCreditoNumero} sobre ${ajuste.documentoOrigenNumero} (${formatoMoneda.format(ajuste.montoNc)})`}</h3>
      <FormularioCatalogo onSubmit={handleSubmit} cargando={enviando || form.motivo.trim() === "" || form.referencia.trim() === ""} error={error} etiquetaBoton="Resolver ajuste">
        <CampoFormulario id="aju-motivo" etiqueta="Motivo">
          <input id="aju-motivo" className="input" maxLength={300} value={form.motivo} onChange={cambiar("motivo")} required />
          <small className="nota-fiscal">Explique cómo se resolvió (p.ej. compensado en el siguiente entero o rectificación ante SAT).</small>
        </CampoFormulario>
        <CampoFormulario id="aju-ref" etiqueta="Referencia">
          <input id="aju-ref" className="input" maxLength={100} value={form.referencia} onChange={cambiar("referencia")} required />
          <small className="nota-fiscal">Número de boleta, rectificación o documento de respaldo.</small>
        </CampoFormulario>
        <button type="button" className="btn btn-outline" onClick={onCancelar} disabled={enviando}>Cancelar</button>
      </FormularioCatalogo>
    </>
  );
}

export default ResolverAjusteDialog;
