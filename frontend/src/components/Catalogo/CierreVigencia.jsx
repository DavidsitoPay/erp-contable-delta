import CampoFormulario from "../CampoFormulario";

function CierreVigencia({ id, cierre, cargando, onCambio, onConfirmar }) {
  const { item, fecha } = cierre;

  return (
    <form className="catalog-form" onSubmit={onConfirmar}>
      <CampoFormulario id={id} etiqueta={`Fecha final de vigencia de ${item.nombre}`}>
        <input id={id} type="date" className="input" min={item.vigenteDesde} value={fecha} onChange={(e) => onCambio({ ...cierre, fecha: e.target.value })} required />
      </CampoFormulario>
      <button type="submit" className="btn btn-primary" disabled={cargando}>
        Confirmar cierre
      </button>
      <button type="button" className="btn btn-outline" onClick={() => onCambio(null)}>
        Cancelar cierre
      </button>
    </form>
  );
}

export default CierreVigencia;
