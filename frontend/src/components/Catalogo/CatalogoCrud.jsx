export function FormularioCatalogo({ onSubmit, cargando, error, children }) {
  return (
    <>
      <form onSubmit={onSubmit} className="catalog-form">
        {children}
        <button type="submit" disabled={cargando} className="btn btn-primary">Agregar</button>
      </form>
      {error && <p className="error-chip">{error}</p>}
    </>
  );
}

export function TablaCatalogo({ encabezados, children }) {
  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              {encabezados.map((titulo) => <th key={titulo}>{titulo}</th>)}
              <th></th>
            </tr>
          </thead>
          <tbody>{children}</tbody>
        </table>
      </div>
    </div>
  );
}
