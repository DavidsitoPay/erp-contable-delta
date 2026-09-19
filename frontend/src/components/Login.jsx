import { useState } from "react";
import { login } from "../services/api";
import LogoMark from "./LogoMark";
import ToggleTema from "./ToggleTema";

function Login({ onLoginExitoso }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setCargando(true);
    try {
      const { data } = await login(email, password);
      localStorage.setItem("delta_token", data.token);
      localStorage.setItem("delta_usuario", JSON.stringify(data.usuario));
      onLoginExitoso(data.usuario);
    } catch (err) {
      const mensaje = err.response?.data?.error || "No se pudo iniciar sesión.";
      setError(mensaje);
    } finally {
      setCargando(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-toolbar">
          <ToggleTema />
        </div>
        <div className="login-brand">
          <div className="brand">
            <div className="logo-badge">
              <LogoMark size={64} />
            </div>
            <span className="wordmark">Delta</span>
          </div>
        </div>
        <p className="login-subtitle">ERP Contable</p>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-field">
            <label htmlFor="email">Correo</label>
            <input
              id="email"
              type="email"
              className="input"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div className="form-field">
            <label htmlFor="password">Contraseña</label>
            <input
              id="password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          {error && (
            <p className="error-chip" role="alert">
              {error}
            </p>
          )}
          <button type="submit" disabled={cargando} className="btn btn-primary btn-block">
            {cargando ? "Ingresando..." : "Ingresar"}
          </button>
        </form>
      </div>
    </div>
  );
}

export default Login;
