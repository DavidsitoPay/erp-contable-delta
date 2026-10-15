import { useCallback, useEffect, useState } from "react";
import { tiposCambioApi } from "../../services/api";
import { MONEDAS_EXTRANJERAS } from "../../utils/cambio";
import { mensajeError } from "../../utils/formato";
import CasillaInactivos from "../Catalogo/CasillaInactivos";
import FormularioMotivo from "../FormularioMotivo";
import SelectMoneda from "../SelectMoneda";
import FormularioTasaManual from "./FormularioTasaManual";
import TablaTiposCambio from "./TablaTiposCambio";

const FILTROS_VACIOS = { moneda: "USD", desde: "", hasta: "", incluirInactivas: false };

function parametros({ moneda, desde, hasta, incluirInactivas }) {
  return {
    moneda,
    ...(desde ? { desde } : {}),
    ...(hasta ? { hasta } : {}),
    ...(incluirInactivas ? { incluirInactivas: true } : {}),
  };
}

function TiposCambio() {
  const [filtros, setFiltros] = useState(FILTROS_VACIOS);
  const [filas, setFilas] = useState([]);
  const [error, setError] = useState("");
  const [capturando, setCapturando] = useState(false);
  const [aDarDeBaja, setADarDeBaja] = useState(null);

  const cargar = useCallback(async () => {
    setError("");
    try {
      const { data } = await tiposCambioApi.historial(parametros(filtros));
      setFilas(data);
    } catch (err) {
      setError(mensajeError(err, "No se pudo cargar el historial de tipos de cambio."));
    }
  }, [filtros]);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  const cambiar = (campo) => (e) => setFiltros({ ...filtros, [campo]: e.target.value });

  async function darDeBaja(motivo) {
    await tiposCambioApi.darDeBaja(aDarDeBaja.id, motivo);
    setADarDeBaja(null);
    await cargar();
  }

  async function sustituir(fila) {
    setError("");
    try {
      await tiposCambioApi.sustituirPorBanguat({ monedaCodigo: fila.monedaCodigo, fecha: fila.fecha });
      await cargar();
    } catch (err) {
      setError(mensajeError(err, "No se pudo sustituir la tasa por la de Banguat."));
    }
  }

  async function alCapturar() {
    setCapturando(false);
    await cargar();
  }

  return (
    <div>
      <h2>Tipos de cambio</h2>
      <p className="warning-chip">Referencia de Banguat; la captura manual es un respaldo y exige motivo.</p>

      <div className="catalog-form">
        <SelectMoneda etiqueta="Filtrar por moneda" valor={filtros.moneda} onChange={(moneda) => setFiltros({ ...filtros, moneda })} opciones={MONEDAS_EXTRANJERAS} />
        <input type="date" className="input" aria-label="Desde" value={filtros.desde} onChange={cambiar("desde")} />
        <input type="date" className="input" aria-label="Hasta" value={filtros.hasta} onChange={cambiar("hasta")} />
        <CasillaInactivos marcada={filtros.incluirInactivas} onCambio={(marcada) => setFiltros({ ...filtros, incluirInactivas: marcada })} />
        <button type="button" className="btn btn-outline" onClick={() => setCapturando((actual) => !actual)}>
          {capturando ? "Ocultar captura manual" : "Capturar tasa manual"}
        </button>
      </div>

      {capturando && <FormularioTasaManual monedaInicial={filtros.moneda} onCapturada={alCapturar} onCancelar={() => setCapturando(false)} />}
      {aDarDeBaja && (
        <FormularioMotivo
          titulo={`Motivo de la baja de la tasa del ${aDarDeBaja.fecha}`}
          etiquetaConfirmar="Dar de baja"
          mensajeFallo="No se pudo dar de baja la tasa."
          onConfirmar={darDeBaja}
          onCancelar={() => setADarDeBaja(null)}
        />
      )}

      {error && <p className="error-chip">{error}</p>}
      <TablaTiposCambio filas={filas} onBaja={setADarDeBaja} onSustituir={sustituir} />
    </div>
  );
}

export default TiposCambio;
