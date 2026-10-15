import { useCallback, useEffect, useState } from "react";
import { tiposCambioApi } from "../../services/api";
import { esMonedaExtranjera } from "../../utils/cambio";
import { mensajeError } from "../../utils/formato";

const ESPERA_MS = 300;
const SIN_RESULTADO = { cargando: false, consultado: false, error: "", disponible: false, tipoCambio: null, propuesta: null, mensaje: "" };

export function useTipoCambio({ moneda, fecha, activo = true }) {
  const [estado, setEstado] = useState(SIN_RESULTADO);
  const [version, setVersion] = useState(0);
  const requerido = Boolean(activo) && esMonedaExtranjera(moneda);
  const completo = requerido && Boolean(fecha);

  useEffect(() => {
    if (!completo) {
      setEstado(SIN_RESULTADO);
      return undefined;
    }
    let vigente = true;
    setEstado({ ...SIN_RESULTADO, cargando: true });
    const temporizador = setTimeout(() => {
      tiposCambioApi
        .consultar(moneda, fecha)
        .then(({ data }) => {
          if (vigente) {
            setEstado({ ...SIN_RESULTADO, consultado: true, disponible: data.disponible, tipoCambio: data.tipoCambio, propuesta: data.propuesta, mensaje: data.mensaje ?? "" });
          }
        })
        .catch((err) => {
          if (vigente) setEstado({ ...SIN_RESULTADO, error: mensajeError(err, "No se pudo consultar el tipo de cambio.") });
        });
    }, ESPERA_MS);
    return () => {
      vigente = false;
      clearTimeout(temporizador);
    };
  }, [completo, moneda, fecha, version]);

  const recargar = useCallback(() => setVersion((anterior) => anterior + 1), []);
  const tasa = estado.disponible && estado.tipoCambio ? estado.tipoCambio.tasa : null;

  return { ...estado, tasa, requerido, bloqueado: requerido && tasa === null, recargar };
}
