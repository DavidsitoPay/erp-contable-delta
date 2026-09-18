import { useEffect, useState } from "react";
import { periodosApi } from "../../services/api";

const vacio = { nombre: "", fechaInicio: "", fechaFin: "" };

function PeriodosContables() {
  const [periodos, setPeriodos] = useState([]);
  const [form, setForm] = useState(vacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function cargar() {
    const { data } = await periodosApi.listar();
    setPeriodos(data);
  }

  useEffect(() => {
    cargar();
  }, []);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await periodosApi.crear(form);
      setForm(vacio);
      await cargar();
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo crear el periodo.");
    } finally {
      setCargando(false);
    }
  }

  async function handleCerrar(id) {
    if (!confirm("¿Cerrar este periodo? Esta acción consolida los saldos y no se puede deshacer fácilmente.")) return;
    setError("");
    try {
      await periodosApi.cerrar(id);
      await cargar();
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo cerrar el periodo.");
    }
  }

  async function handleReabrir(id) {
    if (!confirm("¿Reabrir este periodo?")) return;
    setError("");
    try {
      await periodosApi.reabrir(id);
      await cargar();
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo reabrir el periodo.");
    }
  }

  return (
    <div>
      <span className="eyebrow">Cierre y control</span>
      <h2>Periodos contables</h2>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <input type="date" className="input" value={form.fechaInicio} onChange={(e) => setForm({ ...form, fechaInicio: e.target.value })} required />
        <input type="date" className="input" value={form.fechaFin} onChange={(e) => setForm({ ...form, fechaFin: e.target.value })} required />
        <button type="submit" disabled={cargando} className="btn btn-primary">Agregar</button>
      </form>
      {error && <p className="error-chip">{error}</p>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Nombre</th><th>Fecha inicio</th><th>Fecha fin</th><th>Estado</th><th></th>
              </tr>
            </thead>
            <tbody>
              {periodos.map((p) => (
                <tr key={p.id}>
                  <td>{p.nombre}</td>
                  <td>{p.fechaInicio}</td>
                  <td>{p.fechaFin}</td>
                  <td>{p.estado}</td>
                  <td>
                    {p.estado === "Abierto" && <button className="btn btn-danger-outline btn-sm" onClick={() => handleCerrar(p.id)}>Cerrar</button>}
                    {p.estado === "Cerrado" && <button className="btn btn-outline btn-sm" onClick={() => handleReabrir(p.id)}>Reabrir</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export default PeriodosContables;
