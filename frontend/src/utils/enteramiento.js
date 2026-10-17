import { FORMULARIOS_CONSTANCIA } from "./etiquetasFiscales";

export const FORMULARIOS_ENTERAMIENTO = FORMULARIOS_CONSTANCIA.filter((f) => f.valor !== "SAT-2340");
export const ESTADOS_VENCIMIENTO = { AL_DIA: "Al día", POR_VENCER: "Por vencer", VENCIDO: "Vencido", SIN_SALDO: "Sin saldo" };
export const ESTADOS_ENTERAMIENTO = { VIGENTE: "Vigente", ANULADO: "Anulado" };
export const CLASE_VENCIMIENTO ={ AL_DIA: "badge-ok", POR_VENCER: "badge-warning", VENCIDO: "badge-warning", SIN_SALDO: "" };
export const AVISO_CALENDARIO = "No hay feriados cargados para el año del vencimiento; la fecha de vencimiento puede ser inexacta.";
export const cuentasGtq = (cuentas) => cuentas.filter((c) => c.monedaCodigo === undefined || c.monedaCodigo === "GTQ");
export const enteramientoValido = (f) => f.formulario !== "" && f.fechaPago !== "" && f.boleta.trim() !== "" && Number(f.monto) > 0 && f.cuentaBancariaId !== "";
export const solicitudEnteramiento = (periodo, f) => ({ periodo, monto: Number(f.monto), cuentaBancariaId: Number(f.cuentaBancariaId) });
export const payloadEnteramiento = (periodo, f) => ({ ...solicitudEnteramiento(periodo, f), formulario: f.formulario, fechaPago: f.fechaPago, boleta: f.boleta.trim() });
