import { useEffect, useState } from "react";
import { contrapartesApi } from "../../services/api";
import { REGIMENES_ISR, REGIMENES_IVA, etiquetaDe } from "../../utils/etiquetasFiscales";
import { mensajeError } from "../../utils/formato";
import { mensajeNit } from "../../utils/nit";
import OpcionesSelect from "../OpcionesSelect";

const ENCABEZADOS = ["Nombre", "NIT", "Dirección", "Régimen IVA", "Residente", "Agente de retención IVA"];

function vacio(tipo) {
  return {
    tipo,
    nombre: "",
    nit: "",
    direccion: "",
    regimenIva: "GENERAL",
    regimenIsr: "UTILIDADES",
    esResidente: true,
    esAgenteRetencionIva: false,
  };
}

const siNo = (valor) => (valor ? "Sí" : "No");

function CamposFiscales({ form, onCambio, esProveedor }) {
  return (
    <>
      <select className="select" aria-label="Régimen de IVA" value={form.regimenIva} onChange={(e) => onCambio({ ...form, regimenIva: e.target.value })}>
        <OpcionesSelect opciones={REGIMENES_IVA} />
      </select>
      {esProveedor && (
        <select className="select" aria-label="Régimen de ISR" value={form.regimenIsr} onChange={(e) => onCambio({ ...form, regimenIsr: e.target.value })}>
          <OpcionesSelect opciones={REGIMENES_ISR} />
        </select>
      )}
      <label>
        <input type="checkbox" checked={form.esResidente} onChange={(e) => onCambio({ ...form, esResidente: e.target.checked })} />
        <span>Residente en Guatemala</span>
      </label>
      <label>
        <input type="checkbox" checked={form.esAgenteRetencionIva} onChange={(e) => onCambio({ ...form, esAgenteRetencionIva: e.target.checked })} />
        <span>Agente de retención de IVA</span>
      </label>
    </>
  );
}

function Contrapartes() {
  const [tipo, setTipo] = useState("Cliente");
  const [lista, setLista] = useState([]);
  const [form, setForm] = useState(vacio("Cliente"));
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const esProveedor = tipo === "Proveedor";
  const encabezados = esProveedor ? [...ENCABEZADOS, "Régimen ISR"] : ENCABEZADOS;
  const nitMensaje = mensajeNit(form.nit, tipo);

  async function cargar(t) {
    const { data } = await contrapartesApi.listar(t);
    setLista(data);
  }

  useEffect(() => {
    void cargar(tipo);
    setForm(vacio(tipo));
    setError("");
  }, [tipo]);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await contrapartesApi.crear({ ...form, nit: form.nit.trim().toUpperCase() });
      setForm(vacio(tipo));
      await cargar(tipo);
    } catch (err) {
      setError(mensajeError(err, `No se pudo crear el/la ${tipo.toLowerCase()}.`));
    } finally {
      setCargando(false);
    }
  }

  return (
    <div>
      <h2>Clientes y proveedores</h2>

      <div className="lineas-toolbar">
        <button type="button" className={`btn btn-sm ${tipo === "Cliente" ? "btn-primary" : "btn-outline"}`} onClick={() => setTipo("Cliente")}>
          Clientes
        </button>
        <button type="button" className={`btn btn-sm ${esProveedor ? "btn-primary" : "btn-outline"}`} onClick={() => setTipo("Proveedor")}>
          Proveedores
        </button>
      </div>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Nombre" aria-label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <input className="input" placeholder={esProveedor ? "NIT" : "NIT (opcional, CF si no tiene)"} aria-label="NIT" value={form.nit} onChange={(e) => setForm({ ...form, nit: e.target.value })} />
        <input className="input" placeholder="Dirección (opcional)" aria-label="Dirección" value={form.direccion} onChange={(e) => setForm({ ...form, direccion: e.target.value })} />
        <CamposFiscales form={form} onCambio={setForm} esProveedor={esProveedor} />
        <button type="submit" disabled={cargando || nitMensaje !== ""} className="btn btn-primary">
          Agregar {tipo.toLowerCase()}
        </button>
      </form>
      {nitMensaje && <p className="warning-chip">{nitMensaje}</p>}
      {error && <p className="error-chip">{error}</p>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                {encabezados.map((titulo) => (
                  <th key={titulo}>{titulo}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {lista.length === 0 ? (
                <tr>
                  <td colSpan={encabezados.length}>No hay {tipo.toLowerCase()}s registrados todavía.</td>
                </tr>
              ) : (
                lista.map((c) => (
                  <tr key={c.id}>
                    <td>{c.nombre}</td>
                    <td>{c.nit || "—"}</td>
                    <td>{c.direccion || "—"}</td>
                    <td>{etiquetaDe(REGIMENES_IVA, c.regimenIva)}</td>
                    <td>{siNo(c.esResidente)}</td>
                    <td>{siNo(c.esAgenteRetencionIva)}</td>
                    {esProveedor && <td>{etiquetaDe(REGIMENES_ISR, c.regimenIsr)}</td>}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export default Contrapartes;
