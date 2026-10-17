import { useCallback, useEffect, useState } from "react";
import { enteramientosApi } from "../../services/api";
import { AVISO_CALENDARIO, CLASE_VENCIMIENTO, ESTADOS_ENTERAMIENTO, ESTADOS_VENCIMIENTO } from "../../utils/enteramiento";
import { formatoMoneda, mensajeError } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";
import FormularioMotivo from "../FormularioMotivo";
import CeldasSaldoPeriodo from "./CeldasSaldoPeriodo";
import FormularioEnteramiento from "./FormularioEnteramiento";

const fechaDmy = (iso) => {
  if (!iso) return "";
  const [anio, mes, dia] = iso.split("-");
  return `${dia}/${mes}/${anio}`;
};

function Enteramiento() {
  const [saldos, setSaldos] = useState([]);
  const [enteramientos, setEnteramientos] = useState([]);
  const [accion, setAccion] = useState(null);
  const [error, setError] = useState("");

  const cargar = useCallback(async () => {
    try {
      const [{ data: saldosData }, { data: enteramientosData }] = await Promise.all([enteramientosApi.saldo(), enteramientosApi.listar()]);
      setSaldos(saldosData);
      setEnteramientos(enteramientosData);
      setError("");
    } catch (err) {
      setError(mensajeError(err, "No se pudo cargar el enteramiento."));
    }
  }, []);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  const terminar = async () => {
    setAccion(null);
    await cargar();
  };

  return (
    <>
      <h2>Enteramiento de ISR a no residentes</h2>
      {error && <p className="error-chip">{error}</p>}
      {saldos.some((s) => s.calendarioIncompleto) && <p className="warning-chip">{AVISO_CALENDARIO}</p>}
      <TablaCatalogo encabezados={["Período", "Retenido", "Enterado", "Saldo por enterar", "Vence", "Días para vencer", "Estado", ""]}>
        {saldos.map((s) => (
          <tr key={s.periodo}>
            <CeldasSaldoPeriodo fila={s} saldo={s.saldo} />
            <td>{fechaDmy(s.fechaVencimiento)}</td>
            <td className="numeric">{s.diasParaVencer}</td>
            <td><span className={CLASE_VENCIMIENTO[s.estadoVencimiento]}>{ESTADOS_VENCIMIENTO[s.estadoVencimiento]}</span></td>
            <td>
              {s.saldo > 0 && (
                <button type="button" className="btn btn-outline" aria-label={`Enterar ${s.periodo.slice(0, 7)}`} onClick={() => setAccion({ tipo: "enterar", saldo: s })}>
                  Enterar
                </button>
              )}
              {(s.estadoVencimiento === "POR_VENCER" || s.estadoVencimiento === "VENCIDO") && s.avisoMulta && (
                <small className="nota-fiscal">{s.avisoMulta}</small>
              )}
              {s.estadoVencimiento === "POR_VENCER" && (
                <small className="nota-fiscal">{`Vence en ${s.diasParaVencer} días hábiles o menos: programe el pago.`}</small>
              )}
            </td>
          </tr>
        ))}
      </TablaCatalogo>

      {accion?.tipo === "enterar" && (
        <FormularioEnteramiento saldo={accion.saldo} onRegistrado={terminar} onCancelar={() => setAccion(null)} />
      )}
      {accion?.tipo === "anular" && (
        <FormularioMotivo
          titulo={`Motivo de anulación del entero ${accion.enteramiento.boleta}`}
          etiquetaConfirmar="Anular enteramiento"
          mensajeFallo="No se pudo anular el enteramiento."
          onConfirmar={async (motivo) => {
            await enteramientosApi.anular(accion.enteramiento.id, motivo);
            await terminar();
          }}
          onCancelar={() => setAccion(null)}
        />
      )}

      <h3>Enteramientos registrados</h3>
      <TablaCatalogo encabezados={["Período", "Formulario", "Fecha de pago", "Boleta", "Monto", "Estado", ""]}>
        {enteramientos.length === 0 && (
          <tr>
            <td colSpan={7}>No hay enteramientos registrados.</td>
          </tr>
        )}
        {enteramientos.map((e) => (
          <tr key={e.id}>
            <td>{e.periodo.slice(0, 7)}</td>
            <td>{e.formulario}</td>
            <td>{fechaDmy(e.fechaPago)}</td>
            <td>{e.boleta}</td>
            <td className="numeric">{formatoMoneda.format(e.monto)}</td>
            <td>
              {ESTADOS_ENTERAMIENTO[e.estado]}
              {e.estado === "ANULADO" && <small className="nota-fiscal">{e.motivoAnulacion}</small>}
            </td>
            <td>
              {e.estado === "VIGENTE" && (
                <button type="button" className="btn btn-outline" aria-label={`Anular entero ${e.boleta}`} onClick={() => setAccion({ tipo: "anular", enteramiento: e })}>
                  Anular
                </button>
              )}
            </td>
          </tr>
        ))}
      </TablaCatalogo>
    </>
  );
}

export default Enteramiento;
