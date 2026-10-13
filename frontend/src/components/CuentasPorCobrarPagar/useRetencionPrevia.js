import { useEffect, useState } from "react";
import { mensajeError } from "../../utils/formato";

const ESPERA_MS = 300;
const SIN_RESULTADO = { previa: null, error: "", bloqueada: false, cargando: false };

export function useRetencionPrevia({ api, activo, proveedorId, fecha, montoBase, montoTotal }) {
  const [estado, setEstado] = useState(SIN_RESULTADO);
  const completo = Boolean(activo) && proveedorId !== "" && fecha !== "" && montoTotal > 0;

  useEffect(() => {
    if (!completo) {
      setEstado(SIN_RESULTADO);
      return undefined;
    }
    let vigente = true;
    const temporizador = setTimeout(() => {
      setEstado((anterior) => ({ ...anterior, cargando: true }));
      api
        .retencionPrevia({ proveedorId: Number(proveedorId), fecha, montoBase, montoTotal })
        .then(({ data }) => {
          if (vigente) setEstado({ ...SIN_RESULTADO, previa: data });
        })
        .catch((err) => {
          if (vigente) {
            setEstado({ ...SIN_RESULTADO, error: mensajeError(err, "No se pudo calcular la retención de ISR."), bloqueada: err.response?.status === 409 });
          }
        });
    }, ESPERA_MS);
    return () => {
      vigente = false;
      clearTimeout(temporizador);
    };
  }, [api, completo, proveedorId, fecha, montoBase, montoTotal]);

  return estado;
}
