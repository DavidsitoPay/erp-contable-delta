import { useEffect, useState } from "react";
import { periodosApi } from "../../services/api";
import { FormularioCatalogo, TablaCatalogo } from "./CatalogoCrud";

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
    void cargar();
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
      <h2>Periodos contables</h2>

      <FormularioCatalogo onSubmit={handleSubmit} cargando={cargando} error={error}>
        <input className="input" placeholder="Nombre" aria-label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <input type="date" className="input" aria-label="Fecha de inicio" value={form.fechaInicio} onChange={(e) => setForm({ ...form, fechaInicio: e.target.value })} required />
        <input type="date" className="input" aria-label="Fecha de fin" value={form.fechaFin} onChange={(e) => setForm({ ...form, fechaFin: e.target.value })} required />
      </FormularioCatalogo>

      <TablaCatalogo encabezados={["Nombre", "Fecha inicio", "Fecha fin", "Estado"]}>
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
      </TablaCatalogo>
    </div>
  );
}

export default PeriodosContables;
