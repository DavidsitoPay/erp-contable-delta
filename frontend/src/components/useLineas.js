import { useState } from "react";

export function useLineas(crearLinea, minimo) {
  const iniciales = () => Array.from({ length: minimo }, () => crearLinea());
  const [lineas, setLineas] = useState(iniciales);

  function actualizarLinea(index, campo, valor) {
    setLineas((prev) => prev.map((l, i) => (i === index ? { ...l, [campo]: valor } : l)));
  }

  function agregarLinea() {
    setLineas((prev) => [...prev, crearLinea()]);
  }

  function quitarLinea(index) {
    setLineas((prev) => (prev.length <= minimo ? prev : prev.filter((_, i) => i !== index)));
  }

  function reiniciarLineas() {
    setLineas(iniciales());
  }

  return { lineas, actualizarLinea, agregarLinea, quitarLinea, reiniciarLineas };
}
