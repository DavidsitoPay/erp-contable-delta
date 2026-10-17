import { useEffect, useState } from "react";
import { mensajeError } from "../../utils/formato";

const ESPERA_MS = 300;
export const SIN_RESULTADO = { previa: null, error: "", bloqueada: false, cargando: false };

export function useConsultaPrevia({ api, completo, clave, consultar, mensaje }) {
  const [estado, setEstado] = useState(SIN_RESULTADO);

  useEffect(() => {
    if (!completo) {
      setEstado(SIN_RESULTADO);
      return undefined;
    }
    let vigente = true;
    const temporizador = setTimeout(() => {
      setEstado((anterior) => ({ ...anterior, cargando: true }));
      consultar()
        .then(({ data }) => {
          if (vigente) setEstado({ ...SIN_RESULTADO, previa: data });
        })
        .catch((err) => {
          if (vigente) {
            setEstado({ ...SIN_RESULTADO, error: mensajeError(err, mensaje), bloqueada: err.response?.status === 409 });
          }
        });
    }, ESPERA_MS);
    return () => {
      vigente = false;
      clearTimeout(temporizador);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, completo, clave]);

  return estado;
}
