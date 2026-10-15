import { useState } from "react";
import { reportesSaldosApi } from "../../services/api";
import { MONEDAS_DOCUMENTO, formatoTasa } from "../../utils/cambio";
import { descargarCsv } from "../../utils/csv";
import { formatoImporte, formatoMoneda, hoyIso, mensajeError } from "../../utils/formato";
import { ETIQUETAS_TRAMO, TRAMOS, csvSaldos, nombreArchivoSaldos } from "../../utils/saldosMoneda";
import { BotonesConsulta, EstadoConsulta } from "../AccionesConsulta";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS_FILAS = ["Número", "Cliente o proveedor", "Fecha", "Vencimiento", "Moneda", "Saldo original", "Tipo de cambio", "Saldo Q", "Días vencido", "Tramo"];
const ENCABEZADOS_TOTALES = ["Moneda", "Saldo original", "Saldo Q", ...TRAMOS.map((t) => ETIQUETAS_TRAMO[t])];
const ENCABEZADOS_CONCILIACION = ["Cuenta", "Total del reporte", "Saldo del mayor", "Revaluación vigente", "Diferencia"];

function TablaFilas({ filas }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS_FILAS}>
      {filas.length === 0 ? (
        <tr><td colSpan={ENCABEZADOS_FILAS.length + 1}>No hay saldos pendientes al corte.</td></tr>
      ) : (
        filas.map((f) => (
          <tr key={f.documentoId}>
            <td>{f.numero}</td>
            <td>{f.tercero}</td>
            <td>{f.fecha}</td>
            <td>{f.fechaVencimiento}</td>
            <td>{f.moneda}</td>
            <td className="numeric">{formatoImporte(f.saldoOriginal, f.moneda)}</td>
            <td className="numeric">{formatoTasa(f.tipoCambioLibros)}</td>
            <td className="numeric">{formatoMoneda.format(f.saldoGtq)}</td>
            <td className="numeric">{f.diasVencido}</td>
            <td>{ETIQUETAS_TRAMO[f.tramo] ?? f.tramo}</td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

function TablaTotales({ totales }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS_TOTALES}>
      {totales.map((t) => (
        <tr key={t.moneda}>
          <td>{t.moneda}</td>
          <td className="numeric">{formatoImporte(t.saldoOriginal, t.moneda)}</td>
          <td className="numeric">{formatoMoneda.format(t.saldoGtq)}</td>
          {TRAMOS.map((tramo) => (
            <td key={tramo} className="numeric">{formatoImporte(t.porTramo[tramo], t.moneda)}</td>
          ))}
        </tr>
      ))}
    </TablaCatalogo>
  );
}

function TablaConciliacion({ conciliacion }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS_CONCILIACION}>
      {conciliacion.map((c) => (
        <tr key={c.cuentaId}>
          <td>{c.cuentaCodigo}</td>
          <td className="numeric">{formatoMoneda.format(c.totalReporte)}</td>
          <td className="numeric">{formatoMoneda.format(c.saldoMayor)}</td>
          <td className="numeric">{formatoMoneda.format(c.revaluacionVigente)}</td>
          <td className="numeric">{formatoMoneda.format(c.diferencia)}</td>
        </tr>
      ))}
    </TablaCatalogo>
  );
}

function SaldosMoneda() {
  const [tipo, setTipo] = useState("CxC");
  const [moneda, setMoneda] = useState("");
  const [corte, setCorte] = useState(hoyIso);
  const [reporte, setReporte] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function handleConsultar(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      const { data } = await reportesSaldosApi.saldosAntiguedad({ tipo, corte, ...(moneda ? { moneda } : {}) });
      setReporte(data);
    } catch (err) {
      setReporte(null);
      setError(mensajeError(err, "No se pudo consultar el reporte de saldos."));
    } finally {
      setCargando(false);
    }
  }

  return (
    <div>
      <h2>Saldos y antigüedad en moneda extranjera</h2>

      <div className="lineas-toolbar">
        {["CxC", "CxP"].map((valor) => (
          <button key={valor} type="button" className={`btn btn-sm ${tipo === valor ? "btn-primary" : "btn-outline"}`} onClick={() => setTipo(valor)}>
            {valor}
          </button>
        ))}
      </div>

      <form className="catalog-form no-print" onSubmit={handleConsultar}>
        <select className="select" aria-label="Moneda del reporte" value={moneda} onChange={(e) => setMoneda(e.target.value)}>
          <option value="">Todas las monedas</option>
          {MONEDAS_DOCUMENTO.map((codigo) => <option key={codigo} value={codigo}>{codigo}</option>)}
        </select>
        <input type="date" className="input" aria-label="Fecha de corte" value={corte} onChange={(e) => setCorte(e.target.value)} required />
        <BotonesConsulta
          cargando={cargando}
          mostrarExportar={Boolean(reporte)}
          onExportar={() => descargarCsv(nombreArchivoSaldos(reporte.tipo, reporte.corte), csvSaldos(reporte))}
        />
      </form>

      <EstadoConsulta error={error} cargando={cargando} />
      {reporte && !cargando && (
        <>
          <p className="warning-chip">{reporte.leyenda}</p>
          <TablaFilas filas={reporte.filas} />
          <h3>Totales por moneda</h3>
          <TablaTotales totales={reporte.totales} />
          <p>Total en GTQ: {formatoMoneda.format(reporte.totalGtq)}</p>
          {reporte.conciliacion.length > 0 && (
            <>
              <h3>Conciliación con el libro mayor</h3>
              <TablaConciliacion conciliacion={reporte.conciliacion} />
            </>
          )}
        </>
      )}
    </div>
  );
}

export default SaldosMoneda;
