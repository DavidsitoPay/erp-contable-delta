import { REGIMENES_ISR_CONTRAPARTE, REGIMENES_IVA, TIPOS_AGENTE_IVA_CLIENTE } from "../../utils/etiquetasFiscales";
import { requiereFundamento } from "../../utils/formularioContraparte";
import OpcionesSelect from "../OpcionesSelect";

const AYUDA_SIN_REGIMEN = "Sin régimen definido, las facturas se registran sin retención y quedan marcadas para revisión.";

function CamposFiscalesContraparte({ form, onCambio }) {
  const esProveedor = form.tipo === "Proveedor";
  const cambiar = (campo) => (e) => onCambio({ ...form, [campo]: e.target.value });

  return (
    <>
      <select className="select" aria-label="Régimen de IVA" value={form.regimenIva} onChange={cambiar("regimenIva")}>
        <OpcionesSelect opciones={REGIMENES_IVA} />
      </select>
      {esProveedor && (
        <select className="select" aria-label="Régimen de ISR" value={form.regimenIsr} onChange={cambiar("regimenIsr")}>
          <OpcionesSelect opciones={REGIMENES_ISR_CONTRAPARTE} />
        </select>
      )}
      {requiereFundamento(form) && (
        <input className="input" placeholder="Fundamento o número de resolución" aria-label="Fundamento o número de resolución" maxLength={200} value={form.isrFundamento} onChange={cambiar("isrFundamento")} required />
      )}
      <label>
        <input type="checkbox" checked={form.esResidente} onChange={(e) => onCambio({ ...form, esResidente: e.target.checked })} />
        <span>Residente en Guatemala</span>
      </label>
      {!esProveedor && (
        <label>
          <input type="checkbox" checked={form.esAgenteRetencionIva} onChange={(e) => onCambio({ ...form, esAgenteRetencionIva: e.target.checked })} />
          <span>Agente de retención de IVA</span>
        </label>
      )}
      {!esProveedor && form.esAgenteRetencionIva && (
        <select className="select" aria-label="Tipo de agente de IVA" value={form.tipoAgenteIva} onChange={cambiar("tipoAgenteIva")} required>
          <option value="">Selecciona tipo de agente</option>
          <OpcionesSelect opciones={TIPOS_AGENTE_IVA_CLIENTE} />
        </select>
      )}
      {esProveedor && form.regimenIsr === "NO_DEFINIDO" && <small className="nota-fiscal">{AYUDA_SIN_REGIMEN}</small>}
    </>
  );
}

export default CamposFiscalesContraparte;
