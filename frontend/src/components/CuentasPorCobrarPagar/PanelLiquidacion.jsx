import PanelTipoCambio from "../TiposCambio/PanelTipoCambio";

function PanelLiquidacion({ liquidacion, fecha }) {
  return (
    <>
      {liquidacion.errorMonedas && <p className="error-chip">{liquidacion.errorMonedas}</p>}
      <PanelTipoCambio
        estado={liquidacion.cambioPago}
        moneda={liquidacion.monedaCuenta}
        fecha={fecha}
        titulo={`Referencia de ${liquidacion.monedaCuenta} de la fecha de pago`}
      />
    </>
  );
}

export default PanelLiquidacion;
