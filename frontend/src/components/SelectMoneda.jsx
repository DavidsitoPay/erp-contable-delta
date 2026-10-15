import { MONEDAS_DOCUMENTO } from "../utils/cambio";

function SelectMoneda({ valor, onChange, opciones = MONEDAS_DOCUMENTO, etiqueta = "Moneda", deshabilitado = false }) {
  return (
    <select className="select" aria-label={etiqueta} value={valor} onChange={(e) => onChange(e.target.value)} disabled={deshabilitado}>
      {opciones.map((codigo) => (
        <option key={codigo} value={codigo}>{codigo}</option>
      ))}
    </select>
  );
}

export default SelectMoneda;
