import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoImporte, formatoMoneda } from "../../utils/formato";
import TablaPartidasRevaluacion from "./TablaPartidasRevaluacion";

const partidas = [
  { tipo: "CxC", referencia: "F-100", saldoOriginal: 1000, valorLibrosGtq: 7600, valorCierreGtq: 7650, ajusteGtq: 50, efecto: "Ganancia" },
  { tipo: "CxP", referencia: "P-50", saldoOriginal: 500, valorLibrosGtq: 3830, valorCierreGtq: 3825, ajusteGtq: -5, efecto: "Ganancia" },
  { tipo: "Banco", referencia: "Banco USD", saldoOriginal: 2000, valorLibrosGtq: 15400, valorCierreGtq: 15300, ajusteGtq: -100, efecto: "Perdida" },
];

describe("TablaPartidasRevaluacion", () => {
  it("muestra saldo, valores, ajuste con signo y efecto de cada partida", () => {
    render(<TablaPartidasRevaluacion partidas={partidas} moneda="USD" />);

    const filaCxc = within(screen.getByText("F-100").closest("tr"));
    expect(filaCxc.getByText(formatoImporte(1000, "USD"))).toBeInTheDocument();
    expect(filaCxc.getByText(formatoMoneda.format(7600))).toBeInTheDocument();
    expect(filaCxc.getByText(formatoMoneda.format(7650))).toBeInTheDocument();
    expect(filaCxc.getByText("+Q50.00")).toBeInTheDocument();
    expect(screen.getByText("-Q5.00")).toBeInTheDocument();
    expect(screen.getByText("-Q100.00")).toBeInTheDocument();
  });

  it("rotula la pérdida con tilde", () => {
    render(<TablaPartidasRevaluacion partidas={partidas} moneda="USD" />);

    expect(within(screen.getByText("Banco USD").closest("tr")).getByText("Pérdida")).toBeInTheDocument();
  });

  it("sin partidas muestra el mensaje", () => {
    render(<TablaPartidasRevaluacion partidas={[]} moneda="USD" />);

    expect(screen.getByText("No hay partidas con diferencia.")).toBeInTheDocument();
  });
});
