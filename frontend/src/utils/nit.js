export function normalizarNit(valor) {
  const limpio = valor.toUpperCase().replace(/[\s-]/g, "");
  return limpio.length > 1 ? `${limpio.slice(0, -1)}-${limpio.slice(-1)}` : limpio;
}

function digitoVerificador(cuerpo) {
  const suma = [...cuerpo].reduce((acumulado, digito, posicion) => acumulado + Number(digito) * (cuerpo.length + 1 - posicion), 0);
  const digito = (11 - (suma % 11)) % 11;
  return digito === 10 ? "K" : String(digito);
}

const normalizarCf = (valor) => valor.trim().toUpperCase().replace("C/F", "CF");

export function nitValido(valor) {
  const [cuerpo, verificador] = normalizarNit(normalizarCf(valor)).split("-");
  return /^\d{1,12}$/.test(cuerpo) && verificador === digitoVerificador(cuerpo);
}

export function mensajeNit(valor, tipo) {
  const nit = normalizarCf(valor);
  if (tipo === "Cliente" && (nit === "" || nit === "CF")) return "";
  if (tipo !== "Cliente" && nit === "CF") return "CF solo es válido para clientes.";
  if (tipo !== "Cliente" && nit === "") return "El NIT del proveedor es obligatorio.";
  return nitValido(nit) ? "" : "El NIT no es válido (dígito verificador incorrecto).";
}
