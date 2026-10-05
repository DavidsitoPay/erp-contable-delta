import { useCallback, useEffect, useState } from "react";
import { movimientosTesoreriaApi } from "../../services/api";
import { indexarPorId } from "../../utils/cuentas";
import { mensajeError } from "../../utils/formato";
import FormularioMovimiento from "./FormularioMovimiento";
import SelectCuentaBancaria from "./SelectCuentaBancaria";
import TablaMovimientos from "./TablaMovimientos";

const ORIGENES = ["Manual", "Transferencia", "CxC", "CxP", "Apertura"];
const filtrosVacios = { cuentaBancariaId: "", desde: "", hasta: "", origen: "" };

function Movimientos({ cuentasBancarias, cuentasContables }) {
  const [filtros, setFiltros] = useState(filtrosVacios);
  const [movimientos, setMovimientos] = useState([]);
  const [error, setError] = useState("");

  const cargar = useCallback(async () => {
    setError("");
    const params = Object.fromEntries(Object.entries(filtros).filter(([, valor]) => valor !== ""));
    try {
      const { data } = await movimientosTesoreriaApi.listar(params);
      setMovimientos(data);
    } catch (err) {
      setError(mensajeError(err, "No se pudieron cargar los movimientos."));
    }
  }, [filtros]);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  function cambiarFiltro(campo, valor) {
    setFiltros((prev) => ({ ...prev, [campo]: valor }));
  }

  return (
    <div>
      <h3>Movimientos de tesorería</h3>

      <FormularioMovimiento cuentasBancarias={cuentasBancarias} cuentasContables={cuentasContables} onRegistrado={cargar} />
      <p>Para corregir un movimiento registra uno inverso; los movimientos no se editan ni se anulan.</p>

      <div className="catalog-form">
        <SelectCuentaBancaria
          etiqueta="Filtrar por cuenta"
          valor={filtros.cuentaBancariaId}
          onChange={(valor) => cambiarFiltro("cuentaBancariaId", valor)}
          cuentasBancarias={cuentasBancarias}
          soloActivas={false}
          requerido={false}
        />
        <input type="date" className="input" aria-label="Desde" value={filtros.desde} onChange={(e) => cambiarFiltro("desde", e.target.value)} />
        <input type="date" className="input" aria-label="Hasta" value={filtros.hasta} onChange={(e) => cambiarFiltro("hasta", e.target.value)} />
        <select className="select" aria-label="Filtrar por origen" value={filtros.origen} onChange={(e) => cambiarFiltro("origen", e.target.value)}>
          <option value="">Todos los orígenes</option>
          {ORIGENES.map((o) => <option key={o} value={o}>{o}</option>)}
        </select>
      </div>

      {error && <p className="error-chip">{error}</p>}

      <TablaMovimientos movimientos={movimientos} cuentasPorId={indexarPorId(cuentasBancarias)} />
    </div>
  );
}

export default Movimientos;
