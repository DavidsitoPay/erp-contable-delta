const REGIMENES_CON_FUNDAMENTO = new Set(["EXENTO", "PAGO_DIRECTO"]);

export function formularioContraparteVacio(tipo) {
  return {
    tipo,
    nombre: "",
    nit: "",
    direccion: "",
    regimenIva: "GENERAL",
    regimenIsr: tipo === "Proveedor" ? "NO_DEFINIDO" : "UTILIDADES",
    isrFundamento: "",
    esResidente: true,
    esAgenteRetencionIva: false,
    tipoAgenteIva: "",
    tipoRentaNrCodigo: "",
    pais: "",
  };
}

export const requiereFundamento = (form) => form.tipo === "Proveedor" && REGIMENES_CON_FUNDAMENTO.has(form.regimenIsr);

export const requiereTipoAgente = (form) => form.tipo === "Cliente" && form.esAgenteRetencionIva;

export const requiereTipoRenta = (form) => form.tipo === "Proveedor" && !form.esResidente && form.regimenIsr !== "EXENTO";

export const requierePais = (form) => !form.esResidente;

export function contraparteCompleta(form) {
  const fundamentoCompleto = !requiereFundamento(form) || form.isrFundamento.trim() !== "";
  const agenteCompleto = !requiereTipoAgente(form) || form.tipoAgenteIva !== "";
  const rentaCompleta = !requiereTipoRenta(form) || form.tipoRentaNrCodigo !== "";
  const paisCompleto = !requierePais(form) || /^[A-Z]{2}$/.test((form.pais ?? "").trim().toUpperCase());
  return fundamentoCompleto && agenteCompleto && rentaCompleta && paisCompleto;
}

export function payloadContraparte(form) {
  return {
    ...form,
    nit: form.nit.trim().toUpperCase(),
    esAgenteRetencionIva: form.tipo === "Cliente" && form.esAgenteRetencionIva,
    isrFundamento: requiereFundamento(form) ? form.isrFundamento.trim() : null,
    tipoAgenteIva: requiereTipoAgente(form) ? form.tipoAgenteIva : null,
    tipoRentaNrCodigo: form.tipo === "Proveedor" && !form.esResidente && form.tipoRentaNrCodigo !== "" ? form.tipoRentaNrCodigo : null,
    pais: requierePais(form) ? (form.pais ?? "").trim().toUpperCase() : null,
  };
}
