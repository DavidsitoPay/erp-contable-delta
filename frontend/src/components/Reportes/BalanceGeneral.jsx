import { reportesApi } from "../../services/api";
import PaginaReporte from "./PaginaReporte";
import { FilaMonto, SeccionReporte, TablaReporte } from "./TablaReporte";

const MENSAJE_FALLO = "No se pudo cargar el balance general.";

function FilaCuadre({ cuadra, diferencia }) {
  if (cuadra) {
    return (
      <tr>
        <td colSpan={2} className="text-success">
          Cuadra
        </td>
      </tr>
    );
  }
  return <FilaMonto etiqueta="Diferencia" valor={Math.abs(diferencia)} clase="text-danger" />;
}

function BalanceGeneral() {
  return (
    <PaginaReporte titulo="Balance general" obtener={reportesApi.balanceGeneral} mensajeFallo={MENSAJE_FALLO}>
      {(r, mostrarCeros) => (
        <TablaReporte>
          <SeccionReporte titulo="Activo" seccion={r.activo} mostrarCeros={mostrarCeros} />
          <SeccionReporte titulo="Pasivo" seccion={r.pasivo} mostrarCeros={mostrarCeros} />
          <SeccionReporte
            titulo="Capital"
            seccion={r.capital}
            mostrarCeros={mostrarCeros}
            total={r.totalCapital}
            etiquetaTotal="Total capital"
          >
            <FilaMonto etiqueta="Resultado del ejercicio (no distribuido)" valor={r.resultadoEjercicio} />
          </SeccionReporte>
          <tfoot>
            <FilaMonto etiqueta="Total pasivo + capital" valor={r.totalPasivoCapital} />
            <FilaCuadre cuadra={r.cuadra} diferencia={r.diferencia} />
          </tfoot>
        </TablaReporte>
      )}
    </PaginaReporte>
  );
}

export default BalanceGeneral;
