import { reportesApi } from "../../services/api";
import PaginaReporte from "./PaginaReporte";
import { FilaMonto, SeccionReporte, TablaReporte } from "./TablaReporte";

const MENSAJE_FALLO = "No se pudo cargar el estado de resultados.";

function EstadoResultados() {
  return (
    <PaginaReporte titulo="Estado de resultados" obtener={reportesApi.estadoResultados} mensajeFallo={MENSAJE_FALLO}>
      {(r, mostrarCeros) => (
        <TablaReporte>
          <SeccionReporte titulo="Ingresos" seccion={r.ingresos} mostrarCeros={mostrarCeros} />
          <SeccionReporte titulo="Gastos" seccion={r.gastos} mostrarCeros={mostrarCeros} />
          <tfoot>
            <FilaMonto
              etiqueta={r.utilidadNeta < 0 ? "Pérdida neta" : "Utilidad neta"}
              valor={Math.abs(r.utilidadNeta)}
              clase={r.utilidadNeta < 0 ? "text-danger" : undefined}
            />
          </tfoot>
        </TablaReporte>
      )}
    </PaginaReporte>
  );
}

export default EstadoResultados;
