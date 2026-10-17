import { REGIMENES_ISR_CONTRAPARTE, REGIMENES_IVA, TIPOS_AGENTE_IVA_CLIENTE } from "../../utils/etiquetasFiscales";
import { PAISES_ISO } from "../../utils/paises";
import { requiereFundamento } from "../../utils/formularioContraparte";
import OpcionesSelect from "../OpcionesSelect";

const AYUDA_SIN_REGIMEN = "Sin régimen definido, las facturas se registran sin retención y quedan marcadas para revisión.";

function CamposFiscalesContraparte({ form, onCambio, tiposRenta = [] }) {
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
        <input type="checkbox" checked={form.esResidente} onChange={(e) => onCambio({ ...form, esResidente: e.target.checked, tipoRentaNrCodigo: "", pais: "" })} />
        <span>Residente en Guatemala</span>
      </label>
      {!form.esResidente && (
        <>
          <input className="input" list="paises-iso" placeholder="País (ISO-2)" aria-label="País (ISO-2)" maxLength={2} pattern="[A-Za-z]{2}" value={form.pais ?? ""} onChange={(e) => onCambio({ ...form, pais: e.target.value.toUpperCase() })} required />
          <datalist id="paises-iso">
            {PAISES_ISO.map((p) => <option key={p.codigo} value={p.codigo}>{p.nombre}</option>)}
          </datalist>
        </>
      )}
      {esProveedor && !form.esResidente && (
        <select className="select" aria-label="Tipo de renta (art. 104)" value={form.tipoRentaNrCodigo} onChange={cambiar("tipoRentaNrCodigo")} required={form.regimenIsr !== "EXENTO"}>
          <option value="">Selecciona tipo de renta</option>
          {tiposRenta.map((t) => (
            <option key={t.id} value={t.codigo}>{`${t.nombre} (${t.tasaBase} %)`}</option>
          ))}
        </select>
      )}
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
