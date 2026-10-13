import { MESES } from "../utils/formato";

function CamposAnioMes({ anio, mes, onAnio, onMes }) {
  return (
    <>
      <input type="number" className="input" aria-label="Año" min="2000" max="2100" value={anio} onChange={(e) => onAnio(e.target.value)} required />
      <select className="select" aria-label="Mes" value={mes} onChange={(e) => onMes(e.target.value)}>
        {MESES.map((nombre, indice) => (
          <option key={nombre} value={indice + 1}>{nombre}</option>
        ))}
      </select>
    </>
  );
}

export default CamposAnioMes;
