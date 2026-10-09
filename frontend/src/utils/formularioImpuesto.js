export const FORMULARIO_VACIO = {
  codigo: "",
  nombre: "",
  tipo: "IVA_GENERAL",
  tasa: "",
  aplicaA: "AMBOS",
  generaCredito: true,
  articuloLegal: "",
  vigenteDesde: "",
  vigenteHasta: "",
};

export function formularioDesdeImpuesto(impuesto, modo) {
  const base = {
    codigo: impuesto.codigo,
    nombre: impuesto.nombre,
    tipo: impuesto.tipo,
    tasa: String(impuesto.tasa),
    aplicaA: impuesto.aplicaA,
    generaCredito: impuesto.generaCredito,
    articuloLegal: impuesto.articuloLegal,
    vigenteDesde: impuesto.vigenteDesde,
    vigenteHasta: impuesto.vigenteHasta ?? "",
  };
  return modo === "version" ? { ...base, vigenteDesde: "", vigenteHasta: "" } : base;
}

export function payloadCreacion(form) {
  return {
    codigo: form.codigo.trim(),
    nombre: form.nombre.trim(),
    tipo: form.tipo,
    tasa: Number(form.tasa),
    aplicaA: form.aplicaA,
    generaCredito: form.generaCredito,
    articuloLegal: form.articuloLegal.trim(),
    vigenteDesde: form.vigenteDesde,
    vigenteHasta: form.vigenteHasta || null,
  };
}

export function payloadEdicion(form, impuesto) {
  const campos = impuesto.enUso ? formularioDesdeImpuesto(impuesto, "editar") : form;
  return {
    nombre: form.nombre.trim(),
    tipo: campos.tipo,
    tasa: Number(campos.tasa),
    aplicaA: campos.aplicaA,
    generaCredito: campos.generaCredito,
    articuloLegal: form.articuloLegal.trim(),
    nota: impuesto.nota ?? null,
    vigenteDesde: campos.vigenteDesde,
    vigenteHasta: form.vigenteHasta || null,
    activo: impuesto.activo,
  };
}

export function payloadCierre(impuesto, fecha) {
  return { ...payloadEdicion(formularioDesdeImpuesto(impuesto, "editar"), impuesto), vigenteHasta: fecha };
}
