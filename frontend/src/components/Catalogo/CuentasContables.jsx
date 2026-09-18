import { useEffect, useState } from "react";
import { cuentasApi } from "../../services/api";

const TIPOS = ["Activo", "Pasivo", "Capital", "Ingreso", "Gasto"];
const NATURALEZAS = ["Deudora", "Acreedora"];

const vacio = { codigo: "", nombre: "", tipo: TIPOS[0], naturaleza: NATURALEZAS[0], cuentaPadreId: "" };

function CuentasContables() {
  const [cuentas, setCuentas] = useState([]);
  const [form, setForm] = useState(vacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function cargar() {
    const { data } = await cuentasApi.listar();
    setCuentas(data);
  }

  useEffect(() => {
    cargar();
  }, []);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await cuentasApi.crear({
        ...form,
        cuentaPadreId: form.cuentaPadreId ? Number(form.cuentaPadreId) : null,
      });
      setForm(vacio);
      await cargar();
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo crear la cuenta.");
    } finally {
      setCargando(false);
    }
  }

  async function handleDesactivar(id) {
    if (!confirm("¿Desactivar esta cuenta?")) return;
    await cuentasApi.desactivar(id);
    await cargar();
  }

  return (
    <div>
      <span className="eyebrow">Catálogo contable</span>
      <h2>Cuentas contables</h2>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Código" value={form.codigo} onChange={(e) => setForm({ ...form, codigo: e.target.value })} required />
        <input className="input" placeholder="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <select className="select" value={form.tipo} onChange={(e) => setForm({ ...form, tipo: e.target.value })}>
          {TIPOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <select className="select" value={form.naturaleza} onChange={(e) => setForm({ ...form, naturaleza: e.target.value })}>
          {NATURALEZAS.map((n) => <option key={n} value={n}>{n}</option>)}
        </select>
        <select className="select" value={form.cuentaPadreId} onChange={(e) => setForm({ ...form, cuentaPadreId: e.target.value })}>
          <option value="">(sin cuenta padre)</option>
          {cuentas.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
        </select>
        <button type="submit" disabled={cargando} className="btn btn-primary">Agregar</button>
      </form>
      {error && <p className="error-chip">{error}</p>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Código</th><th>Nombre</th><th>Tipo</th><th>Naturaleza</th><th>Cuenta padre</th><th></th>
              </tr>
            </thead>
            <tbody>
              {cuentas.map((c) => (
                <tr key={c.id}>
                  <td>{c.codigo}</td>
                  <td>{c.nombre}</td>
                  <td>{c.tipo}</td>
                  <td>{c.naturaleza}</td>
                  <td>{cuentas.find((p) => p.id === c.cuentaPadreId)?.codigo || "-"}</td>
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

export default CuentasContables;
