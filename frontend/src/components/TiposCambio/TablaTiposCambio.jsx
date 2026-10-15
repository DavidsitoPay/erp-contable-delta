import { formatoTasa } from "../../utils/cambio";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS = ["Fecha", "Moneda", "Tasa", "Fuente", "Motivo", "Usuario", "Estado"];

function AccionesTasa({ fila, onBaja, onSustituir }) {
  const editable = fila.activa && !fila.usada;
  return (
    <>
      <button type="button" className="btn btn-danger-outline btn-sm" aria-label={`Dar de baja la tasa del ${fila.fecha}`} disabled={!editable} onClick={() => onBaja(fila)}>
        Dar de baja
      </button>
      {editable && fila.fuente === "Manual" && (
        <button type="button" className="btn btn-outline btn-sm" aria-label={`Sustituir por Banguat la tasa del ${fila.fecha}`} onClick={() => onSustituir(fila)}>
          Sustituir por Banguat
        </button>
      )}
    </>
  );
}

function TablaTiposCambio({ filas, onBaja, onSustituir }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS}>
      {filas.length === 0 ? (
        <tr><td colSpan={ENCABEZADOS.length + 1}>No hay tipos de cambio en el rango seleccionado.</td></tr>
      ) : (
        filas.map((fila) => (
          <tr key={fila.id}>
            <td>{fila.fecha}</td>
            <td>{fila.monedaCodigo}</td>
            <td className="numeric">{formatoTasa(fila.tasa)}</td>
            <td>
              {fila.fuente}
              {fila.arrastrado && <span className="badge badge-warning">Arrastrado</span>}
            </td>
            <td>{fila.motivo ?? "—"}</td>
            <td>{fila.usuarioNombre ?? "—"}</td>
            <td>
              {fila.activa ? "Activa" : "Inactiva"}
              {fila.usada && <small className="nota-fiscal">En uso</small>}
            </td>
            <td><AccionesTasa fila={fila} onBaja={onBaja} onSustituir={onSustituir} /></td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

export default TablaTiposCambio;
