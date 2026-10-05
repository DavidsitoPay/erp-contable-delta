import { useState } from "react";
import { cuentasBancariasApi } from "../../services/api";
import { cuentasHojaDeTipo } from "../../utils/cuentas";
import { formatoMoneda, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo, TablaCatalogo } from "../Catalogo/CatalogoCrud";

const TIPOS = ["Monetaria", "Ahorro"];

const vacio = {
  banco: "",
  numero: "",
  tipo: "Monetaria",
  cuentaContableId: "",
  saldoApertura: "0",
  fechaApertura: "",
  cuentaContrapartidaId: "",
};

function construirSolicitud(form) {
  const saldoApertura = monto(form.saldoApertura);
  const solicitud = {
    banco: form.banco.trim(),
    numero: form.numero.trim(),
    tipo: form.tipo,
    cuentaContableId: Number(form.cuentaContableId),
    saldoApertura,
  };
  if (saldoApertura <= 0) return solicitud;
  return { ...solicitud, fechaApertura: form.fechaApertura, cuentaContrapartidaId: Number(form.cuentaContrapartidaId) };
}

function CuentasBancarias({ cuentasBancarias, cuentasContables, recargar }) {
  const [form, setForm] = useState(vacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const conApertura = monto(form.saldoApertura) > 0;
  const cuentasActivo = cuentasHojaDeTipo(cuentasContables, "Activo");
  const cuentasCapital = cuentasHojaDeTipo(cuentasContables, "Capital");

  function cambiar(campo, valor) {
    setForm((prev) => ({ ...prev, [campo]: valor }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      await cuentasBancariasApi.crear(construirSolicitud(form));
      setForm(vacio);
      await recargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo crear la cuenta bancaria."));
    } finally {
      setCargando(false);
    }
  }

  async function handleDesactivar(id) {
    if (!confirm("¿Desactivar esta cuenta bancaria?")) return;
    setError("");
    try {
      await cuentasBancariasApi.desactivar(id);
      await recargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo desactivar la cuenta bancaria."));
    }
  }

  return (
    <div>
      <h3>Cuentas bancarias</h3>

      <FormularioCatalogo onSubmit={handleSubmit} cargando={cargando} error={error}>
        <input className="input" placeholder="Banco" aria-label="Banco" value={form.banco} onChange={(e) => cambiar("banco", e.target.value)} required />
        <input className="input" placeholder="Número de cuenta" aria-label="Número de cuenta" value={form.numero} onChange={(e) => cambiar("numero", e.target.value)} required />
        <select className="select" aria-label="Tipo de cuenta" value={form.tipo} onChange={(e) => cambiar("tipo", e.target.value)}>
          {TIPOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <select className="select" aria-label="Cuenta contable" value={form.cuentaContableId} onChange={(e) => cambiar("cuentaContableId", e.target.value)} required>
          <option value="">Selecciona cuenta contable</option>
          {cuentasActivo.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
        </select>
        <input type="number" className="input input-money" aria-label="Saldo de apertura" min="0" step="0.01" value={form.saldoApertura} onChange={(e) => cambiar("saldoApertura", e.target.value)} required />
        {conApertura && (
          <>
            <input type="date" className="input" aria-label="Fecha de apertura" value={form.fechaApertura} onChange={(e) => cambiar("fechaApertura", e.target.value)} required />
            <select className="select" aria-label="Cuenta de contrapartida de la apertura" value={form.cuentaContrapartidaId} onChange={(e) => cambiar("cuentaContrapartidaId", e.target.value)} required>
              <option value="">Selecciona contrapartida (capital)</option>
              {cuentasCapital.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
            </select>
          </>
        )}
      </FormularioCatalogo>

      <TablaCatalogo encabezados={["Banco", "Número", "Tipo", "Cuenta contable", "Saldo de apertura", "Saldo", "Estado"]}>
        {cuentasBancarias.map((c) => (
          <tr key={c.id}>
            <td>{c.banco}</td>
            <td>{c.numero}</td>
            <td>{c.tipo}</td>
            <td>{c.cuentaContableCodigo} - {c.cuentaContableNombre}</td>
            <td className="numeric">{formatoMoneda.format(c.saldoApertura)}</td>
            <td className="numeric">{formatoMoneda.format(c.saldo)}</td>
            <td>{c.activa ? "Activa" : "Inactiva"}</td>
            <td>
              {c.activa && <button className="btn btn-danger-outline btn-sm" onClick={() => handleDesactivar(c.id)}>Desactivar</button>}
            </td>
          </tr>
        ))}
      </TablaCatalogo>
    </div>
  );
}

export default CuentasBancarias;
