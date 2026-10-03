import { useEffect, useState } from "react";
import { librosApi } from "../../services/api";

const formatoMoneda = new Intl.NumberFormat("es-GT", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

function BalanceSaldos() {
  const [filas, setFilas] = useState([]);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    librosApi
      .balanceSaldos()
      .then(({ data }) => setFilas(data))
      .catch((err) => setError(err.response?.data?.error || "No se pudo cargar el balance de saldos."))
      .finally(() => setCargando(false));
  }, []);

  return (
    <div>
      <h2>Balance de saldos</h2>
      <p>Calculado en tiempo real a partir de los asientos confirmados. Una cuenta sin movimiento aparece con saldo 0.</p>

      {error && <p className="error-chip">{error}</p>}
      {cargando ? (
        <p>Cargando...</p>
      ) : (
        <div className="table-wrap">
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Naturaleza</th>
                  <th className="numeric">Total débito</th>
                  <th className="numeric">Total crédito</th>
                  <th className="numeric">Saldo</th>
                </tr>
              </thead>
              <tbody>
                {filas.map((f) => (
                  <tr key={f.cuentaId}>
                    <td>{f.codigo}</td>
                    <td>{f.nombre}</td>
                    <td>{f.naturaleza}</td>
                    <td className="numeric">{formatoMoneda.format(f.totalDebito)}</td>
                    <td className="numeric">{formatoMoneda.format(f.totalCredito)}</td>
                    <td className="numeric">{formatoMoneda.format(f.saldo)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

export default BalanceSaldos;
