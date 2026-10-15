import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import TransferenciaMoneda from "./TransferenciaMoneda";

vi.mock("../../services/api", () => ({ tiposCambioApi: { capturarManual: vi.fn() } }));

const cambio = {
  cargando: false,
  error: "",
  disponible: true,
  tipoCambio: { tasa: 7.64157, fuente: "Banguat", arrastrado: false },
  propuesta: null,
  mensaje: "",
  tasa: 7.64157,
  recargar: vi.fn(),
};
const cuentaGtq = { id: 7, banco: "BAC", numero: "123", monedaCodigo: "GTQ" };
const cuentaUsd = { id: 10, banco: "BAC", numero: "USD-1", monedaCodigo: "USD" };

describe("TransferenciaMoneda", () => {
  it("compra de dólares: muestra la diferencia contra la referencia y el monto destino en USD", () => {
    render(<TransferenciaMoneda origen={cuentaGtq} destino={cuentaUsd} fecha="2026-10-12" montoOrigen="7700" montoDestino="1000" onMontoDestino={vi.fn()} cambio={cambio} />);

    expect(screen.getByText("Diferencia vs. referencia: -Q58.43; se asienta a ganancia o pérdida realizada.")).toBeInTheDocument();
    expect(screen.getByLabelText("Monto destino en USD")).toBeInTheDocument();
  });

  it("venta de dólares: muestra la diferencia y el monto destino en GTQ", () => {
    render(<TransferenciaMoneda origen={cuentaUsd} destino={cuentaGtq} fecha="2026-10-12" montoOrigen="1000" montoDestino="7600" onMontoDestino={vi.fn()} cambio={cambio} />);

    expect(screen.getByText("Diferencia vs. referencia: -Q41.57; se asienta a ganancia o pérdida realizada.")).toBeInTheDocument();
    expect(screen.getByLabelText("Monto destino en GTQ")).toBeInTheDocument();
  });

  it("notifica el monto destino tecleado", () => {
    const onMontoDestino = vi.fn();
    render(<TransferenciaMoneda origen={cuentaGtq} destino={cuentaUsd} fecha="2026-10-12" montoOrigen="7700" montoDestino="1000" onMontoDestino={onMontoDestino} cambio={cambio} />);

    fireEvent.change(screen.getByLabelText("Monto destino en USD"), { target: { value: "7650" } });

    expect(onMontoDestino).toHaveBeenCalledWith("7650");
  });

  it("sin tasa o sin montos no muestra la diferencia", () => {
    render(<TransferenciaMoneda origen={cuentaGtq} destino={cuentaUsd} fecha="2026-10-12" montoOrigen="7700" montoDestino="" onMontoDestino={vi.fn()} cambio={{ ...cambio, tasa: null }} />);

    expect(screen.queryByText(/Diferencia vs. referencia/)).toBeNull();
  });
});
