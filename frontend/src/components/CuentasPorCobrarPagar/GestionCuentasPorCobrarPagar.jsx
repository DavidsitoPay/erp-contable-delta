import { useEffect, useState } from "react";
import { centrosCostoApi, contrapartesApi, cuentasApi, cuentasBancariasApi, periodosApi } from "../../services/api";
import { cuentasHoja } from "../../utils/cuentas";
import { formatoMoneda, hoyIso, monto } from "../../utils/formato";
import { perfilActual, puedeGestionarTesoreria } from "../../utils/perfiles";
import SelectCuentaBancaria from "../Tesoreria/SelectCuentaBancaria";
import FormularioFactura from "./FormularioFactura";
import TablaFacturas from "./TablaFacturas";

// config aísla las diferencias de terminología/cuenta de control entre CxC y
// CxP, de ahí las claves dinámicas como pagoContraparteId.
function GestionCuentasPorCobrarPagar({ config }) {
  const {
    tipoContraparte,
    etiquetaContraparte,
    campoContraparteId,
    campoContraparteNombre,
    api,
    filtroCuentaControl,
  } = config;

  const [subTab, setSubTab] = useState("facturas");

  const [contrapartes, setContrapartes] = useState([]);
  const [cuentas, setCuentas] = useState([]);
  const [centros, setCentros] = useState([]);
  const [periodos, setPeriodos] = useState([]);
  const [facturas, setFacturas] = useState([]);
  const [pagos, setPagos] = useState([]);
  const [cuentasBancarias, setCuentasBancarias] = useState([]);
  const [puedeRegistrarPagos] = useState(() => puedeGestionarTesoreria(perfilActual()));

  const [errorPago, setErrorPago] = useState("");
  const [enviandoPago, setEnviandoPago] = useState(false);
  const [pagoContraparteId, setPagoContraparteId] = useState("");
  const [pagoCuentaBancariaId, setPagoCuentaBancariaId] = useState("");
  const [pagoFecha, setPagoFecha] = useState(hoyIso());
  const [pagoMetodo, setPagoMetodo] = useState("");
  const [pagoReferencia, setPagoReferencia] = useState("");
  const [montosAplicados, setMontosAplicados] = useState({}); // documentoId -> string

  async function cargarFacturas() {
    const { data } = await api.listarFacturas();
    setFacturas(data);
  }

  async function cargarPagos() {
    const { data } = await api.listarPagos();
    setPagos(data);
  }

  useEffect(() => {
    void contrapartesApi.listar(tipoContraparte).then(({ data }) => setContrapartes(data));
    void cuentasApi.listar(true).then(({ data }) => setCuentas(data));
    void centrosCostoApi.listar().then(({ data }) => setCentros(data));
    void periodosApi.listar().then(({ data }) => setPeriodos(data));
    void cargarFacturas();
    void cargarPagos();
    if (puedeRegistrarPagos) {
      void cuentasBancariasApi.listar().then(({ data }) => setCuentasBancarias(data));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tipoContraparte]);

  const hojas = cuentasHoja(cuentas);
  const cuentasControl = hojas.filter(filtroCuentaControl);
  const periodosAbiertos = periodos.filter((p) => p.estado === "Abierto");

  const facturasDeLaContraparte = facturas.filter(
    (f) => String(f[campoContraparteId]) === String(pagoContraparteId) && f.estado === "Vigente" && f.saldoPendiente > 0
  );

  function actualizarMontoAplicado(documentoId, valor) {
    setMontosAplicados((prev) => ({ ...prev, [documentoId]: valor }));
  }

  const aplicacionesConMonto = facturasDeLaContraparte
    .map((f) => ({ documentoId: f.id, montoAplicado: monto(montosAplicados[f.id]) }))
    .filter((a) => a.montoAplicado > 0);

  const puedeEnviarPago =
    !enviandoPago && pagoContraparteId !== "" && pagoCuentaBancariaId !== "" && pagoMetodo.trim() !== "" && aplicacionesConMonto.length > 0;

  async function handleSubmitPago(e) {
    e.preventDefault();
    setErrorPago("");
    setEnviandoPago(true);
    try {
      await api.crearPago({
        [campoContraparteId]: Number(pagoContraparteId),
        cuentaBancariaId: Number(pagoCuentaBancariaId),
        fecha: pagoFecha,
        metodoPago: pagoMetodo.trim(),
        referenciaBancaria: pagoReferencia || null,
        aplicaciones: aplicacionesConMonto,
      });
      setPagoMetodo("");
      setPagoReferencia("");
      setMontosAplicados({});
      await Promise.all([cargarFacturas(), cargarPagos()]);
    } catch (err) {
      setErrorPago(err.response?.data?.error || "No se pudo registrar el pago.");
    } finally {
      setEnviandoPago(false);
    }
  }

  return (
    <div>
      <h2>{config.titulo}</h2>

      <div className="lineas-toolbar">
        <button type="button" className={`btn btn-sm ${subTab === "facturas" ? "btn-primary" : "btn-outline"}`} onClick={() => setSubTab("facturas")}>
          Facturas
        </button>
        <button type="button" className={`btn btn-sm ${subTab === "pagos" ? "btn-primary" : "btn-outline"}`} onClick={() => setSubTab("pagos")}>
          Pagos
        </button>
      </div>

      {subTab === "facturas" && (
        <>
          <FormularioFactura
            config={config}
            contrapartes={contrapartes}
            hojas={hojas}
            cuentasControl={cuentasControl}
            centros={centros}
            periodosAbiertos={periodosAbiertos}
            facturas={facturas}
            onRegistrada={cargarFacturas}
          />

          <h3>Facturas registradas</h3>
          <TablaFacturas facturas={facturas} etiquetaContraparte={etiquetaContraparte} campoContraparteNombre={campoContraparteNombre} />
        </>
      )}

      {subTab === "pagos" && (
        <>
          {puedeRegistrarPagos ? (
            <form onSubmit={handleSubmitPago}>
              <div className="catalog-form">
                <select className="select" aria-label={etiquetaContraparte} value={pagoContraparteId} onChange={(e) => { setPagoContraparteId(e.target.value); setMontosAplicados({}); }} required>
                  <option value="">Selecciona {etiquetaContraparte.toLowerCase()}</option>
                  {contrapartes.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                </select>
                <SelectCuentaBancaria etiqueta="Cuenta bancaria" valor={pagoCuentaBancariaId} onChange={setPagoCuentaBancariaId} cuentasBancarias={cuentasBancarias} />
                <input type="date" className="input" aria-label="Fecha de pago" value={pagoFecha} onChange={(e) => setPagoFecha(e.target.value)} required />
                <input className="input" placeholder="Método de pago (Efectivo, Transferencia...)" aria-label="Método de pago" value={pagoMetodo} onChange={(e) => setPagoMetodo(e.target.value)} required />
                <input className="input" placeholder="Referencia bancaria (opcional)" aria-label="Referencia bancaria" value={pagoReferencia} onChange={(e) => setPagoReferencia(e.target.value)} />
              </div>

              {pagoContraparteId !== "" && (
                <div className="table-wrap">
                  <div className="table-scroll">
                    <table className="data-table">
                      <thead>
                        <tr><th>Factura</th><th className="numeric">Saldo pendiente</th><th className="numeric">Monto a aplicar</th></tr>
                      </thead>
                      <tbody>
                        {facturasDeLaContraparte.length === 0 ? (
                          <tr><td colSpan="3">No hay facturas con saldo pendiente para esta selección.</td></tr>
                        ) : (
                          facturasDeLaContraparte.map((f) => (
                            <tr key={f.id}>
                              <td>{f.numero}</td>
                              <td className="numeric">{formatoMoneda.format(f.saldoPendiente)}</td>
                              <td className="numeric">
                                <input
                                  type="number"
                                  className="input input-money"
                                  aria-label={`Monto a aplicar a ${f.numero}`}
                                  min="0"
                                  max={f.saldoPendiente}
                                  step="0.01"
                                  placeholder="0.00"
                                  value={montosAplicados[f.id] || ""}
                                  onChange={(e) => actualizarMontoAplicado(f.id, e.target.value)}
                                />
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {errorPago && <p className="error-chip">{errorPago}</p>}

              <button type="submit" disabled={!puedeEnviarPago} className="btn btn-primary">
                {enviandoPago ? "Registrando..." : "Registrar pago"}
              </button>
            </form>
          ) : (
            <p>Tu perfil no puede registrar cobros ni pagos.</p>
          )}

          <h3>Pagos registrados</h3>
          <div className="table-wrap">
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr><th>{etiquetaContraparte}</th><th>Fecha</th><th className="numeric">Monto</th><th>Método</th><th>Aplicado a</th></tr>
                </thead>
                <tbody>
                  {pagos.length === 0 ? (
                    <tr><td colSpan="5">No hay pagos registrados todavía.</td></tr>
                  ) : (
                    pagos.map((p) => (
                      <tr key={p.id}>
                        <td>{p[campoContraparteNombre]}</td>
                        <td>{p.fecha}</td>
                        <td className="numeric">{formatoMoneda.format(p.montoTotal)}</td>
                        <td>{p.metodoPago}</td>
                        <td>{p.aplicaciones.map((a) => `${a.numero} (${formatoMoneda.format(a.montoAplicado)})`).join(", ")}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default GestionCuentasPorCobrarPagar;
