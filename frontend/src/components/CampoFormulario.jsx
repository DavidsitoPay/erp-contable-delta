function CampoFormulario({ id, etiqueta, children }) {
  return (
    <div className="form-field">
      <label htmlFor={id}>{etiqueta}</label>
      {children}
    </div>
  );
}

export default CampoFormulario;
