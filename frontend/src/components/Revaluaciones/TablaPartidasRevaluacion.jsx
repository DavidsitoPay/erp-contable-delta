import { formatoDiferencial, formatoImporte, formatoMoneda } from "../../utils/formato";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";

const ENCABEZADOS = ["Tipo", "Referencia", "Saldo original", "Valor en libros Q", "Valor al cierre Q", "Ajuste Q", "Efecto"];
const ETIQUETAS_EFECTO = { Ganancia: "Ganancia", Perdida: "Pérdida" };

function TablaPartidasRevaluacion({ partidas, moneda }) {
  return (
    <TablaCatalogo encabezados={ENCABEZADOS}>
      {partidas.length === 0 ? (
        <tr><td colSpan={ENCABEZADOS.length + 1}>No hay partidas con diferencia.</td></tr>
      ) : (
        partidas.map((p) => (
          <tr key={`${p.tipo}-${p.referencia}`}>
            <td>{p.tipo}</td>
            <td>{p.referencia}</td>
            <td className="numeric">{formatoImporte(p.saldoOriginal, moneda)}</td>
            <td className="numeric">{formatoMoneda.format(p.valorLibrosGtq)}</td>
            <td className="numeric">{formatoMoneda.format(p.valorCierreGtq)}</td>
            <td className="numeric">{formatoDiferencial(p.ajusteGtq)}</td>
            <td>{ETIQUETAS_EFECTO[p.efecto] ?? p.efecto}</td>
          </tr>
        ))
      )}
    </TablaCatalogo>
  );
}

export default TablaPartidasRevaluacion;
