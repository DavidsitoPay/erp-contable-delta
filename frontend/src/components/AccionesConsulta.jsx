export function BotonesConsulta({ cargando, mostrarExportar, onExportar }) {
  return (
    <>
      <button type="submit" className="btn btn-primary" disabled={cargando}>Consultar</button>
      {mostrarExportar && (
        <button type="button" className="btn btn-outline" onClick={onExportar}>Exportar CSV</button>
      )}
    </>
  );
}

export function EstadoConsulta({ error, cargando }) {
  return (
    <>
      {error && <p className="error-chip">{error}</p>}
      {cargando && <p>Cargando...</p>}
    </>
  );
}
