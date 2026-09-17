import { useEffect, useState } from "react";
import { checkHealth } from "./services/api";

function App() {
  const [status, setStatus] = useState("Verificando conexión con la API...");

  useEffect(() => {
    checkHealth()
      .then(() => setStatus("Conectado a Delta ERP Contable API"))
      .catch(() => setStatus("No se pudo conectar con la API (¿está corriendo el backend?)"));
  }, []);

  return (
    <div style={{ fontFamily: "sans-serif", padding: "2rem" }}>
      <h1>Delta ERP Contable</h1>
      <p>{status}</p>
      {/* TODO Etapa 4 Frontend: login (M8) -> catálogo (M1) -> transacciones (M2) -> CxC/CxP -> reportes */}
    </div>
  );
}

export default App;
