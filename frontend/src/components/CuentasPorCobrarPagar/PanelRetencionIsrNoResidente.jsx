import { formatoMoneda } from "../../utils/formato";
import { LEYENDA_REFERENCIAL } from "../../utils/retenciones";
import MarcoPanelRetencion from "./MarcoPanelRetencion";

function SelectorTipoRenta({ aplicacion, facturas, tiposRenta, tipos, porDefecto, onTipo }) {
  const numero = facturas.find((f) => f.id === aplicacion.documentoId)?.numero;

  return (
    <select className="select" aria-label={`Tipo de renta de ${numero}`} value={tipos[aplicacion.documentoId] || porDefecto} onChange={(e) => onTipo(aplicacion.documentoId, e.target.value)}>
      <option value="">Selecciona tipo de renta</option>
      {tiposRenta.map((t) => (
        <option key={t.id} value={t.codigo}>{`${t.nombre} (${t.tasaBase} %)`}</option>
      ))}
    </select>
  );
}

function DetallePrevia({ previa }) {
  return (
    <>
      {previa.lineas.map((linea) => (
        <div key={linea.documentoId}>
          <p>{`${linea.tipoRentaNombre} · ${linea.tasa} %`}</p>
          <p>{`Monto ${formatoMoneda.format(linea.montoOrigen)} ${linea.monedaCodigo} · TC ${linea.tipoCambio} del ${linea.tipoCambioFecha}`}</p>
          <p>{`Base: ${formatoMoneda.format(linea.baseGtq)} · Retención: ${formatoMoneda.format(linea.retencionGtq)}`}</p>
        </div>
      ))}
      <p>Total base: {formatoMoneda.format(previa.totalBaseGtq)}</p>
      <p>Total retención: {formatoMoneda.format(previa.totalRetencionGtq)}</p>
      <p>Neto al banco: {formatoMoneda.format(previa.netoBancoGtq)}</p>
      {previa.advertencias?.map((texto) => (
        <p key={texto} className="warning-chip">{texto}</p>
      ))}
      <small className="nota-fiscal">{LEYENDA_REFERENCIAL}</small>
    </>
  );
}

function PanelRetencionIsrNoResidente({ estado, aplicaciones, facturas, tiposRenta, tipos, porDefecto, onTipo }) {
  const { previa } = estado;

  return (
    <MarcoPanelRetencion titulo="Retención de ISR a no residentes (art. 104)" estado={estado} siempre>
      {previa?.aplica === false && <p>{previa.motivoNoAplica}</p>}
      {previa?.aplica !== false &&
        aplicaciones.filter((a) => facturas.find((f) => f.id === a.documentoId)?.tipoDocumento !== "NotaCredito").map((a) => (
          <SelectorTipoRenta key={a.documentoId} aplicacion={a} facturas={facturas} tiposRenta={tiposRenta} tipos={tipos} porDefecto={porDefecto} onTipo={onTipo} />
        ))}
      {previa?.aplica && <DetallePrevia previa={previa} />}
    </MarcoPanelRetencion>
  );
}

export default PanelRetencionIsrNoResidente;
