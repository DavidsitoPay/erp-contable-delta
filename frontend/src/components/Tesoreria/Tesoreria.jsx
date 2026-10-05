import { useState } from "react";
import ConciliacionesVista from "./Conciliaciones";
import CuentasBancarias from "./CuentasBancarias";
import Movimientos from "./Movimientos";
import { useCatalogosTesoreria } from "./useCatalogosTesoreria";

const VISTAS = [
  ["cuentas", "Cuentas bancarias"],
  ["movimientos", "Movimientos"],
  ["conciliaciones", "Conciliaciones"],
];

function Tesoreria() {
  const [subTab, setSubTab] = useState("cuentas");
  const { cuentasBancarias, cuentasContables, recargarCuentasBancarias } = useCatalogosTesoreria(subTab);

  return (
    <div>
      <h2>Tesorería</h2>

      <div className="lineas-toolbar">
        {VISTAS.map(([clave, etiqueta]) => (
          <button key={clave} type="button" className={`btn btn-sm ${subTab === clave ? "btn-primary" : "btn-outline"}`} onClick={() => setSubTab(clave)}>
            {etiqueta}
          </button>
        ))}
      </div>

      {subTab === "cuentas" && (
        <CuentasBancarias cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} recargar={recargarCuentasBancarias} />
      )}
      {subTab === "movimientos" && <Movimientos cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} />}
      {subTab === "conciliaciones" && <ConciliacionesVista cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} />}
    </div>
  );
}

export default Tesoreria;
