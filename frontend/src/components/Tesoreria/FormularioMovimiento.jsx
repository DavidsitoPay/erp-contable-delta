import { useState } from "react";
import { movimientosTesoreriaApi } from "../../services/api";
import { cuentasHoja } from "../../utils/cuentas";
import { hoyIso, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import SelectCuentaBancaria from "./SelectCuentaBancaria";

const OPERACIONES = ["Ingreso", "Egreso", "Transferencia"];

function estadoInicial({ cuentaBancariaId = "", fecha = hoyIso() }) {
  return {
    operacion: "Ingreso",
    cuentaBancariaId: String(cuentaBancariaId),
    cuentaDestinoId: "",
    fecha,
    monto: "",
    descripcion: "",
    referencia: "",
    contrapartidaId: "",
  };
}

function construirSolicitud(form) {
  const comunes = {
    fecha: form.fecha,
    monto: monto(form.monto),
    descripcion: form.descripcion.trim(),
    referencia: form.referencia || null,
  };
  if (form.operacion === "Transferencia") {
    return { ...comunes, cuentaOrigenId: Number(form.cuentaBancariaId), cuentaDestinoId: Number(form.cuentaDestinoId) };
  }
  return {
    ...comunes,
    cuentaBancariaId: Number(form.cuentaBancariaId),
    tipo: form.operacion,
    cuentaContrapartidaId: Number(form.contrapartidaId),
  };
}

function FormularioMovimiento({ cuentasBancarias, cuentasContables, inicial = {}, onRegistrado }) {
  const [form, setForm] = useState(() => estadoInicial(inicial));
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const esTransferencia = form.operacion === "Transferencia";
  const contrapartidas = cuentasHoja(cuentasContables);

  function cambiar(campo, valor) {
    setForm((prev) => ({ ...prev, [campo]: valor }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      const solicitud = construirSolicitud(form);
      await (esTransferencia ? movimientosTesoreriaApi.transferir(solicitud) : movimientosTesoreriaApi.crear(solicitud));
      setForm(estadoInicial(inicial));
      await onRegistrado();
    } catch (err) {
      setError(mensajeError(err, "No se pudo registrar el movimiento."));
    } finally {
      setCargando(false);
    }
  }

  return (
    <FormularioCatalogo onSubmit={handleSubmit} cargando={cargando} error={error} etiquetaBoton="Registrar">
      <select className="select" aria-label="Operación" value={form.operacion} onChange={(e) => cambiar("operacion", e.target.value)}>
        {OPERACIONES.map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
      <SelectCuentaBancaria
        etiqueta={esTransferencia ? "Cuenta de origen" : "Cuenta bancaria"}
        valor={form.cuentaBancariaId}
        onChange={(valor) => cambiar("cuentaBancariaId", valor)}
        cuentasBancarias={cuentasBancarias}
      />
      {esTransferencia && (
        <SelectCuentaBancaria
          etiqueta="Cuenta de destino"
          valor={form.cuentaDestinoId}
          onChange={(valor) => cambiar("cuentaDestinoId", valor)}
          cuentasBancarias={cuentasBancarias}
        />
      )}
      <input type="date" className="input" aria-label="Fecha del movimiento" value={form.fecha} onChange={(e) => cambiar("fecha", e.target.value)} required />
      <input type="number" className="input input-money" aria-label="Monto del movimiento" min="0.01" step="0.01" placeholder="0.00" value={form.monto} onChange={(e) => cambiar("monto", e.target.value)} required />
      <input className="input" placeholder="Descripción" aria-label="Descripción del movimiento" value={form.descripcion} onChange={(e) => cambiar("descripcion", e.target.value)} required />
      <input className="input" placeholder="Referencia (opcional)" aria-label="Referencia del movimiento" value={form.referencia} onChange={(e) => cambiar("referencia", e.target.value)} />
      {!esTransferencia && (
        <select className="select" aria-label="Cuenta de contrapartida" value={form.contrapartidaId} onChange={(e) => cambiar("contrapartidaId", e.target.value)} required>
          <option value="">Selecciona contrapartida</option>
          {contrapartidas.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
        </select>
      )}
    </FormularioCatalogo>
  );
}

export default FormularioMovimiento;
