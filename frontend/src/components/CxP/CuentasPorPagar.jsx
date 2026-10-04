import { useEffect, useState } from "react";
import { centrosCostoApi, contrapartesApi, cuentasApi, cxpApi, periodosApi } from "../../services/api";

const TIPOS_DOCUMENTO = ["Factura", "NotaCredito", "NotaDebito"];

function hoyIso() {
  return new Date().toISOString().slice(0, 10);
}

function lineaVacia() {
  return { id: crypto.randomUUID(), descripcion: "", cantidad: "1", precioUnitario: "", porcentajeImpuesto: "0", centroCostoId: "", cuentaContableId: "" };
}

const formoFacturaVacio = {
  numero: "",
  tipoDocumento: "Factura",
  proveedorId: "",
  fecha: hoyIso(),
  fechaVencimiento: hoyIso(),
  periodoId: "",
  cuentaControlId: "",
};

const formatoMoneda = new Intl.NumberFormat("es-GT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

function monto(valor) {
  const n = Number.parseFloat(valor);
  return Number.isFinite(n) ? n : 0;
}

function CuentasPorPagar() {
  const [subTab, setSubTab] = useState("facturas");

  const [proveedores, setProveedores] = useState([]);
  const [cuentas, setCuentas] = useState([]);
  const [centros, setCentros] = useState([]);
  const [periodos, setPeriodos] = useState([]);
  const [facturas, setFacturas] = useState([]);
  const [pagos, setPagos] = useState([]);

  const [errorFactura, setErrorFactura] = useState("");
  const [enviandoFactura, setEnviandoFactura] = useState(false);
  const [formFactura, setFormFactura] = useState(formoFacturaVacio);
  const [lineas, setLineas] = useState([lineaVacia()]);

  const [errorPago, setErrorPago] = useState("");
  const [enviandoPago, setEnviandoPago] = useState(false);
  const [pagoProveedorId, setPagoProveedorId] = useState("");
  const [pagoFecha, setPagoFecha] = useState(hoyIso());
  const [pagoMetodo, setPagoMetodo] = useState("");
  const [pagoReferencia, setPagoReferencia] = useState("");
  const [montosAplicados, setMontosAplicados] = useState({}); // documentoId -> string

  async function cargarFacturas() {
    const { data } = await cxpApi.listarFacturas();
    setFacturas(data);
  }

  async function cargarPagos() {
    const { data } = await cxpApi.listarPagos();
    setPagos(data);
  }

  useEffect(() => {
    contrapartesApi.listar("Proveedor").then(({ data }) => setProveedores(data));
    cuentasApi.listar(true).then(({ data }) => setCuentas(data));
    centrosCostoApi.listar().then(({ data }) => setCentros(data));
    periodosApi.listar().then(({ data }) => setPeriodos(data));
    void cargarFacturas();
    void cargarPagos();
  }, []);

  // Misma regla que RegistrarAsiento: solo cuentas "hoja" reciben movimientos.
  const idsConHijos = new Set(cuentas.filter((c) => c.cuentaPadreId).map((c) => c.cuentaPadreId));
  const cuentasHoja = cuentas.filter((c) => c.activa && !idsConHijos.has(c.id));
  const cuentasControl = cuentasHoja.filter((c) => c.tipo === "Pasivo" && c.naturaleza === "Acreedora");
  const periodosAbiertos = periodos.filter((p) => p.estado === "Abierto");

  function actualizarLinea(index, campo, valor) {
    setLineas((prev) => prev.map((l, i) => (i === index ? { ...l, [campo]: valor } : l)));
  }

  function agregarLinea() {
    setLineas((prev) => [...prev, lineaVacia()]);
  }

  function quitarLinea(index) {
    setLineas((prev) => (prev.length <= 1 ? prev : prev.filter((_, i) => i !== index)));
  }

  const totalFactura = lineas.reduce((acc, l) => acc + monto(l.cantidad) * monto(l.precioUnitario) * (1 + monto(l.porcentajeImpuesto) / 100), 0);

  const puedeEnviarFactura =
    !enviandoFactura &&
    formFactura.numero.trim() !== "" &&
    formFactura.proveedorId !== "" &&
    formFactura.periodoId !== "" &&
    formFactura.cuentaControlId !== "" &&
    lineas.every((l) => l.cuentaContableId !== "" && monto(l.cantidad) > 0);

  async function handleSubmitFactura(e) {
    e.preventDefault();
    setErrorFactura("");
    setEnviandoFactura(true);
    try {
      const payload = {
        numero: formFactura.numero.trim(),
        tipoDocumento: formFactura.tipoDocumento,
        proveedorId: Number(formFactura.proveedorId),
        fecha: formFactura.fecha,
        fechaVencimiento: formFactura.fechaVencimiento,
        periodoId: Number(formFactura.periodoId),
        cuentaControlId: Number(formFactura.cuentaControlId),
        lineas: lineas.map((l) => ({
          descripcion: l.descripcion || null,
          cantidad: monto(l.cantidad),
          precioUnitario: monto(l.precioUnitario),
          porcentajeImpuesto: monto(l.porcentajeImpuesto),
          centroCostoId: l.centroCostoId ? Number(l.centroCostoId) : null,
          cuentaContableId: Number(l.cuentaContableId),
        })),
      };
      await cxpApi.crearFactura(payload);
      setFormFactura({ ...formoFacturaVacio, periodoId: formFactura.periodoId, cuentaControlId: formFactura.cuentaControlId });
      setLineas([lineaVacia()]);
      await cargarFacturas();
    } catch (err) {
      setErrorFactura(err.response?.data?.error || "No se pudo registrar la factura.");
    } finally {
      setEnviandoFactura(false);
    }
  }

  const facturasDelProveedor = facturas.filter(
    (f) => String(f.proveedorId) === String(pagoProveedorId) && f.estado === "Vigente" && f.saldoPendiente > 0
  );

  function actualizarMontoAplicado(documentoId, valor) {
    setMontosAplicados((prev) => ({ ...prev, [documentoId]: valor }));
  }

  const aplicacionesConMonto = facturasDelProveedor
    .map((f) => ({ documentoId: f.id, montoAplicado: monto(montosAplicados[f.id]) }))
    .filter((a) => a.montoAplicado > 0);

  const puedeEnviarPago = !enviandoPago && pagoProveedorId !== "" && pagoMetodo.trim() !== "" && aplicacionesConMonto.length > 0;

  async function handleSubmitPago(e) {
    e.preventDefault();
    setErrorPago("");
    setEnviandoPago(true);
    try {
      await cxpApi.crearPago({
        proveedorId: Number(pagoProveedorId),
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
      <h2>Cuentas por pagar</h2>

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
          <form onSubmit={handleSubmitFactura}>
            <div className="catalog-form">
              <input className="input" placeholder="Número" aria-label="Número de factura" value={formFactura.numero} onChange={(e) => setFormFactura({ ...formFactura, numero: e.target.value })} required />
              <select className="select" aria-label="Tipo de documento" value={formFactura.tipoDocumento} onChange={(e) => setFormFactura({ ...formFactura, tipoDocumento: e.target.value })}>
                {TIPOS_DOCUMENTO.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
              <select className="select" aria-label="Proveedor" value={formFactura.proveedorId} onChange={(e) => setFormFactura({ ...formFactura, proveedorId: e.target.value })} required>
                <option value="">Selecciona proveedor</option>
                {proveedores.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
              </select>
              <input type="date" className="input" aria-label="Fecha" value={formFactura.fecha} onChange={(e) => setFormFactura({ ...formFactura, fecha: e.target.value })} required />
              <input type="date" className="input" aria-label="Fecha de vencimiento" value={formFactura.fechaVencimiento} onChange={(e) => setFormFactura({ ...formFactura, fechaVencimiento: e.target.value })} required />
              <select className="select" aria-label="Periodo" value={formFactura.periodoId} onChange={(e) => setFormFactura({ ...formFactura, periodoId: e.target.value })} required>
                <option value="">Selecciona periodo</option>
                {periodosAbiertos.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
              </select>
              <select className="select" aria-label="Cuenta de control (Cuentas por pagar)" value={formFactura.cuentaControlId} onChange={(e) => setFormFactura({ ...formFactura, cuentaControlId: e.target.value })} required>
                <option value="">Cuenta de control (CxP)</option>
                {cuentasControl.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
              </select>
            </div>

            <div className="table-wrap">
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Descripción</th>
                      <th>Cantidad</th>
                      <th>Precio unitario</th>
                      <th>% Impuesto</th>
                      <th>Cuenta (gasto/activo)</th>
                      <th>Centro de costo</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {lineas.map((l, index) => (
                      <tr key={l.id}>
                        <td><input className="input" aria-label="Descripción de línea" value={l.descripcion} onChange={(e) => actualizarLinea(index, "descripcion", e.target.value)} /></td>
                        <td className="numeric"><input type="number" className="input input-money" aria-label="Cantidad" min="0" step="0.01" value={l.cantidad} onChange={(e) => actualizarLinea(index, "cantidad", e.target.value)} /></td>
                        <td className="numeric"><input type="number" className="input input-money" aria-label="Precio unitario" min="0" step="0.01" value={l.precioUnitario} onChange={(e) => actualizarLinea(index, "precioUnitario", e.target.value)} /></td>
                        <td className="numeric"><input type="number" className="input input-money" aria-label="Porcentaje de impuesto" min="0" step="0.01" value={l.porcentajeImpuesto} onChange={(e) => actualizarLinea(index, "porcentajeImpuesto", e.target.value)} /></td>
                        <td>
                          <select className="select" aria-label="Cuenta de la línea" value={l.cuentaContableId} onChange={(e) => actualizarLinea(index, "cuentaContableId", e.target.value)}>
                            <option value="">Selecciona cuenta</option>
                            {cuentasHoja.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
                          </select>
                        </td>
                        <td>
                          <select className="select" aria-label="Centro de costo de la línea" value={l.centroCostoId} onChange={(e) => actualizarLinea(index, "centroCostoId", e.target.value)}>
                            <option value="">(ninguno)</option>
                            {centros.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
                          </select>
                        </td>
                        <td>
                          <button type="button" className="btn btn-danger-outline btn-sm" onClick={() => quitarLinea(index)} disabled={lineas.length <= 1}>Quitar</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="lineas-toolbar">
              <button type="button" className="btn btn-outline btn-sm" onClick={agregarLinea}>+ Agregar línea</button>
            </div>

            <p>Total de la factura: {formatoMoneda.format(totalFactura)}</p>

            {errorFactura && <p className="error-chip">{errorFactura}</p>}

            <button type="submit" disabled={!puedeEnviarFactura} className="btn btn-primary">
              {enviandoFactura ? "Registrando..." : "Registrar factura"}
            </button>
          </form>

          <h3>Facturas registradas</h3>
          <div className="table-wrap">
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Número</th><th>Proveedor</th><th>Fecha</th><th>Vencimiento</th>
                    <th className="numeric">Monto total</th><th className="numeric">Saldo pendiente</th><th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {facturas.length === 0 ? (
                    <tr><td colSpan="7">No hay facturas registradas todavía.</td></tr>
                  ) : (
                    facturas.map((f) => (
                      <tr key={f.id}>
                        <td>{f.numero}</td>
                        <td>{f.proveedorNombre}</td>
                        <td>{f.fecha}</td>
                        <td>{f.fechaVencimiento}</td>
                        <td className="numeric">{formatoMoneda.format(f.montoTotal)}</td>
                        <td className="numeric">{formatoMoneda.format(f.saldoPendiente)}</td>
                        <td>{f.estado}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {subTab === "pagos" && (
        <>
          <form onSubmit={handleSubmitPago}>
            <div className="catalog-form">
              <select className="select" aria-label="Proveedor" value={pagoProveedorId} onChange={(e) => { setPagoProveedorId(e.target.value); setMontosAplicados({}); }} required>
                <option value="">Selecciona proveedor</option>
                {proveedores.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
              </select>
              <input type="date" className="input" aria-label="Fecha de pago" value={pagoFecha} onChange={(e) => setPagoFecha(e.target.value)} required />
              <input className="input" placeholder="Método de pago (Efectivo, Transferencia...)" aria-label="Método de pago" value={pagoMetodo} onChange={(e) => setPagoMetodo(e.target.value)} required />
              <input className="input" placeholder="Referencia bancaria (opcional)" aria-label="Referencia bancaria" value={pagoReferencia} onChange={(e) => setPagoReferencia(e.target.value)} />
            </div>

            {pagoProveedorId !== "" && (
              <div className="table-wrap">
                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      <tr><th>Factura</th><th className="numeric">Saldo pendiente</th><th className="numeric">Monto a aplicar</th></tr>
                    </thead>
                    <tbody>
                      {facturasDelProveedor.length === 0 ? (
                        <tr><td colSpan="3">Este proveedor no tiene facturas con saldo pendiente.</td></tr>
                      ) : (
                        facturasDelProveedor.map((f) => (
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

          <h3>Pagos registrados</h3>
          <div className="table-wrap">
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr><th>Proveedor</th><th>Fecha</th><th className="numeric">Monto</th><th>Método</th><th>Aplicado a</th></tr>
                </thead>
                <tbody>
                  {pagos.length === 0 ? (
                    <tr><td colSpan="5">No hay pagos registrados todavía.</td></tr>
                  ) : (
                    pagos.map((p) => (
                      <tr key={p.id}>
                        <td>{p.proveedorNombre}</td>
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

export default CuentasPorPagar;
