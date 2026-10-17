import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import VistaPreviaPartida from "./VistaPreviaPartida";

const vista = {
  lineas: [
    { cuentaId: 1, codigo: "2.1.03", nombre: "ISR no residente por pagar", debe: 150, haber: 0 },
    { cuentaId: 2, codigo: "1.1.01", nombre: "Banco", debe: 0, haber: 150 },
  ],
  saldoAntes: 150,
  saldoDespues: 0,
};

describe("VistaPreviaPartida", () => {
  it("muestra las líneas de la partida y el saldo por enterar antes y después", () => {
    render(<VistaPreviaPartida vista={vista} />);

    expect(screen.getByText("2.1.03")).toBeInTheDocument();
    expect(screen.getByText("Banco")).toBeInTheDocument();
    expect(screen.getByText(`Saldo por enterar: ${formatoMoneda.format(150)} → ${formatoMoneda.format(0)}`)).toBeInTheDocument();
  });
});
