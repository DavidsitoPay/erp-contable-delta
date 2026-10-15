import { esMonedaExtranjera, etiquetaFuente, formatoTasa } from "../../utils/cambio";
import { formatoImporte, formatoMoneda, formatoQuetzales } from "../../utils/formato";

function ResumenMoneda({ moneda, totalOriginal, tipoCambio, total }) {
  return (
    <>
      <p>Total original: {formatoImporte(totalOriginal, moneda)}</p>
      <p>{`Tipo de cambio: ${formatoTasa(tipoCambio.tasa)} (${etiquetaFuente(tipoCambio)})`}</p>
      <p>Total en GTQ: {formatoQuetzales(total)}</p>
    </>
  );
}

function ResumenFactura({ resumen, etiquetaIva, esCompra, moneda = "GTQ", tipoCambio = null }) {
  const iva = esCompra ? resumen.ivaAcreditable : resumen.iva;

  return (
    <div>
      {esMonedaExtranjera(moneda) && tipoCambio && (
        <ResumenMoneda moneda={moneda} totalOriginal={resumen.totalOriginal} tipoCambio={tipoCambio} total={resumen.total} />
      )}
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
