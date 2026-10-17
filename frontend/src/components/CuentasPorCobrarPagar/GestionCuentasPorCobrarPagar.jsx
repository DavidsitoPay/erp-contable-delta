import { useEffect, useState } from "react";
import { centrosCostoApi, contrapartesApi, cuentasApi, cuentasBancariasApi, parametrosRetencionApi, periodosApi } from "../../services/api";
import { esMonedaExtranjera, monedaDe } from "../../utils/cambio";
import { cuentasHoja } from "../../utils/cuentas";
import { hoyIso, monto } from "../../utils/formato";
import { perfilActual, puedeGestionarTesoreria } from "../../utils/perfiles";
import { aplicacionesConRetencion, aplicacionesConTipoRenta, hayErrorRetencionIva, resumenCobro, tipoRentaInicial } from "../../utils/retenciones";
import FormularioMotivo from "../FormularioMotivo";
import ResolverAjusteDialog from "../Retenciones/ResolverAjusteDialog";
import SelectCuentaBancaria from "../Tesoreria/SelectCuentaBancaria";
import FormularioFactura from "./FormularioFactura";
import PanelLiquidacion from "./PanelLiquidacion";
import PanelRetencionIsrNoResidente from "./PanelRetencionIsrNoResidente";
import ResumenCobro from "./ResumenCobro";
import ResumenLiquidacion from "./ResumenLiquidacion";
import TablaAplicaciones from "./TablaAplicaciones";
import TablaFacturas from "./TablaFacturas";
import TablaPagos from "./TablaPagos";
import { useLiquidacionPago } from "./useLiquidacionPago";
import { useRetencionPreviaPago } from "./useRetencionPreviaPago";
import { useRetencionesCobro } from "./useRetencionesCobro";

