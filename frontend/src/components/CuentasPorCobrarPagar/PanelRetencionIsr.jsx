import { formatoMoneda, monto } from "../../utils/formato";
import { LEYENDA_REFERENCIAL, ajusteValido, descripcionEscala, netoAPagar, permiteAjuste } from "../../utils/retenciones";
import CampoFormulario from "../CampoFormulario";
import MarcoPanelRetencion from "./MarcoPanelRetencion";

function DetallePrevia({ previa, total, ajusteActivo, ajuste }) {
  const retencion = ajusteActivo ? monto(ajuste.monto) : previa.retencion;

  return (
    <>
      {previa.estado === "PendienteRevision" && (
        <>
          <p className="warning-chip">{previa.motivo}</p>
          <small className="nota-fiscal">Se puede registrar sin retención; la factura quedará marcada para revisión.</small>
        </>
      )}
      {previa.estado === "NoAplica" && <p>{`No se retiene ISR: ${previa.motivo}`}</p>}
      {previa.estado === "Retenida" && (
        <>
          <p>Base sin IVA: {formatoMoneda.format(previa.base)}</p>
          <p>Tasa: {descripcionEscala(previa)}</p>
        </>
      )}
      <p>Retención de ISR: {formatoMoneda.format(retencion)}</p>
      <p>Neto a pagar al proveedor: {formatoMoneda.format(netoAPagar(previa, ajusteActivo, ajuste, total))}</p>
      {previa.advertencias?.map((texto) => (
        <p key={texto} className="warning-chip">{texto}</p>
      ))}
      {previa.referencial && <small className="nota-fiscal">{LEYENDA_REFERENCIAL}</small>}
    </>
  );
}

function CamposAjuste({ ajuste, total, onAjuste }) {
  const cambiar = (campo) => (e) => onAjuste({ ...ajuste, [campo]: e.target.value });

  return (
    <>
      <label>
        <input type="checkbox" checked={ajuste.activo} onChange={(e) => onAjuste({ ...ajuste, activo: e.target.checked })} />
        <span>Ajustar retención</span>
      </label>
      {ajuste.activo && (
        <>
          <div className="catalog-form">
            <CampoFormulario id="isr-ajuste-monto" etiqueta="Monto de retención ajustado">
              <input id="isr-ajuste-monto" type="number" className="input input-money" min="0" max={total} step="0.01" value={ajuste.monto} onChange={cambiar("monto")} />
            </CampoFormulario>
            <CampoFormulario id="isr-ajuste-motivo" etiqueta="Motivo del ajuste">
              <input id="isr-ajuste-motivo" className="input" maxLength={300} value={ajuste.motivo} onChange={cambiar("motivo")} />
            </CampoFormulario>
          </div>
          {!ajusteValido(ajuste, total) && (
            <p className="warning-chip">Indique un monto desde 0 y menor al total de la factura, y el motivo del ajuste.</p>
          )}
        </>
      )}
    </>
  );
}

function PanelRetencionIsr({ estado, total, ajuste, onAjuste }) {
  const { previa } = estado;

  return (
    <MarcoPanelRetencion titulo="Retención de ISR" estado={estado}>
      {previa && <DetallePrevia previa={previa} total={total} ajusteActivo={ajuste.activo && permiteAjuste(previa)} ajuste={ajuste} />}
      {permiteAjuste(previa) && <CamposAjuste ajuste={ajuste} total={total} onAjuste={onAjuste} />}
    </MarcoPanelRetencion>
  );
}

export default PanelRetencionIsr;
