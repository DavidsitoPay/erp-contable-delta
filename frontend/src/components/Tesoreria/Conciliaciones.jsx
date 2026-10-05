import { useCallback, useEffect, useState } from "react";
import { conciliacionesApi } from "../../services/api";
import { etiquetaCuenta, indexarPorId } from "../../utils/cuentas";
import { formatoMoneda, hoyIso, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo, TablaCatalogo } from "../Catalogo/CatalogoCrud";
import ConciliacionDetalle from "./ConciliacionDetalle";
import SelectCuentaBancaria from "./SelectCuentaBancaria";

function formVacio() {
  return { cuentaBancariaId: "", fecha: hoyIso(), saldoExtracto: "" };
}

function Conciliaciones({ cuentasBancarias, cuentasContables }) {
  const [conciliaciones, setConciliaciones] = useState([]);
  const [seleccionada, setSeleccionada] = useState(null);
  const [form, setForm] = useState(formVacio);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const cargar = useCallback(async () => {
    const { data } = await conciliacionesApi.listar();
    setConciliaciones(data);
  }, []);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  function cambiar(campo, valor) {
    setForm((prev) => ({ ...prev, [campo]: valor }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      const { data } = await conciliacionesApi.crear({
        cuentaBancariaId: Number(form.cuentaBancariaId),
        fecha: form.fecha,
        saldoExtracto: monto(form.saldoExtracto),
      });
      setForm(formVacio());
      setSeleccionada(data.id);
      await cargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo crear la conciliación."));
    } finally {
      setCargando(false);
    }
  }

  const cuentasPorId = indexarPorId(cuentasBancarias);

  return (
    <div>
      <h3>Conciliaciones bancarias</h3>

      <FormularioCatalogo onSubmit={handleSubmit} cargando={cargando} error={error}>
        <SelectCuentaBancaria etiqueta="Cuenta bancaria" valor={form.cuentaBancariaId} onChange={(valor) => cambiar("cuentaBancariaId", valor)} cuentasBancarias={cuentasBancarias} />
        <input type="date" className="input" aria-label="Fecha de corte" value={form.fecha} onChange={(e) => cambiar("fecha", e.target.value)} required />
        <input type="number" className="input input-money" aria-label="Saldo del extracto" step="0.01" placeholder="0.00" value={form.saldoExtracto} onChange={(e) => cambiar("saldoExtracto", e.target.value)} required />
      </FormularioCatalogo>

      <TablaCatalogo encabezados={["Cuenta", "Fecha de corte", "Saldo del extracto", "Estado"]}>
        {conciliaciones.map((c) => (
          <tr key={c.id}>
            <td>{etiquetaCuenta(cuentasPorId[c.cuentaBancariaId])}</td>
            <td>{c.fecha}</td>
            <td className="numeric">{formatoMoneda.format(c.saldoExtracto)}</td>
            <td>{c.estado}</td>
            <td>
              <button type="button" className="btn btn-outline btn-sm" aria-label={`Ver detalle de conciliación ${c.id}`} onClick={() => setSeleccionada(c.id)}>
                Ver detalle
              </button>
            </td>
          </tr>
        ))}
      </TablaCatalogo>

      {seleccionada !== null && (
        <ConciliacionDetalle id={seleccionada} cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onCambio={cargar} />
      )}
    </div>
  );
}

export default Conciliaciones;
