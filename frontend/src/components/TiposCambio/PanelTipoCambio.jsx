import { useState } from "react";
import { AVISO_BANGUAT, AVISO_SIN_PERMISO_TASA, esMonedaExtranjera, etiquetaFuente, formatoTasa } from "../../utils/cambio";
import { perfilActual, puedeGestionarTesoreria } from "../../utils/perfiles";
import FormularioTasaManual from "./FormularioTasaManual";

function TasaVigente({ tipoCambio, heredado }) {
  return (
    <>
      <p>{`Tipo de cambio: ${formatoTasa(tipoCambio.tasa)} (${etiquetaFuente(tipoCambio)})`}</p>
      {tipoCambio.arrastrado && <span className="badge badge-warning">Arrastrado</span>}
      {tipoCambio.arrastrado && tipoCambio.fechaReferencia && <small className="nota-fiscal">{`Referencia del ${tipoCambio.fechaReferencia}`}</small>}
      {heredado && <small className="nota-fiscal">Heredado de la factura de origen.</small>}
    </>
  );
}

function SinTasa({ estado, moneda, fecha, puedeCapturar }) {
  const [capturando, setCapturando] = useState(false);
  const { propuesta, mensaje, recargar } = estado;

  return (
    <>
      <p className="warning-chip">{mensaje || "Banguat no respondió; capture la tasa manual o reintente."}</p>
      {propuesta && <p>{`Último valor guardado: ${formatoTasa(propuesta.tasa)} del ${propuesta.fecha}`}</p>}
      <button type="button" className="btn btn-outline btn-sm" onClick={recargar}>Reintentar</button>
      {puedeCapturar && !capturando && (
        <button type="button" className="btn btn-outline btn-sm" onClick={() => setCapturando(true)}>
          {propuesta ? "Usar este valor" : "Capturar tasa manual"}
        </button>
      )}
      {!puedeCapturar && <p>{AVISO_SIN_PERMISO_TASA}</p>}
      {capturando && (
        <FormularioTasaManual
          fijo
          enLinea
          monedaInicial={moneda}
          fechaInicial={fecha}
          tasaInicial={propuesta?.tasa ?? ""}
          onCapturada={async () => {
            setCapturando(false);
            recargar();
          }}
          onCancelar={() => setCapturando(false)}
        />
      )}
    </>
  );
}

function PanelTipoCambio({ estado, moneda, fecha, titulo }) {
  const [puedeCapturar] = useState(() => puedeGestionarTesoreria(perfilActual()));
  if (!esMonedaExtranjera(moneda)) return null;

  const { cargando, error, disponible, tipoCambio } = estado;
  const sinTasa = estado.consultado && !disponible;

  return (
    <fieldset className="dte-datos">
      <legend>{titulo ?? `Tipo de cambio ${moneda}`}</legend>
      {cargando && <p>Consultando tipo de cambio...</p>}
      {error && <p className="error-chip">{error}</p>}
      {disponible && tipoCambio && <TasaVigente tipoCambio={tipoCambio} heredado={estado.heredado} />}
      {sinTasa && <SinTasa estado={estado} moneda={moneda} fecha={fecha} puedeCapturar={puedeCapturar} />}
      <small className="nota-fiscal">{AVISO_BANGUAT}</small>
    </fieldset>
  );
}

export default PanelTipoCambio;
