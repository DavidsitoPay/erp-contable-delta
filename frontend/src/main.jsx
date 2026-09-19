import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App.jsx";
import { iniciarSincronizacionTema } from "./tema.js";
import "./styles/theme.css";
import "./styles/components.css";

// Inicia la sincronización de tema entre pestañas y con el SO
// ANTES de renderizar, así cubre tanto Login como la app autenticada
iniciarSincronizacionTema();

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
