import { CONCEPTOS_RETENCION, ESTADOS_CONSTANCIA, SENTIDOS_RETENCION, etiquetaDe } from "../../utils/etiquetasFiscales";
import { formatoMoneda } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS = ["Sentido", "Impuesto", "NIT", "Nombre", "Documento", "Fecha documento", "Fecha pago o cobro", "Base", "Tasa efectiva (%)", "Retención", "Constancia", "Estado"];
const CLASE_ESTADO = { PENDIENTE: "badge-warning", VENCIDA: "badge-warning", EMITIDA: "badge-ok" };
const ESTADOS_ALERTA = new Set(["PENDIENTE", "VENCIDA"]);

function AccionConstancia({ fila, onAccion }) {
  if (fila.estadoConstancia === "EMITIDA") {
    return (
      <button type="button" className="btn btn-danger-outline btn-sm" aria-label={`Anular constancia de ${fila.documentoNumero}`} onClick={() => onAccion({ tipo: "anular", fila })}>
        Anular
      </button>
    );
  }
  return (
    <button type="button" className="btn btn-outline btn-sm" aria-label={`Registrar constancia de ${fila.documentoNumero}`} onClick={() => onAccion({ tipo: "registrar", fila })}>
      Registrar constancia
    </button>
  );
}

function TablaRetenciones({ filas, onAccion }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS}>
      {filas.length === 0 ? (
        <tr>
          <td colSpan={ENCABEZADOS.length + 1}>No hay retenciones en el período seleccionado.</td>
        </tr>
      ) : (
        filas.map((f) => (
          <tr key={f.retencionId} className={ESTADOS_ALERTA.has(f.estadoConstancia) ? "row-alerta" : undefined}>
            <td>{etiquetaDe(SENTIDOS_RETENCION, f.sentido)}</td>
            <td>{etiquetaDe(CONCEPTOS_RETENCION, f.concepto)}</td>
            <td>{f.nit}</td>
            <td>{f.nombre}</td>
            <td>
              {f.documentoNumero}
              {f.periodoContableDistinto && <small className="nota-fiscal">Cobro en otro mes</small>}
            </td>
            <td>{f.fechaDocumento}</td>
            <td>{f.fechaOperacion}</td>
            <td className="numeric">{formatoMoneda.format(f.base)}</td>
            <td className="numeric">{f.tasaEfectiva ?? f.tasa}</td>
            <td className="numeric">{formatoMoneda.format(f.monto)}</td>
            <td>
              {f.constanciaNumero ?? "—"}
              {f.constanciaFecha && <small className="nota-fiscal">{f.constanciaFecha}</small>}
            </td>
            <td>
              <span className={`badge ${CLASE_ESTADO[f.estadoConstancia] ?? ""}`}>{etiquetaDe(ESTADOS_CONSTANCIA, f.estadoConstancia)}</span>
            </td>
            <td>
              <AccionConstancia fila={f} onAccion={onAccion} />
            </td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

export default TablaRetenciones;
