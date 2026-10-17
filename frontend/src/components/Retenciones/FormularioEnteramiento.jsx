import { useEffect, useState } from "react";
import { cuentasBancariasApi, enteramientosApi } from "../../services/api";
import { cuentasGtq, enteramientoValido, FORMULARIOS_ENTERAMIENTO, hoyGuatemala, payloadEnteramiento, solicitudEnteramiento } from "../../utils/enteramiento";
import { mensajeError } from "../../utils/formato";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import CampoFormulario from "../CampoFormulario";
import OpcionesSelect from "../OpcionesSelect";
import SelectCuentaBancaria from "../Tesoreria/SelectCuentaBancaria";
import VistaPreviaPartida from "./VistaPreviaPartida";

function FormularioEnteramiento({ saldo, onRegistrado, onCancelar }) {
  const [form, setForm] = useState({ formulario: "", fechaPago: "", boleta: "", monto: String(saldo.saldo), cuentaBancariaId: "" });
  const [cuentas, setCuentas] = useState([]);
  const [vista, setVista] = useState(null);
  const [error, setError] = useState("");
  const [enviando, setEnviando] = useState(false);
  const cambiar = (campo) => (e) => {
    setForm({ ...form, [campo]: e.target.value });
    setVista(null);
  };

  useEffect(() => {
    cuentasBancariasApi.listar()
      .then(({ data }) => setCuentas(cuentasGtq(data)))
      .catch((err) => setError(mensajeError(err, "No se pudieron cargar las cuentas bancarias.")));
  }, []);

  async function previsualizar() {
    setError("");
    try {
      const { data } = await enteramientosApi.vistaPrevia(solicitudEnteramiento(saldo.periodo, form));
      setVista(data);
    } catch (err) {
      setError(mensajeError(err, "No se pudo calcular la vista previa."));
    }
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      await enteramientosApi.crear(payloadEnteramiento(saldo.periodo, form));
      await onRegistrado();
    } catch (err) {
      setError(mensajeError(err, "No se pudo registrar el enteramiento."));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <>
      <h3>{`Enterar ISR del período ${saldo.periodo.slice(0, 7)}`}</h3>
      <FormularioCatalogo onSubmit={handleSubmit} cargando={enviando || !enteramientoValido(form)} error={error} etiquetaBoton="Registrar enteramiento">
        <CampoFormulario id="ent-formulario" etiqueta="Formulario">
          <select id="ent-formulario" className="select" value={form.formulario} onChange={cambiar("formulario")} required>
            <option value="">Selecciona formulario</option>
            <OpcionesSelect opciones={FORMULARIOS_ENTERAMIENTO} />
          </select>
        </CampoFormulario>
        <CampoFormulario id="ent-pago" etiqueta="Fecha de pago">
          <input id="ent-pago" type="date" className="input" min={saldo.periodo} max={hoyGuatemala()} value={form.fechaPago} onChange={cambiar("fechaPago")} required />
        </CampoFormulario>
        <CampoFormulario id="ent-boleta" etiqueta="Número de boleta">
          <input id="ent-boleta" className="input" maxLength={40} value={form.boleta} onChange={cambiar("boleta")} required />
        </CampoFormulario>
        <CampoFormulario id="ent-monto" etiqueta="Monto">
          <input id="ent-monto" type="number" step="0.01" className="input" value={form.monto} onChange={cambiar("monto")} required />
        </CampoFormulario>
        <SelectCuentaBancaria etiqueta="Cuenta bancaria" valor={form.cuentaBancariaId} onChange={(v) => { setForm({ ...form, cuentaBancariaId: v }); setVista(null); }} cuentasBancarias={cuentas} />
        {cuentas.length === 0 && <small className="nota-fiscal">No hay cuentas bancarias activas en quetzales.</small>}
        <button type="button" className="btn btn-outline" onClick={previsualizar} disabled={enviando || !enteramientoValido(form)}>
          Vista previa
        </button>
        <button type="button" className="btn btn-outline" onClick={onCancelar} disabled={enviando}>
          Cancelar
        </button>
      </FormularioCatalogo>
      {vista && <VistaPreviaPartida vista={vista} />}
    </>
  );
}

export default FormularioEnteramiento;
