import { useConsultaPrevia } from "./useConsultaPrevia";

export function useRetencionPreviaPago({ api, activo, proveedorId, fecha, aplicaciones }) {
  const completo = Boolean(activo) && proveedorId !== "" && fecha !== "" && aplicaciones.length > 0;
  return useConsultaPrevia({
    api,
    completo,
    clave: JSON.stringify([proveedorId, fecha, aplicaciones]),
    consultar: () => api.retencionPreviaPago({ proveedorId: Number(proveedorId), fecha, aplicaciones }),
    mensaje: "No se pudo calcular la retención de ISR a no residentes.",
  });
}
