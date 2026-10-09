import { screen, within } from "@testing-library/react";

export const PERIODOS = [
  { id: 1, nombre: "Septiembre 2026", fechaInicio: "2026-09-01", fechaFin: "2026-09-30", estado: "Cerrado" },
  { id: 2, nombre: "Octubre 2026", fechaInicio: "2026-10-01", fechaFin: "2026-10-31", estado: "Abierto" },
];

export const periodoReporte = (o) => ({ ...PERIODOS[1], cierres: 0, ...o });

export const cuentaReporte = (o) => ({ nivel: 1, esHoja: true, saldo: 0, ...o });

export const celdasDe = (texto) =>
  within(screen.getByText(texto).closest("tr")).getAllByRole("cell").map((c) => c.textContent);
