import { cxcApi } from "../../services/api";
import GestionCuentasPorCobrarPagar from "../CuentasPorCobrarPagar/GestionCuentasPorCobrarPagar";

const CONFIG_CXC = {
  titulo: "Cuentas por cobrar",
  tipoContraparte: "Cliente",
  etiquetaContraparte: "Cliente",
  campoContraparteId: "clienteId",
  campoContraparteNombre: "clienteNombre",
  api: cxcApi,
  // La cuenta de control de CxC es un Activo (nos deben) de naturaleza Deudora.
  filtroCuentaControl: (c) => c.tipo === "Activo" && c.naturaleza === "Deudora",
  etiquetaCuentaControl: "Cuenta de control (CxC)",
  etiquetaCuentaLinea: "Cuenta (ingreso)",
};

function CuentasPorCobrar() {
  return <GestionCuentasPorCobrarPagar config={CONFIG_CXC} />;
}

export default CuentasPorCobrar;
