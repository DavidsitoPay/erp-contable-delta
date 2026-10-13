export const hastaONulo = (form) => form.vigenteHasta || null;

export function sinVigencia(formulario) {
  return { ...formulario, vigenteDesde: "", vigenteHasta: "" };
}
