import { etiquetaCuenta } from "../../utils/cuentas";

function SelectCuentaBancaria({ etiqueta, valor, onChange, cuentasBancarias, soloActivas = true, requerido = true }) {
  const opciones = soloActivas ? cuentasBancarias.filter((c) => c.activa) : cuentasBancarias;
  return (
    <select className="select" aria-label={etiqueta} value={valor} onChange={(e) => onChange(e.target.value)} required={requerido}>
      <option value="">{requerido ? `Selecciona ${etiqueta.toLowerCase()}` : "Todas las cuentas"}</option>
      {opciones.map((c) => <option key={c.id} value={c.id}>{etiquetaCuenta(c)}</option>)}
    </select>
  );
}

export default SelectCuentaBancaria;
