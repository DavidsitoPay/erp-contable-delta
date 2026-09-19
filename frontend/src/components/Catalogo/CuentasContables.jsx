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
  const [busqueda, setBusqueda] = useState("");
  const [filtroTipo, setFiltroTipo] = useState("");
  const [incluirInactivas, setIncluirInactivas] = useState(false);
  const [ordenCampo, setOrdenCampo] = useState("codigo");
  const [ordenAsc, setOrdenAsc] = useState(true);

  async function cargar(inactivas = false) {
    const { data } = await cuentasApi.listar(inactivas);
    setCuentas(data);
  }

  useEffect(() => {
    cargar(incluirInactivas);
  }, [incluirInactivas]);

  function calcularProfundidad(cuentaId, maxIteraciones = 10) {
    let profundidad = 0;
    let actual = cuentas.find((c) => c.id === cuentaId);
    let iteraciones = 0;
    while (actual && actual.cuentaPadreId && iteraciones < maxIteraciones) {
      actual = cuentas.find((c) => c.id === actual.cuentaPadreId);
      if (actual) profundidad++;
      iteraciones++;
    }
    return profundidad;
  }

  function tieneHijos(cuentaId) {
    return cuentas.some((c) => c.cuentaPadreId === cuentaId);
  }

  const busquedaTrimmed = busqueda.trim();
  const hayFiltroActivo = busquedaTrimmed !== "" || filtroTipo !== "";
  const estaOrdenadoPorDefecto = ordenCampo === "codigo" && ordenAsc;

  const cuentasFiltradas = cuentas.filter((c) => {
    const cumpleBusqueda =
      busquedaTrimmed === "" ||
      c.codigo.toLowerCase().includes(busquedaTrimmed.toLowerCase()) ||
      c.nombre.toLowerCase().includes(busquedaTrimmed.toLowerCase());
    const cumpleTipo = filtroTipo === "" || c.tipo === filtroTipo;
    return cumpleBusqueda && cumpleTipo;
  });

  const cuentasFinales = [...cuentasFiltradas].sort((a, b) => {
    let resultado = 0;
    if (ordenCampo === "codigo") {
      resultado = a.codigo.localeCompare(b.codigo, "es");
    } else if (ordenCampo === "nombre") {
      resultado = a.nombre.localeCompare(b.nombre, "es");
    } else if (ordenCampo === "tipo") {
      resultado = a.tipo.localeCompare(b.tipo, "es");
    } else if (ordenCampo === "naturaleza") {
      resultado = a.naturaleza.localeCompare(b.naturaleza, "es");
    }
    return ordenAsc ? resultado : -resultado;
  });

  const debeAplanar = hayFiltroActivo || !estaOrdenadoPorDefecto;

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
      await cargar(incluirInactivas);
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo crear la cuenta.");
    } finally {
      setCargando(false);
    }
  }

  async function handleDesactivar(id) {
    if (!confirm("¿Desactivar esta cuenta?")) return;
    await cuentasApi.desactivar(id);
    await cargar(incluirInactivas);
  }

  function handleOrdenar(campo) {
    if (ordenCampo === campo) {
      setOrdenAsc(!ordenAsc);
    } else {
      setOrdenCampo(campo);
      setOrdenAsc(true);
    }
  }

  const etiquetaOrden = (campo) => {
    if (ordenCampo === campo) {
      return ordenAsc ? ` ▲` : ` ▼`;
    }
    return "";
  };

  return (
    <div>
      <h2>Cuentas contables</h2>

      <form onSubmit={handleSubmit} className="catalog-form">
        <input className="input" placeholder="Código" aria-label="Código" value={form.codigo} onChange={(e) => setForm({ ...form, codigo: e.target.value })} required />
        <input className="input" placeholder="Nombre" aria-label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} required />
        <select className="select" aria-label="Tipo" value={form.tipo} onChange={(e) => setForm({ ...form, tipo: e.target.value })}>
          {TIPOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <select className="select" aria-label="Naturaleza" value={form.naturaleza} onChange={(e) => setForm({ ...form, naturaleza: e.target.value })}>
          {NATURALEZAS.map((n) => <option key={n} value={n}>{n}</option>)}
        </select>
        <select className="select" aria-label="Cuenta padre" value={form.cuentaPadreId} onChange={(e) => setForm({ ...form, cuentaPadreId: e.target.value })}>
          <option value="">(sin cuenta padre)</option>
          {cuentas.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
        </select>
        <button type="submit" disabled={cargando} className="btn btn-primary">Agregar</button>
      </form>
      {error && <p className="error-chip">{error}</p>}

      <div className="catalog-form">
        <input
          className="input"
          type="text"
          placeholder="Buscar por código o nombre"
          aria-label="Buscar cuenta"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
        />
        <select
          className="select"
          aria-label="Filtrar por tipo"
          value={filtroTipo}
          onChange={(e) => setFiltroTipo(e.target.value)}
        >
          <option value="">Todos los tipos</option>
          {TIPOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <label>
          <input
            type="checkbox"
            checked={incluirInactivas}
            onChange={(e) => setIncluirInactivas(e.target.checked)}
          />
          Incluir inactivas
        </label>
      </div>

      <p>Mostrando {cuentasFinales.length} de {cuentas.length} cuentas</p>

      <div className="table-wrap">
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th><button type="button" onClick={() => handleOrdenar("codigo")} aria-label="Ordenar por código">Código{etiquetaOrden("codigo")}</button></th>
                <th><button type="button" onClick={() => handleOrdenar("nombre")} aria-label="Ordenar por nombre">Nombre{etiquetaOrden("nombre")}</button></th>
                <th><button type="button" onClick={() => handleOrdenar("tipo")} aria-label="Ordenar por tipo">Tipo{etiquetaOrden("tipo")}</button></th>
                <th><button type="button" onClick={() => handleOrdenar("naturaleza")} aria-label="Ordenar por naturaleza">Naturaleza{etiquetaOrden("naturaleza")}</button></th>
                <th>Cuenta padre</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {cuentasFinales.length === 0 ? (
                <tr>
                  <td colSpan="6">No se encontraron cuentas con esos criterios.</td>
                </tr>
              ) : (
                cuentasFinales.map((c) => {
                  const profundidad = debeAplanar ? 0 : calcularProfundidad(c.id);
                  const esParent = tieneHijos(c.id);
                  return (
                    <tr key={c.id}>
                      <td style={{ paddingLeft: `${0.75 + profundidad * 1.25}rem`, fontWeight: esParent ? 600 : 400 }}>{c.codigo}</td>
                      <td>{c.nombre}</td>
                      <td>{c.tipo}</td>
                      <td>{c.naturaleza}</td>
                      <td>{cuentas.find((p) => p.id === c.cuentaPadreId)?.codigo || "-"}</td>
                      <td><button className="btn btn-danger-outline btn-sm" onClick={() => handleDesactivar(c.id)}>Desactivar</button></td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export default CuentasContables;
