function OpcionesSelect({ opciones }) {
  return opciones.map((opcion) => (
    <option key={opcion.valor} value={opcion.valor}>
      {opcion.etiqueta}
    </option>
  ));
}

export default OpcionesSelect;
