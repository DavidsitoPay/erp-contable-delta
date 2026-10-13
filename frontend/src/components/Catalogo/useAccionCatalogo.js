import { useState } from "react";
import { mensajeError } from "../../utils/formato";

export function useAccionCatalogo(recargar) {
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function ejecutar(accion, mensajeFallo) {
    setError("");
    setCargando(true);
    try {
      await accion();
      await recargar();
      return true;
    } catch (err) {
      setError(mensajeError(err, mensajeFallo));
      return false;
    } finally {
      setCargando(false);
    }
  }

  return { error, setError, cargando, ejecutar };
}
