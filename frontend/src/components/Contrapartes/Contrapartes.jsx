import { useEffect, useState } from "react";
import { contrapartesApi, parametrosRetencionApi } from "../../services/api";
import { REGIMENES_ISR_CONTRAPARTE, REGIMENES_IVA, TIPOS_AGENTE_IVA_CLIENTE, etiquetaDe } from "../../utils/etiquetasFiscales";
import { contraparteCompleta, formularioContraparteVacio, payloadContraparte } from "../../utils/formularioContraparte";
import { hoyIso, mensajeError } from "../../utils/formato";
import { mensajeNit } from "../../utils/nit";
import CamposFiscalesContraparte from "./CamposFiscalesContraparte";

const ENCABEZADOS = ["Nombre", "NIT", "Dirección", "Régimen IVA", "Residente", "País", "Agente de retención IVA"];

const siNo = (valor) => (valor ? "Sí" : "No");

function Contrapartes() {
  const [tipo, setTipo] = useState("Cliente");
  const [lista, setLista] = useState([]);
  const [form, setForm] = useState(formularioContraparteVacio("Cliente"));
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);
  const [tiposRenta, setTiposRenta] = useState([]);

  const esProveedor = tipo === "Proveedor";
  const encabezados = esProveedor ? [...ENCABEZADOS, "Régimen ISR"] : ENCABEZADOS;
  const nitMensaje = mensajeNit(form.nit, tipo);

  async function cargar(t) {
    const { data } = await contrapartesApi.listar(t);
    setLista(data);
  }

  useEffect(() => {
    void cargar(tipo);
    setForm(formularioContraparteVacio(tipo));
    setError("");
  }, [tipo]);

  useEffect(() => {
    void parametrosRetencionApi.listar({ concepto: "ISR_NO_RESIDENTE", vigenteEn: hoyIso() }).then(({ data }) => setTiposRenta(data)).catch(() => setTiposRenta([]));
  }, []);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await contrapartesApi.crear(payloadContraparte(form));
      setForm(formularioContraparteVacio(tipo));
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
        <CamposFiscalesContraparte form={form} onCambio={setForm} tiposRenta={tiposRenta} />
        <button type="submit" disabled={cargando || nitMensaje !== "" || !contraparteCompleta(form)} className="btn btn-primary">
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
                    <td>{c.pais ?? "—"}</td>
                    <td>
                      {siNo(c.esAgenteRetencionIva)}
                      {c.tipoAgenteIva && <small className="nota-fiscal">{etiquetaDe(TIPOS_AGENTE_IVA_CLIENTE, c.tipoAgenteIva)}</small>}
                    </td>
                    {esProveedor && (
                      <td>
                        {etiquetaDe(REGIMENES_ISR_CONTRAPARTE, c.regimenIsr)}
                        {c.isrFundamento && <small className="nota-fiscal">{c.isrFundamento}</small>}
                      </td>
                    )}
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
