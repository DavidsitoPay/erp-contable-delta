import { useConsultaPrevia } from "./useConsultaPrevia";

export function useRetencionPrevia({ api, activo, proveedorId, fecha, montoBase, montoTotal }) {
  const completo = Boolean(activo) && proveedorId !== "" && fecha !== "" && montoTotal > 0;
  return useConsultaPrevia({
    api,
    completo,
    clave: [proveedorId, fecha, montoBase, montoTotal].join("|"),
    consultar: () => api.retencionPrevia({ proveedorId: Number(proveedorId), fecha, montoBase, montoTotal }),
    mensaje: "No se pudo calcular la retención de ISR.",
  });
}
