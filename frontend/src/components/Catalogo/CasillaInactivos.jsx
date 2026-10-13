function CasillaInactivos({ marcada, onCambio }) {
  return (
    <label>
      <input type="checkbox" checked={marcada} onChange={(e) => onCambio(e.target.checked)} />
      <span>Incluir inactivos</span>
    </label>
  );
}

export default CasillaInactivos;
