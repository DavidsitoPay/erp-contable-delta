import { useState } from "react";
import { cuentasBancariasApi } from "../../services/api";
import { MONEDAS_CUENTA, camposMoneda, esMonedaExtranjera, monedaDeCuenta } from "../../utils/cambio";
import { cuentasHojaDeTipo } from "../../utils/cuentas";
import { formatoImporte, formatoQuetzales, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo, TablaCatalogo } from "../Catalogo/CatalogoCrud";
import SelectMoneda from "../SelectMoneda";

const TIPOS = ["Monetaria", "Ahorro"];

const vacio = {
  banco: "",
  numero: "",
  tipo: "Monetaria",
  monedaCodigo: "GTQ",
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
    ...camposMoneda(form.monedaCodigo),
  };
  if (saldoApertura <= 0) return solicitud;
  return { ...solicitud, fechaApertura: form.fechaApertura, cuentaContrapartidaId: Number(form.cuentaContrapartidaId) };
}

function CuentasBancarias({ cuentasBancarias, cuentasContables, recargar }) {
  const [form, setForm] = useState(vacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const conApertura = monto(form.saldoApertura) > 0;
  const etiquetaApertura = esMonedaExtranjera(form.monedaCodigo) ? `Saldo de apertura en ${form.monedaCodigo}` : "Saldo de apertura";
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
        <SelectMoneda etiqueta="Moneda de la cuenta" valor={form.monedaCodigo} onChange={(codigo) => cambiar("monedaCodigo", codigo)} opciones={MONEDAS_CUENTA} />
        <select className="select" aria-label="Cuenta contable" value={form.cuentaContableId} onChange={(e) => cambiar("cuentaContableId", e.target.value)} required>
          <option value="">Selecciona cuenta contable</option>
          {cuentasActivo.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
        </select>
        <input type="number" className="input input-money" aria-label={etiquetaApertura} min="0" step="0.01" value={form.saldoApertura} onChange={(e) => cambiar("saldoApertura", e.target.value)} required />
        {conApertura && (
          <>
            <input type="date" className="input" aria-label="Fecha de apertura" value={form.fechaApertura} onChange={(e) => cambiar("fechaApertura", e.target.value)} required />
            <select className="select" aria-label="Cuenta de contrapartida de la apertura" value={form.cuentaContrapartidaId} onChange={(e) => cambiar("cuentaContrapartidaId", e.target.value)} required>
              <option value="">Selecciona contrapartida (capital)</option>
              {cuentasCapital.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
            </select>
            {esMonedaExtranjera(form.monedaCodigo) && <small className="nota-fiscal">{`El saldo de apertura en ${form.monedaCodigo} se valora al tipo de cambio de la fecha de apertura.`}</small>}
          </>
        )}
      </FormularioCatalogo>

      <TablaCatalogo encabezados={["Banco", "Número", "Tipo", "Moneda", "Cuenta contable", "Saldo de apertura", "Saldo", "Saldo en libros Q", "Estado"]}>
        {cuentasBancarias.map((c) => (
          <tr key={c.id}>
            <td>{c.banco}</td>
            <td>{c.numero}</td>
            <td>{c.tipo}</td>
            <td>{monedaDeCuenta(c)}</td>
            <td>{c.cuentaContableCodigo} - {c.cuentaContableNombre}</td>
            <td className="numeric">{formatoImporte(c.saldoApertura, monedaDeCuenta(c))}</td>
            <td className="numeric">{formatoImporte(c.saldo, monedaDeCuenta(c))}</td>
            <td className="numeric">{esMonedaExtranjera(monedaDeCuenta(c)) ? formatoQuetzales(c.saldoLibrosGtq) : null}</td>
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
