import { useRef, useState } from "react";
import { mensajeError } from "../../utils/formato";
import { RETENCION_VACIA } from "../../utils/retenciones";

export function useRetencionesCobro(api) {
  const [valores, setValores] = useState({});
  const [sugerencias, setSugerencias] = useState({});
  const pedidas = useRef(new Set());

  function cambiar(documentoId, cambios) {
    setValores((previos) => ({ ...previos, [documentoId]: { ...RETENCION_VACIA, ...previos[documentoId], ...cambios } }));
  }

  async function solicitarSugerencia(documentoId) {
    if (pedidas.current.has(documentoId)) return;
    pedidas.current.add(documentoId);
    try {
      const { data } = await api.retencionIvaSugerida(documentoId);
      setSugerencias((previas) => ({ ...previas, [documentoId]: data }));
    } catch (err) {
      pedidas.current.delete(documentoId);
      setSugerencias((previas) => ({ ...previas, [documentoId]: { error: mensajeError(err, "No se pudo consultar el IVA retenible de la factura.") } }));
    }
  }

  function reiniciar() {
    setValores({});
    setSugerencias({});
    pedidas.current.clear();
  }

  return { valores, sugerencias, cambiar, solicitarSugerencia, reiniciar };
}
