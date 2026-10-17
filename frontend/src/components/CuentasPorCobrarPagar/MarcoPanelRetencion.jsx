function MarcoPanelRetencion({ titulo, estado, siempre = false, children }) {
  const { previa, error, cargando } = estado;
  if (!siempre && !previa && !error && !cargando) return null;

  return (
    <fieldset className="dte-datos">
      <legend>{titulo}</legend>
      {cargando && <p>Calculando retención...</p>}
      {error && <p className="error-chip">{error}</p>}
      {children}
    </fieldset>
  );
}

export default MarcoPanelRetencion;
