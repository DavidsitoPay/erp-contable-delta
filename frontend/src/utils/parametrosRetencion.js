import { BASES_UMBRAL, etiquetaDe } from "./etiquetasFiscales";
import { formatoMoneda } from "./formato";
import { LEYENDA_REFERENCIAL, descripcionEscala } from "./retenciones";
import { hastaONulo, sinVigencia } from "./vigencia";

export const FORMULARIO_PARAMETRO_VACIO = {
  codigo: "",
  concepto: "ISR_PROVEEDOR_ROS",
  nombre: "",
  tasaBase: "",
  tramoLimite: "",
  tasaExcedente: "",
  umbralMinimo: "0",
  umbralSobre: "BASE",
  diaLimite: "",
  articuloLegal: "",
  nota: LEYENDA_REFERENCIAL,
  vigenteDesde: "",
  vigenteHasta: "",
};

const texto = (valor) => (valor === null || valor === undefined ? "" : String(valor));

export const esPlazo = (form) => form.concepto === "PLAZO_CONSTANCIA";

export const admiteTramo = (form) => form.concepto === "ISR_PROVEEDOR_ROS";

export const esNoResidente = (form) => form.concepto === "ISR_NO_RESIDENTE";

export function formularioDesdeParametro(parametro) {
  return sinVigencia({
    codigo: parametro.codigo,
    concepto: parametro.concepto,
    nombre: parametro.nombre,
    tasaBase: texto(parametro.tasaBase),
    tramoLimite: texto(parametro.tramoLimite),
    tasaExcedente: texto(parametro.tasaExcedente),
    umbralMinimo: texto(parametro.umbralMinimo) || "0",
    umbralSobre: parametro.umbralSobre,
    diaLimite: texto(parametro.diaLimite),
    articuloLegal: parametro.articuloLegal,
    nota: parametro.nota ?? LEYENDA_REFERENCIAL,
  });
}

export function payloadCreacion(form) {
  const plazo = esPlazo(form);
  const conTramo = admiteTramo(form) && form.tramoLimite !== "";
  return {
    codigo: form.codigo.trim(),
    concepto: form.concepto,
    nombre: form.nombre.trim(),
    tasaBase: plazo ? null : Number(form.tasaBase),
    tramoLimite: conTramo ? Number(form.tramoLimite) : null,
    tasaExcedente: conTramo ? Number(form.tasaExcedente) : null,
    umbralMinimo: plazo || esNoResidente(form) ? 0 : Number(form.umbralMinimo || 0),
    umbralSobre: form.umbralSobre,
    diaLimite: plazo ? Number(form.diaLimite) : null,
    articuloLegal: form.articuloLegal.trim(),
    nota: form.nota.trim() || LEYENDA_REFERENCIAL,
    vigenteDesde: form.vigenteDesde,
    vigenteHasta: hastaONulo(form),
  };
}

export const payloadActualizacion = (parametro, cambios) => ({
  nota: parametro.nota,
  activo: parametro.activo,
  vigenteHasta: parametro.vigenteHasta ?? null,
  ...cambios,
});

export function descripcionTasa(parametro) {
  return parametro.concepto === "PLAZO_CONSTANCIA" ? `Día ${parametro.diaLimite} del mes siguiente` : descripcionEscala(parametro);
}

export function descripcionUmbral(parametro) {
  if (parametro.concepto === "PLAZO_CONSTANCIA" || parametro.concepto === "ISR_NO_RESIDENTE") return "—";
  return `${formatoMoneda.format(parametro.umbralMinimo)} · ${etiquetaDe(BASES_UMBRAL, parametro.umbralSobre)}`;
}
