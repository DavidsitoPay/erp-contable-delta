import { AVISO_DTE, uuidValido } from "../../utils/fiscal";
import CampoFormulario from "../CampoFormulario";

const TITULO_OBLIGATORIO = "Datos del DTE";
const TITULO_OPCIONAL = "Datos del DTE del proveedor (obligatorios para tomar crédito fiscal)";

function DatosDte({ valor, onCambio, obligatorio }) {
  const cambiar = (campo) => (e) => onCambio({ ...valor, [campo]: e.target.value });
  const uuidInvalido = valor.dteUuid.trim() !== "" && !uuidValido(valor.dteUuid);

  return (
    <fieldset className="dte-datos">
      <legend>{obligatorio ? TITULO_OBLIGATORIO : TITULO_OPCIONAL}</legend>
      <p className="warning-chip">{AVISO_DTE}</p>
      <div className="catalog-form">
        <CampoFormulario id="dte-uuid" etiqueta="UUID de autorización">
          <input id="dte-uuid" className="input" value={valor.dteUuid} onChange={cambiar("dteUuid")} aria-invalid={uuidInvalido} required={obligatorio} />
        </CampoFormulario>
        <CampoFormulario id="dte-serie" etiqueta="Serie">
          <input id="dte-serie" className="input" maxLength={20} value={valor.dteSerie} onChange={cambiar("dteSerie")} required={obligatorio} />
        </CampoFormulario>
        <CampoFormulario id="dte-numero" etiqueta="Número">
          <input id="dte-numero" className="input" maxLength={20} value={valor.dteNumero} onChange={cambiar("dteNumero")} required={obligatorio} />
        </CampoFormulario>
        <CampoFormulario id="dte-fecha" etiqueta="Fecha y hora de certificación">
          <input id="dte-fecha" type="datetime-local" className="input" value={valor.dteFechaCertificacion} onChange={cambiar("dteFechaCertificacion")} required={obligatorio} />
        </CampoFormulario>
      </div>
      {uuidInvalido && <p className="error-chip">El UUID del DTE no tiene un formato válido.</p>}
    </fieldset>
  );
}

export default DatosDte;
