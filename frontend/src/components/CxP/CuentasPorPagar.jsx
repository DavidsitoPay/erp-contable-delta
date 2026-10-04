import { cxpApi } from "../../services/api";
import GestionCuentasPorCobrarPagar from "../CuentasPorCobrarPagar/GestionCuentasPorCobrarPagar";

const CONFIG_CXP = {
  titulo: "Cuentas por pagar",
  tipoContraparte: "Proveedor",
  etiquetaContraparte: "Proveedor",
  campoContraparteId: "proveedorId",
  campoContraparteNombre: "proveedorNombre",
  api: cxpApi,
  // La cuenta de control de CxP es un Pasivo (debemos) de naturaleza Acreedora.
  filtroCuentaControl: (c) => c.tipo === "Pasivo" && c.naturaleza === "Acreedora",
  etiquetaCuentaControl: "Cuenta de control (CxP)",
  etiquetaCuentaLinea: "Cuenta (gasto/activo)",
};

function CuentasPorPagar() {
  return <GestionCuentasPorCobrarPagar config={CONFIG_CXP} />;
}

export default CuentasPorPagar;
