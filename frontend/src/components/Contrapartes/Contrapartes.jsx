import { useEffect, useState } from "react";
import { contrapartesApi } from "../../services/api";

function vacio(tipo) {
  return { tipo, nombre: "", nit: "", direccion: "" };
}

function Contrapartes() {
  const [tipo, setTipo] = useState("Cliente");
  const [lista, setLista] = useState([]);
  const [form, setForm] = useState(vacio("Cliente"));
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function cargar(t) {
    const { data } = await contrapartesApi.listar(t);
    setLista(data);
  }

  useEffect(() => {
    cargar(tipo);
    setForm(vacio(tipo));
    setError("");
  }, [tipo]);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await contrapartesApi.crear(form);
      setForm(vacio(tipo));
      await cargar(tipo);
    } catch (err) {
      setError(err.response?.data?.error || `No se pudo crear el/la ${tipo.toLowerCase()}.`);
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
        <button type="button" className={`btn btn-sm ${tipo === "Proveedor" ? "btn-primary" : "btn-outline"}`} onClick={() => setTipo("Proveedor")}>
          Proveedores
        </button>
      </div>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Nombre" aria-label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <input className="input" placeholder="NIT (opcional)" aria-label="NIT" value={form.nit} onChange={(e) => setForm({ ...form, nit: e.target.value })} />
        <input className="input" placeholder="Dirección (opcional)" aria-label="Dirección" value={form.direccion} onChange={(e) => setForm({ ...form, direccion: e.target.value })} />
        <button type="submit" disabled={cargando} className="btn btn-primary">
          Agregar {tipo.toLowerCase()}
        </button>
      </form>
      {error && <p className="error-chip">{error}</p>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>NIT</th>
                <th>Dirección</th>
              </tr>
            </thead>
            <tbody>
              {lista.length === 0 ? (
                <tr>
                  <td colSpan="3">No hay {tipo.toLowerCase()}s registrados todavía.</td>
                </tr>
              ) : (
                lista.map((c) => (
                  <tr key={c.id}>
                    <td>{c.nombre}</td>
                    <td>{c.nit || "—"}</td>
                    <td>{c.direccion || "—"}</td>
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
