import { useState } from "react";
import { tiposCambioApi } from "../../services/api";
import { MONEDAS_EXTRANJERAS, tasaValida } from "../../utils/cambio";
import { hoyIso, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import SelectMoneda from "../SelectMoneda";

function FormularioTasaManual({ monedaInicial = "USD", fechaInicial = "", tasaInicial = "", fijo = false, enLinea = false, onCapturada, onCancelar }) {
  const [form, setForm] = useState(() => ({
    monedaCodigo: monedaInicial,
    fecha: fechaInicial || hoyIso(),
    tasa: String(tasaInicial),
    motivo: "",
  }));
  const [error, setError] = useState("");
  const [enviando, setEnviando] = useState(false);

  const cambiar = (campo) => (e) => setForm({ ...form, [campo]: e.target.value });
  const valido = tasaValida(monto(form.tasa)) && form.motivo.trim() !== "" && form.fecha !== "";

  async function guardar(e) {
    e?.preventDefault();
    setError("");
    setEnviando(true);
    try {
      await tiposCambioApi.capturarManual({
        monedaCodigo: form.monedaCodigo,
        fecha: form.fecha,
        tasa: monto(form.tasa),
        motivo: form.motivo.trim(),
      });
      await onCapturada();
    } catch (err) {
      setError(mensajeError(err, "No se pudo capturar el tipo de cambio."));
    } finally {
      setEnviando(false);
    }
  }

  const campos = (
    <>
      <SelectMoneda etiqueta="Moneda de la tasa" valor={form.monedaCodigo} onChange={(codigo) => setForm({ ...form, monedaCodigo: codigo })} opciones={MONEDAS_EXTRANJERAS} deshabilitado={fijo} />
      <input type="date" className="input" aria-label="Fecha de la tasa" value={form.fecha} onChange={cambiar("fecha")} disabled={fijo} required />
      <input type="number" className="input input-money" aria-label="Tasa de cambio (GTQ por unidad)" min="0" step="0.000001" placeholder="0.000000" value={form.tasa} onChange={cambiar("tasa")} required />
      <input className="input" aria-label="Motivo de la tasa manual" placeholder="Motivo (obligatorio)" maxLength={300} value={form.motivo} onChange={cambiar("motivo")} required />
      <button type="button" className="btn btn-outline" onClick={onCancelar} disabled={enviando}>
        Cancelar
      </button>
    </>
  );

  if (enLinea) {
    return (
      <div className="catalog-form">
        {campos}
        <button type="button" className="btn btn-primary" disabled={enviando || !valido} onClick={guardar}>
          Guardar tasa manual
        </button>
        {error && <p className="error-chip">{error}</p>}
      </div>
    );
  }

  return (
    <FormularioCatalogo onSubmit={guardar} cargando={enviando || !valido} error={error} etiquetaBoton="Guardar tasa manual">
      {campos}
    </FormularioCatalogo>
  );
}

export default FormularioTasaManual;
