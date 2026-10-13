import { useEffect, useState } from "react";
import { configuracionFiscalApi, cuentasApi } from "../../services/api";
import { cuentasHoja } from "../../utils/cuentas";
import { REGIMENES_ISR, TIPOS_AGENTE_IVA } from "../../utils/etiquetasFiscales";
import { formatoFechaHora, mensajeError } from "../../utils/formato";
import CampoFormulario from "../CampoFormulario";
import OpcionesSelect from "../OpcionesSelect";

const AVISO_CUENTAS = "Falta configurar las cuentas de IVA: no se podrán registrar facturas con IVA hasta que se configuren.";
const AVISO_CUENTAS_RETENCION =
  "Falta configurar las cuentas de retención: no se podrán registrar facturas de compra con retención de ISR ni cobros con IVA retenido por el cliente.";
const AVISO_AGENTE = "La retención de IVA como agente no está disponible en esta versión.";

const CAMPOS_CUENTA = [
  { campo: "cuentaIvaDebitoId", id: "cf-iva-debito", etiqueta: "Cuenta de IVA débito fiscal (Pasivo)", tipo: "Pasivo" },
  { campo: "cuentaIvaCreditoId", id: "cf-iva-credito", etiqueta: "Cuenta de IVA crédito fiscal (Activo)", tipo: "Activo" },
  { campo: "cuentaIsrRetenidoPagarId", id: "cf-isr-retenido", etiqueta: "Cuenta de ISR retenido por pagar (Pasivo)", tipo: "Pasivo" },
  { campo: "cuentaIvaRetenidoClientesId", id: "cf-iva-retenido", etiqueta: "Cuenta de IVA retenido por clientes (Activo)", tipo: "Activo" },
];

const textoId = (id) => (id ? String(id) : "");
const idONulo = (valor) => (valor ? Number(valor) : null);

function aFormulario(config) {
  return {
    nitEmpresa: config.nitEmpresa ?? "",
    nombreLegal: config.nombreLegal ?? "",
    regimenIsr: config.regimenIsr,
    agenteRetencionIva: config.agenteRetencionIva,
    tipoAgenteIva: config.tipoAgenteIva ?? "",
    ...Object.fromEntries(CAMPOS_CUENTA.map(({ campo }) => [campo, textoId(config[campo])])),
  };
}

function aPayload(form) {
  return {
    nitEmpresa: form.nitEmpresa.trim() || null,
    nombreLegal: form.nombreLegal.trim() || null,
    regimenIsr: form.regimenIsr,
    agenteRetencionIva: form.agenteRetencionIva,
    tipoAgenteIva: form.agenteRetencionIva ? form.tipoAgenteIva : null,
    ...Object.fromEntries(CAMPOS_CUENTA.map(({ campo }) => [campo, idONulo(form[campo])])),
  };
}

function SelectCuentaFiscal({ id, etiqueta, valor, onChange, cuentas }) {
  return (
    <CampoFormulario id={id} etiqueta={etiqueta}>
      <select id={id} className="select" value={valor} onChange={onChange}>
        <option value="">(sin configurar)</option>
        {cuentas.map((c) => (
          <option key={c.id} value={c.id}>
            {c.codigo} - {c.nombre}
          </option>
        ))}
      </select>
    </CampoFormulario>
  );
}

