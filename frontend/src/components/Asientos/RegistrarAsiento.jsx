import { useEffect, useState } from "react";
import { asientosApi, centrosCostoApi, cuentasApi, periodosApi } from "../../services/api";

function hoyIso() {
  return new Date().toISOString().slice(0, 10);
}

function lineaVacia() {
  return { cuentaId: "", centroCostoId: "", debito: "", credito: "" };
}

function lineasVacias() {
  return [lineaVacia(), lineaVacia()];
}

const vacio = { numero: "", fecha: hoyIso(), periodoId: "" };

function monto(valor) {
  const n = parseFloat(valor);
  return Number.isFinite(n) ? n : 0;
}

// Redondea a centavos antes de comparar para evitar falsos "no cuadra" por
// errores de precisión de punto flotante en la suma de líneas.
function redondear(n) {
  return Math.round(n * 100) / 100;
}

function RegistrarAsiento() {
  const [form, setForm] = useState(vacio);
  const [lineas, setLineas] = useState(lineasVacias());
  const [cuentas, setCuentas] = useState([]);
  const [centros, setCentros] = useState([]);
  const [periodos, setPeriodos] = useState([]);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    // Se piden TODAS las cuentas (activas e inactivas): el backend decide si
    // una cuenta es "de mayor" mirando si CUALQUIER cuenta (activa o no) la
    // declara como padre. Si aquí solo pidiéramos activas, una cuenta cuyos
    // únicos hijos están inactivos se clasificaría (mal) como hoja.
    cuentasApi.listar(true).then(({ data }) => setCuentas(data));
    centrosCostoApi.listar().then(({ data }) => setCentros(data));
    periodosApi.listar().then(({ data }) => setPeriodos(data));
  }, []);

  // Solo cuentas "hoja" (que ninguna otra cuenta declara como su padre) pueden
  // recibir movimientos: el backend rechaza cuentas de mayor, esto es solo la
  // versión UX de esa misma regla para no dejar seleccionar algo que fallará.
  // idsConHijos se calcula sobre TODAS las cuentas (activas e inactivas) para
  // igualar la regla del backend; el <select> luego solo ofrece las activas.
  const idsConHijos = new Set(cuentas.filter((c) => c.cuentaPadreId).map((c) => c.cuentaPadreId));
  const cuentasHoja = cuentas.filter((c) => c.activa && !idsConHijos.has(c.id));

  // Un periodo cerrado siempre será rechazado por el servidor, así que ni se ofrece.
  const periodosAbiertos = periodos.filter((p) => p.estado === "Abierto");

  const totalDebito = redondear(lineas.reduce((acc, l) => acc + monto(l.debito), 0));
  const totalCredito = redondear(lineas.reduce((acc, l) => acc + monto(l.credito), 0));
  const diferencia = redondear(totalDebito - totalCredito);
  const cuadrado = diferencia === 0;

  const lineasConMonto = lineas.filter((l) => monto(l.debito) > 0 || monto(l.credito) > 0);
  const puedeEnviar =
    !enviando &&
    form.numero.trim() !== "" &&
    form.fecha !== "" &&
    form.periodoId !== "" &&
    lineasConMonto.length >= 2 &&
    lineasConMonto.every((l) => l.cuentaId !== "") &&
    cuadrado &&
    totalDebito > 0;

  function actualizarLinea(index, campo, valor) {
    setLineas((prev) => prev.map((l, i) => (i === index ? { ...l, [campo]: valor } : l)));
  }

  function agregarLinea() {
    setLineas((prev) => [...prev, lineaVacia()]);
  }

  function quitarLinea(index) {
    setLineas((prev) => (prev.length <= 2 ? prev : prev.filter((_, i) => i !== index)));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setMensaje("");
    setEnviando(true);
    try {
      const payload = {
        numero: form.numero.trim(),
        fecha: form.fecha,
        periodoId: Number(form.periodoId),
        monto: totalDebito,
        estado: "Confirmado",
        lineas: lineasConMonto.map((l) => ({
          cuentaId: Number(l.cuentaId),
          centroCostoId: l.centroCostoId ? Number(l.centroCostoId) : null,
          debito: monto(l.debito),
          credito: monto(l.credito),
        })),
      };
      await asientosApi.crear(payload);
      setMensaje(`Asiento ${form.numero} registrado correctamente.`);
      // Se limpian número y líneas para la siguiente captura, pero se conservan
      // fecha y periodo: es normal que el contador registre varios asientos
      // seguidos del mismo día y periodo.
      setForm((prev) => ({ ...prev, numero: "" }));
      setLineas(lineasVacias());
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo registrar el asiento.");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div>
      <span className="eyebrow">Módulo M2</span>
      <h2>Registrar asiento contable</h2>

      <form onSubmit={handleSubmit}>
        <div className="catalog-form">
          <input
            className="input"
            placeholder="Número de asiento"
            value={form.numero}
            onChange={(e) => setForm({ ...form, numero: e.target.value })}
            maxLength={30}
            required
          />
          <input
            type="date"
            className="input"
            value={form.fecha}
            onChange={(e) => setForm({ ...form, fecha: e.target.value })}
            required
          />
          <select
            className="select"
            value={form.periodoId}
            onChange={(e) => setForm({ ...form, periodoId: e.target.value })}
            required
          >
            <option value="">Selecciona periodo</option>
            {periodosAbiertos.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="table-wrap">
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Cuenta</th>
                  <th>Centro de costo</th>
                  <th>Débito</th>
                  <th>Crédito</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {lineas.map((linea, index) => (
                  <tr key={index}>
                    <td>
                      <select
                        className="select"
                        aria-label="Cuenta"
                        value={linea.cuentaId}
                        onChange={(e) => actualizarLinea(index, "cuentaId", e.target.value)}
                      >
                        <option value="">Selecciona cuenta</option>
                        {cuentasHoja.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.codigo} - {c.nombre}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <select
                        className="select"
                        aria-label="Centro de costo"
                        value={linea.centroCostoId}
                        onChange={(e) => actualizarLinea(index, "centroCostoId", e.target.value)}
                      >
                        <option value="">(ninguno)</option>
                        {centros.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.codigo} - {c.nombre}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        type="number"
                        className="input input-money"
                        aria-label="Débito"
                        step="0.01"
                        min="0"
                        placeholder="0.00"
                        value={linea.debito}
                        onChange={(e) => {
                          const valor = e.target.value;
                          actualizarLinea(index, "debito", valor);
                          if (monto(valor) > 0) {
                            actualizarLinea(index, "credito", "");
                          }
                        }}
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        className="input input-money"
                        aria-label="Crédito"
                        step="0.01"
                        min="0"
                        placeholder="0.00"
                        value={linea.credito}
                        onChange={(e) => {
                          const valor = e.target.value;
                          actualizarLinea(index, "credito", valor);
                          if (monto(valor) > 0) {
                            actualizarLinea(index, "debito", "");
                          }
                        }}
                      />
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-danger-outline btn-sm"
                        onClick={() => quitarLinea(index)}
                        disabled={lineas.length <= 2}
                      >
                        Quitar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="lineas-toolbar">
          <button type="button" className="btn btn-outline btn-sm" onClick={agregarLinea}>
            + Agregar línea
          </button>
        </div>

        <p className="partida-indicator" data-balance={cuadrado ? "ok" : "off"}>
          {cuadrado
            ? `Débitos: ${totalDebito.toFixed(2)} · Créditos: ${totalCredito.toFixed(2)} ✓ Cuadrado`
            : `Débitos: ${totalDebito.toFixed(2)} · Créditos: ${totalCredito.toFixed(2)} · Diferencia: ${Math.abs(diferencia).toFixed(2)}`}
        </p>

        {error && <p className="error-chip">{error}</p>}
        {mensaje && <p className="success-chip">{mensaje}</p>}

        <button type="submit" disabled={!puedeEnviar} className="btn btn-primary">
          {enviando ? "Registrando..." : "Registrar asiento"}
        </button>
      </form>
    </div>
  );
}

export default RegistrarAsiento;
