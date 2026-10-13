import { formatoMoneda, monto } from "../../utils/formato";
import { errorRetencionIva, retencionActiva } from "../../utils/retenciones";

function CamposRetencion({ numeroFactura, montoAplicado, valor, sugerencia, clienteAgente, onCambio }) {
  const cambiar = (campo) => (e) => onCambio({ [campo]: e.target.value });
  const sugerido = sugerencia?.sugerido;
  const difiere = sugerido !== undefined && valor.monto !== "" && monto(valor.monto) !== sugerido;
  const error = errorRetencionIva(valor, montoAplicado, sugerencia, clienteAgente);

  return (
    <>
      <input type="number" className="input input-money" aria-label={`IVA retenido en ${numeroFactura}`} min="0" step="0.01" placeholder="0.00" value={valor.monto} onChange={cambiar("monto")} />
      <button type="button" className="btn btn-outline btn-sm" aria-label={`Sugerir IVA retenido para ${numeroFactura}`} disabled={!Number.isFinite(sugerido) || sugerido <= 0} onClick={() => onCambio({ monto: String(sugerido) })}>
        Sugerir
      </button>
      {sugerencia?.motivo && <small className="nota-fiscal">{sugerencia.motivo}</small>}
      {sugerencia?.ivaRetenible !== undefined && <small className="nota-fiscal">{`IVA retenible de la factura: ${formatoMoneda.format(sugerencia.ivaRetenible)}`}</small>}
      {difiere && <p className="warning-chip">{`El monto difiere del sugerido (${formatoMoneda.format(sugerido)}); se registra el monto digitado.`}</p>}
      {!clienteAgente && (
        <label>
          <input type="checkbox" aria-label={`Confirmo la retención de IVA en ${numeroFactura} de un cliente no agente`} checked={valor.confirma} onChange={(e) => onCambio({ confirma: e.target.checked })} />
          <span>El cliente no está registrado como agente de retención; confirmo que retuvo IVA</span>
        </label>
      )}
      <input className="input" aria-label={`Número de constancia de ${numeroFactura}`} placeholder="Constancia (opcional)" maxLength={30} value={valor.constanciaNumero} onChange={cambiar("constanciaNumero")} />
      <input type="date" className="input" aria-label={`Fecha de constancia de ${numeroFactura}`} value={valor.constanciaFecha} onChange={cambiar("constanciaFecha")} />
      {error && <p className="error-chip">{error}</p>}
    </>
  );
}

function RetencionIvaCobro({ numeroFactura, montoAplicado, valor, sugerencia, clienteAgente, onCambio }) {
  const activa = retencionActiva(valor, clienteAgente);
  const yaRetenida = Boolean(sugerencia?.yaRetenida);

  return (
    <div className="catalog-form">
      <label>
        <input type="checkbox" aria-label={`El cliente retuvo IVA en ${numeroFactura}`} checked={activa} onChange={(e) => onCambio({ activa: e.target.checked })} />
        <span>El cliente retuvo IVA</span>
      </label>
      {sugerencia?.error && <p className="error-chip">{sugerencia.error}</p>}
      {yaRetenida && <p className="warning-chip">La factura ya tiene IVA retenido registrado.</p>}
      {activa && !yaRetenida && (
        <CamposRetencion numeroFactura={numeroFactura} montoAplicado={montoAplicado} valor={valor} sugerencia={sugerencia} clienteAgente={clienteAgente} onCambio={onCambio} />
      )}
    </div>
  );
}

export default RetencionIvaCobro;
