export function cuentasHoja(cuentas) {
  const idsConHijos = new Set(cuentas.filter((c) => c.cuentaPadreId).map((c) => c.cuentaPadreId));
  return cuentas.filter((c) => c.activa && !idsConHijos.has(c.id));
}

export function cuentasHojaDeTipo(cuentas, tipo) {
  return cuentasHoja(cuentas).filter((c) => c.tipo === tipo);
}

export function indexarPorId(lista) {
  return Object.fromEntries(lista.map((elemento) => [elemento.id, elemento]));
}

export function etiquetaCuenta(cuenta) {
  return cuenta ? `${cuenta.banco} ${cuenta.numero}` : "";
}

export function sangriaPorProfundidad(profundidad) {
  return `${0.75 + profundidad * 1.25}rem`;
}

export function etiquetaSubcuentas(cantidad) {
  return cantidad === 1 ? "1 subcuenta" : `${cantidad} subcuentas`;
}