function ConfiguracionFiscal() {
  const [config, setConfig] = useState(null);
  const [form, setForm] = useState(null);
  const [cuentas, setCuentas] = useState([]);
  const [error, setError] = useState("");
  const [guardado, setGuardado] = useState(false);
  const [guardando, setGuardando] = useState(false);

  useEffect(() => {
    void cuentasApi.listar().then(({ data }) => setCuentas(data));
    configuracionFiscalApi
      .obtener()
      .then(({ data }) => {
        setConfig(data);
        setForm(aFormulario(data));
      })
      .catch((err) => setError(mensajeError(err, "No se pudo cargar la configuración fiscal.")));
  }, []);

  if (!form) {
    return (
      <div>
        <h2>Configuración fiscal</h2>
        {error ? <p className="error-chip">{error}</p> : <p>Cargando...</p>}
      </div>
    );
  }

  const cambiar = (campo) => (e) => {
    setGuardado(false);
    setForm({ ...form, [campo]: e.target.value });
  };

  const hojas = cuentasHoja(cuentas);
  const cuentasPorTipo = {
    Pasivo: hojas.filter((c) => c.tipo === "Pasivo" && c.naturaleza === "Acreedora"),
    Activo: hojas.filter((c) => c.tipo === "Activo" && c.naturaleza === "Deudora"),
  };
  const faltanCuentas = !config.cuentaIvaDebitoId || !config.cuentaIvaCreditoId;
  const faltanCuentasRetencion = !config.cuentaIsrRetenidoPagarId || !config.cuentaIvaRetenidoClientesId;
  const puedeGuardar = !guardando && (!form.agenteRetencionIva || form.tipoAgenteIva !== "");
  const autor = config.actualizadoPorNombre ? ` por ${config.actualizadoPorNombre}` : "";

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setGuardado(false);
    setGuardando(true);
    try {
      const { data } = await configuracionFiscalApi.actualizar(aPayload(form));
      setConfig(data);
      setForm(aFormulario(data));
      setGuardado(true);
    } catch (err) {
      setError(mensajeError(err, "No se pudo guardar la configuración fiscal."));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <div>
      <h2>Configuración fiscal</h2>

      <form onSubmit={handleSubmit}>
        <div className="catalog-form">
          <CampoFormulario id="cf-nit" etiqueta="NIT de la empresa">
            <input id="cf-nit" className="input" maxLength={30} value={form.nitEmpresa} onChange={cambiar("nitEmpresa")} />
          </CampoFormulario>
          <CampoFormulario id="cf-nombre" etiqueta="Nombre legal">
            <input id="cf-nombre" className="input" maxLength={200} value={form.nombreLegal} onChange={cambiar("nombreLegal")} />
          </CampoFormulario>
          <CampoFormulario id="cf-moneda" etiqueta="Moneda funcional">
            <input id="cf-moneda" className="input" value={config.monedaFuncionalCodigo ?? ""} disabled />
          </CampoFormulario>
          <CampoFormulario id="cf-isr" etiqueta="Régimen de ISR">
            <select id="cf-isr" className="select" value={form.regimenIsr} onChange={cambiar("regimenIsr")}>
              <OpcionesSelect opciones={REGIMENES_ISR} />
            </select>
          </CampoFormulario>
        </div>

        <div className="catalog-form">
          <label>
            <input
              type="checkbox"
              checked={form.agenteRetencionIva}
              onChange={(e) => {
                setGuardado(false);
                setForm({ ...form, agenteRetencionIva: e.target.checked });
              }}
            />
            <span>La empresa es agente de retención de IVA</span>
          </label>
          {form.agenteRetencionIva && (
            <CampoFormulario id="cf-tipo-agente" etiqueta="Tipo de agente">
              <select id="cf-tipo-agente" className="select" value={form.tipoAgenteIva} onChange={cambiar("tipoAgenteIva")}>
                <option value="">Selecciona tipo</option>
                <OpcionesSelect opciones={TIPOS_AGENTE_IVA} />
              </select>
            </CampoFormulario>
          )}
        </div>

        <div className="catalog-form">
          {CAMPOS_CUENTA.map(({ campo, id, etiqueta, tipo }) => (
            <SelectCuentaFiscal key={id} id={id} etiqueta={etiqueta} valor={form[campo]} onChange={cambiar(campo)} cuentas={cuentasPorTipo[tipo]} />
          ))}
        </div>

        {faltanCuentas && <p className="warning-chip">{AVISO_CUENTAS}</p>}
        {faltanCuentasRetencion && <p className="warning-chip">{AVISO_CUENTAS_RETENCION}</p>}
        {form.agenteRetencionIva && <p className="warning-chip">{AVISO_AGENTE}</p>}
        {error && <p className="error-chip">{error}</p>}
        {guardado && <p className="success-chip">Configuración guardada.</p>}

        <button type="submit" disabled={!puedeGuardar} className="btn btn-primary">
          Guardar configuración
        </button>
      </form>

      {config.actualizadoEn && <p>{`Última actualización: ${formatoFechaHora(config.actualizadoEn)}${autor}`}</p>}
    </div>
  );
}

export default ConfiguracionFiscal;
