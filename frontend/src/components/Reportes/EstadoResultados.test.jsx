import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { periodosApi, reportesApi } from "../../services/api";
import { PERIODOS, celdasDe, cuentaReporte, periodoReporte } from "../../test/reportesFixtures";
import EstadoResultados from "./EstadoResultados";

vi.mock("../../services/api", () => ({
  periodosApi: { listar: vi.fn() },
  reportesApi: { estadoResultados: vi.fn() },
}));

const REPORTE = {
  periodo: periodoReporte(),
  fuente: "Preliminar",
  ingresos: {
    total: 800,
    cuentas: [
      cuentaReporte({ cuentaId: 10, codigo: "4", nombre: "Ingresos", esHoja: false, saldo: 800 }),
      cuentaReporte({ cuentaId: 11, codigo: "4.1", nombre: "Ventas", nivel: 2, saldo: 800 }),
    ],
  },
  gastos: {
    total: 300,
    cuentas: [
      cuentaReporte({ cuentaId: 12, codigo: "5", nombre: "Gastos", esHoja: false, saldo: 300 }),
      cuentaReporte({ cuentaId: 13, codigo: "5.1", nombre: "Sueldos", nivel: 2, saldo: 300 }),
      cuentaReporte({ cuentaId: 14, codigo: "5.2", nombre: "Varios", nivel: 2, saldo: 0 }),
    ],
  },
  utilidadNeta: 500,
};

async function cargar(reporte = REPORTE) {
  reportesApi.estadoResultados.mockResolvedValue({ data: reporte });
  render(<EstadoResultados />);
  await screen.findByText("Total Ingresos");
}

beforeEach(() => {
  periodosApi.listar.mockReset();
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  reportesApi.estadoResultados.mockReset();
});

describe("EstadoResultados", () => {
  it("consulta el periodo mas reciente", async () => {
    await cargar();

    expect(reportesApi.estadoResultados).toHaveBeenCalledWith(2);
  });

  it("muestra ingresos, gastos y utilidad neta", async () => {
    await cargar();

    expect(celdasDe("Total Ingresos")).toEqual(["Total Ingresos", "800.00"]);
    expect(celdasDe("Total Gastos")).toEqual(["Total Gastos", "300.00"]);
    expect(celdasDe("Utilidad neta")).toEqual(["Utilidad neta", "500.00"]);
    expect(screen.queryByText("Pérdida neta")).toBeNull();
  });

  it("muestra la perdida neta en valor absoluto y en rojo", async () => {
    await cargar({ ...REPORTE, utilidadNeta: -120 });

    expect(celdasDe("Pérdida neta")).toEqual(["Pérdida neta", "120.00"]);
    expect(screen.getByText("120.00")).toHaveClass("text-danger");
    expect(screen.queryByText("Utilidad neta")).toBeNull();
  });

  it("presenta utilidad cero como Utilidad neta", async () => {
    await cargar({ ...REPORTE, utilidadNeta: 0 });

    expect(celdasDe("Utilidad neta")).toEqual(["Utilidad neta", "0.00"]);
  });

  it("oculta cuentas en cero por defecto y las muestra con la casilla", async () => {
    await cargar();
    expect(screen.queryByText("5.2 - Varios")).toBeNull();

    fireEvent.click(screen.getByLabelText("Mostrar cuentas en cero"));

    expect(screen.getByText("5.2 - Varios")).toBeInTheDocument();
  });

  it("muestra el error cuando falla la consulta", async () => {
    reportesApi.estadoResultados.mockRejectedValue(new Error("red"));
    render(<EstadoResultados />);

    expect(await screen.findByText("No se pudo cargar el estado de resultados.")).toBeInTheDocument();
  });
});
