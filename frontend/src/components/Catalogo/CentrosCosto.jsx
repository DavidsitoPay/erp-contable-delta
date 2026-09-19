import { useEffect, useState } from "react";
import { centrosCostoApi } from "../../services/api";

const vacio = { codigo: "", nombre: "" };

function CentrosCosto() {
  const [centros, setCentros] = useState([]);
  const [form, setForm] = useState(vacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function cargar() {
    const { data } = await centrosCostoApi.listar();
    setCentros(data);
  }

  useEffect(() => {
    cargar();
  }, []);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await centrosCostoApi.crear(form);
      setForm(vacio);
      await cargar();
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo crear el centro de costo.");
    } finally {
      setCargando(false);
    }
  }

  async function handleDesactivar(id) {
    if (!confirm("¿Desactivar este centro de costo?")) return;
    await centrosCostoApi.desactivar(id);
    await cargar();
  }

  return (
    <div>
      <h2>Centros de costo</h2>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Código" aria-label="Código" value={form.codigo} onChange={(e) => setForm({ ...form, codigo: e.target.value })} required />
        <input className="input" placeholder="Nombre" aria-label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <button type="submit" disabled={cargando} className="btn btn-primary">Agregar</button>
      </form>
      {error && <p className="error-chip">{error}</p>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr><th>Código</th><th>Nombre</th><th></th></tr>
            </thead>
            <tbody>
              {centros.map((c) => (
                <tr key={c.id}>
                  <td>{c.codigo}</td>
                  <td>{c.nombre}</td>
                  <td><button className="btn btn-danger-outline btn-sm" onClick={() => handleDesactivar(c.id)}>Desactivar</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export default CentrosCosto;
