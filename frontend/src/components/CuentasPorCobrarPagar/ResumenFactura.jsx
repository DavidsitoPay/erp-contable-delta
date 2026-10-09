import { formatoMoneda } from "../../utils/formato";

function ResumenFactura({ resumen, etiquetaIva, esCompra }) {
  const iva = esCompra ? resumen.ivaAcreditable : resumen.iva;

  return (
    <div>
      <p>Base: {formatoMoneda.format(resumen.base)}</p>
      <p>
        {etiquetaIva}: {formatoMoneda.format(iva)}
      </p>
      {esCompra && resumen.ivaCosto > 0 && <p>IVA a costo: {formatoMoneda.format(resumen.ivaCosto)}</p>}
      <p>Total de la factura: {formatoMoneda.format(resumen.total)}</p>
      <p>Vista previa; el sistema recalcula al registrar.</p>
    </div>
  );
}

export default ResumenFactura;
