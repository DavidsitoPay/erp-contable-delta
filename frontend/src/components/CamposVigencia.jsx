import CampoFormulario from "./CampoFormulario";

function CamposVigencia({ prefijo, form, cambiar, desdeBloqueado = false }) {
  return (
    <>
      <CampoFormulario id={`${prefijo}-desde`} etiqueta="Vigente desde">
        <input id={`${prefijo}-desde`} type="date" className="input" value={form.vigenteDesde} onChange={cambiar("vigenteDesde")} disabled={desdeBloqueado} required />
      </CampoFormulario>
      <CampoFormulario id={`${prefijo}-hasta`} etiqueta="Vigente hasta (opcional)">
        <input id={`${prefijo}-hasta`} type="date" className="input" min={form.vigenteDesde} value={form.vigenteHasta} onChange={cambiar("vigenteHasta")} />
      </CampoFormulario>
    </>
  );
}

export default CamposVigencia;
