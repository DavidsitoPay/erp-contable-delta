import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PanelLiquidacion from "./PanelLiquidacion";

vi.mock("../../services/api", () => ({ tiposCambioApi: { capturarManual: vi.fn() } }));

function liquidacion(cambios = {}) {
  return {
    monedaCuenta: "USD",
    errorMonedas: "",
    cambioPago: { cargando: false, consultado: true, error: "", disponible: true, tipoCambio: { id: 2, tasa: 7.7, fuente: "Banguat", arrastrado: false }, propuesta: null, mensaje: "", recargar: vi.fn() },
    ...cambios,
  };
}

describe("PanelLiquidacion", () => {
  it("muestra el error de monedas", () => {
    render(<PanelLiquidacion liquidacion={liquidacion({ errorMonedas: "RN-27: una factura en quetzales no puede cobrarse con una cuenta en USD en esta versión." })} fecha="2026-11-10" />);

    expect(screen.getByText(/^RN-27/)).toBeInTheDocument();
  });

  it("no muestra error cuando las monedas son compatibles", () => {
    render(<PanelLiquidacion liquidacion={liquidacion()} fecha="2026-11-10" />);

    expect(screen.queryByText(/^RN-27/)).toBeNull();
  });

  it("muestra la referencia de la moneda de la cuenta en la fecha de pago", () => {
    render(<PanelLiquidacion liquidacion={liquidacion()} fecha="2026-11-10" />);

    expect(screen.getByText("Referencia de USD de la fecha de pago")).toBeInTheDocument();
    expect(screen.getByText("Tipo de cambio: 7.700000 (Banguat)")).toBeInTheDocument();
  });

  it("con cuenta GTQ no renderiza el panel de tipo de cambio", () => {
    render(<PanelLiquidacion liquidacion={liquidacion({ monedaCuenta: "GTQ" })} fecha="2026-11-10" />);

    expect(screen.queryByText(/Referencia de GTQ/)).toBeNull();
  });
});