function conservarTiposRentaVigentes(seleccion, tipos) {
  return Object.fromEntries(Object.entries(seleccion).filter(([, codigo]) => tipos.some((t) => t.codigo === codigo)));
}

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
  const [advertenciasPago, setAdvertenciasPago] = useState([]);
  const [enviandoPago, setEnviandoPago] = useState(false);
  const [pagoContraparteId, setPagoContraparteId] = useState("");
  const [pagoCuentaBancariaId, setPagoCuentaBancariaId] = useState("");
  const [pagoFecha, setPagoFecha] = useState(hoyIso());
  const [pagoMetodo, setPagoMetodo] = useState("");
  const [pagoReferencia, setPagoReferencia] = useState("");
  const [montosAplicados, setMontosAplicados] = useState({}); // documentoId -> string
  const [soloPorRevisar, setSoloPorRevisar] = useState(false);
  const [revisionDe, setRevisionDe] = useState(null);
  const [ajusteDe, setAjusteDe] = useState(null);
  const [tiposRenta, setTiposRenta] = useState([]);
  const [tiposRentaPago, setTiposRentaPago] = useState({}); // documentoId -> código
  const retencionesCobro = useRetencionesCobro(api);

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

  useEffect(() => {
    if (!puedeRegistrarPagos || !config.retencionIsr) return;
    void parametrosRetencionApi.listar({ concepto: "ISR_NO_RESIDENTE", vigenteEn: pagoFecha || hoyIso() })
      .then(({ data }) => {
        setTiposRenta(data);
        setTiposRentaPago((p) => conservarTiposRentaVigentes(p, data));
      })
      .catch(() => {
        setTiposRenta([]);
        setTiposRentaPago({});
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagoFecha]);

  const conRetencionIsr = Boolean(config.retencionIsr);
  const conRetencionIva = Boolean(config.retencionIvaCobro);
  const hojas = cuentasHoja(cuentas);
  const cuentasControl = hojas.filter(filtroCuentaControl);
  const periodosAbiertos = periodos.filter((p) => p.estado === "Abierto");
  const facturasVisibles = soloPorRevisar ? facturas.filter((f) => f.revisionRetencion === "PENDIENTE") : facturas;

  const facturasDeLaContraparte = facturas.filter(
    (f) => String(f[campoContraparteId]) === String(pagoContraparteId) && f.estado === "Vigente" && f.saldoPendiente > 0
  );
  const clienteAgente = Boolean(contrapartes.find((c) => String(c.id) === String(pagoContraparteId))?.esAgenteRetencionIva);
  const proveedor = contrapartes.find((c) => String(c.id) === String(pagoContraparteId));
  const proveedorNr = conRetencionIsr && proveedor?.esResidente === false;

  function actualizarMontoAplicado(documentoId, valor) {
    setMontosAplicados((prev) => ({ ...prev, [documentoId]: valor }));
    const factura = facturasDeLaContraparte.find((f) => f.id === documentoId);
    if (conRetencionIva && monto(valor) > 0 && !esMonedaExtranjera(monedaDe(factura))) {
      void retencionesCobro.solicitarSugerencia(documentoId);
    }
  }

  function seleccionarContraparte(valor) {
    setPagoContraparteId(valor);
    setMontosAplicados({});
    setTiposRentaPago({});
    retencionesCobro.reiniciar();
    liquidacion.reiniciar();
  }

  const aplicacionesBase = facturasDeLaContraparte
    .map((f) => ({ documentoId: f.id, montoAplicado: monto(montosAplicados[f.id]) }))
    .filter((a) => a.montoAplicado > 0);
  const aplicacionesConMonto = conRetencionIva
    ? aplicacionesConRetencion(aplicacionesBase, retencionesCobro.valores, retencionesCobro.sugerencias, clienteAgente)
    : aplicacionesBase;
  const retencionInvalida =
    conRetencionIva && hayErrorRetencionIva(aplicacionesBase, retencionesCobro.valores, retencionesCobro.sugerencias, clienteAgente);
  const retencionIva = conRetencionIva
    ? { valores: retencionesCobro.valores, sugerencias: retencionesCobro.sugerencias, clienteAgente, onCambio: retencionesCobro.cambiar }
    : null;

  const resumenPago = resumenCobro(aplicacionesConMonto);
  const cuentaPago = cuentasBancarias.find((c) => String(c.id) === pagoCuentaBancariaId);
  const liquidacion = useLiquidacionPago({
    cuenta: cuentaPago,
    fecha: pagoFecha,
    facturas: facturasDeLaContraparte,
    montosAplicados,
    esCobro: Boolean(config.esCobro),
  });

  const aplicacionesPago = proveedorNr
    ? aplicacionesConTipoRenta(liquidacion.conEfectivo(aplicacionesConMonto), tiposRentaPago, tipoRentaInicial(proveedor))
    : liquidacion.conEfectivo(aplicacionesConMonto);
  const previaNr = useRetencionPreviaPago({ api, activo: proveedorNr, proveedorId: pagoContraparteId, fecha: pagoFecha, aplicaciones: aplicacionesPago });

  const puedeEnviarPago =
    !enviandoPago &&
    pagoContraparteId !== "" &&
    pagoCuentaBancariaId !== "" &&
    pagoMetodo.trim() !== "" &&
    aplicacionesConMonto.length > 0 &&
    resumenPago.neto > 0 &&
    !retencionInvalida &&
    liquidacion.listo &&
    !(proveedorNr && (previaNr.bloqueada || previaNr.error !== ""));

  async function resolverRevision(motivo) {
    await api.resolverRevisionRetencion(revisionDe.id, motivo);
    setRevisionDe(null);
    await cargarFacturas();
  }

  async function handleSubmitPago(e) {
    e.preventDefault();
    setErrorPago("");
    setAdvertenciasPago([]);
    setEnviandoPago(true);
    try {
      const { data } = await api.crearPago({
        [campoContraparteId]: Number(pagoContraparteId),
        cuentaBancariaId: Number(pagoCuentaBancariaId),
        fecha: pagoFecha,
        metodoPago: pagoMetodo.trim(),
        referenciaBancaria: pagoReferencia || null,
        aplicaciones: aplicacionesPago,
      });
      setAdvertenciasPago(data?.advertencias ?? []);
      setPagoMetodo("");
      setPagoReferencia("");
      setMontosAplicados({});
      setTiposRentaPago({});
      retencionesCobro.reiniciar();
      liquidacion.reiniciar();
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
          {conRetencionIsr && (
            <label>
              <input type="checkbox" checked={soloPorRevisar} onChange={(e) => setSoloPorRevisar(e.target.checked)} />
              <span>Solo facturas por revisar</span>
            </label>
          )}
          {revisionDe && (
            <FormularioMotivo
              titulo={`Motivo de la revisión de ${revisionDe.numero}`}
              etiquetaConfirmar="Resolver revisión"
              mensajeFallo="No se pudo resolver la revisión."
              onConfirmar={resolverRevision}
              onCancelar={() => setRevisionDe(null)}
            />
          )}
          {ajusteDe && (
            <ResolverAjusteDialog
              documentoId={ajusteDe.id}
              onResuelto={async () => { setAjusteDe(null); await cargarFacturas(); }}
              onCancelar={() => setAjusteDe(null)}
            />
          )}
          <TablaFacturas
            facturas={facturasVisibles}
            etiquetaContraparte={etiquetaContraparte}
            campoContraparteNombre={campoContraparteNombre}
            conRetencion={conRetencionIsr}
            onResolverRevision={setRevisionDe}
            onResolverAjuste={setAjusteDe}
          />
        </>
      )}

      {subTab === "pagos" && (
        <>
          {puedeRegistrarPagos ? (
            <form onSubmit={handleSubmitPago}>
              <div className="catalog-form">
                <select className="select" aria-label={etiquetaContraparte} value={pagoContraparteId} onChange={(e) => seleccionarContraparte(e.target.value)} required>
                  <option value="">Selecciona {etiquetaContraparte.toLowerCase()}</option>
                  {contrapartes.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                </select>
                <SelectCuentaBancaria etiqueta="Cuenta bancaria" valor={pagoCuentaBancariaId} onChange={setPagoCuentaBancariaId} cuentasBancarias={cuentasBancarias} />
                <input type="date" className="input" aria-label="Fecha de pago" value={pagoFecha} onChange={(e) => setPagoFecha(e.target.value)} required />
                <input className="input" placeholder="Método de pago (Efectivo, Transferencia...)" aria-label="Método de pago" value={pagoMetodo} onChange={(e) => setPagoMetodo(e.target.value)} required />
                <input className="input" placeholder="Referencia bancaria (opcional)" aria-label="Referencia bancaria" value={pagoReferencia} onChange={(e) => setPagoReferencia(e.target.value)} />
              </div>

              <PanelLiquidacion liquidacion={liquidacion} fecha={pagoFecha} />

              {pagoContraparteId !== "" && (
                <TablaAplicaciones
                  facturas={facturasDeLaContraparte}
                  montosAplicados={montosAplicados}
                  onMonto={actualizarMontoAplicado}
                  retencionIva={retencionIva}
                  liquidacion={liquidacion.tabla}
                />
              )}

              {proveedorNr && aplicacionesConMonto.length > 0 && (
                <PanelRetencionIsrNoResidente
                  estado={previaNr}
                  aplicaciones={aplicacionesConMonto}
                  facturas={facturasDeLaContraparte}
                  tiposRenta={tiposRenta}
                  tipos={tiposRentaPago}
                  porDefecto={tipoRentaInicial(proveedor)}
                  onTipo={(id, valor) => setTiposRentaPago((p) => ({ ...p, [id]: valor }))}
                />
              )}

              {conRetencionIva && aplicacionesConMonto.length > 0 && <ResumenCobro resumen={resumenPago} />}
              <ResumenLiquidacion resumen={liquidacion.resumen} moneda={liquidacion.moneda} />

              {errorPago && <p className="error-chip">{errorPago}</p>}
              {advertenciasPago.map((texto) => (
                <p key={texto} className="warning-chip">{texto}</p>
              ))}

              <button type="submit" disabled={!puedeEnviarPago} className="btn btn-primary">
                {enviandoPago ? "Registrando..." : "Registrar pago"}
              </button>
            </form>
          ) : (
            <p>Tu perfil no puede registrar cobros ni pagos.</p>
          )}

          <h3>Pagos registrados</h3>
          <TablaPagos pagos={pagos} etiquetaContraparte={etiquetaContraparte} campoContraparteNombre={campoContraparteNombre} conNeto={conRetencionIva} />
        </>
      )}
    </div>
  );
}

export default GestionCuentasPorCobrarPagar;
