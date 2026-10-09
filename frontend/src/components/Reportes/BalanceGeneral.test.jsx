import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { periodosApi, reportesApi } from "../../services/api";
import { PERIODOS, celdasDe, cuentaReporte, periodoReporte } from "../../test/reportesFixtures";
import BalanceGeneral from "./BalanceGeneral";

vi.mock("../../services/api", () => ({
  periodosApi: { listar: vi.fn() },
  reportesApi: { balanceGeneral: vi.fn() },
}));

const REPORTE = {
  periodo: periodoReporte(),
  fuente: "Preliminar",
  incluyePeriodosAbiertos: false,
  activo: {
    total: 500,
    cuentas: [
      cuentaReporte({ cuentaId: 1, codigo: "1", nombre: "Activo", esHoja: false, saldo: 500 }),
      cuentaReporte({ cuentaId: 2, codigo: "1.1", nombre: "Caja", nivel: 2, saldo: 500 }),
      cuentaReporte({ cuentaId: 3, codigo: "1.2", nombre: "Banco", nivel: 2, saldo: 0 }),
    ],
  },
  pasivo: {
    total: 200,
    cuentas: [
      cuentaReporte({ cuentaId: 4, codigo: "2", nombre: "Pasivo", esHoja: false, saldo: 200 }),
      cuentaReporte({ cuentaId: 5, codigo: "2.1", nombre: "Proveedores", nivel: 2, saldo: 200 }),
    ],
  },
  capital: {
    total: 250,
    cuentas: [
      cuentaReporte({ cuentaId: 6, codigo: "3", nombre: "Capital contable", esHoja: false, saldo: 250 }),
      cuentaReporte({ cuentaId: 7, codigo: "3.1", nombre: "Aporte", nivel: 2, saldo: 250 }),
    ],
  },
  resultadoEjercicio: 50,
  totalCapital: 300,
  totalPasivoCapital: 500,
  diferencia: 0,
  cuadra: true,
};

async function cargar(reporte = REPORTE) {
  reportesApi.balanceGeneral.mockResolvedValue({ data: reporte });
  render(<BalanceGeneral />);
  await screen.findByText("Total capital");
}

beforeEach(() => {
  periodosApi.listar.mockReset();
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  reportesApi.balanceGeneral.mockReset();
});

describe("BalanceGeneral", () => {
  it("consulta el periodo mas reciente", async () => {
    await cargar();

    expect(reportesApi.balanceGeneral).toHaveBeenCalledWith(2);
  });

  it("muestra secciones, resultado del ejercicio y totales", async () => {
    await cargar();

    expect(celdasDe("Total Activo")).toEqual(["Total Activo", "500.00"]);
    expect(celdasDe("Total Pasivo")).toEqual(["Total Pasivo", "200.00"]);
    expect(celdasDe("Resultado del ejercicio (no distribuido)")).toEqual(["Resultado del ejercicio (no distribuido)", "50.00"]);
    expect(celdasDe("Total capital")).toEqual(["Total capital", "300.00"]);
    expect(celdasDe("Total pasivo + capital")).toEqual(["Total pasivo + capital", "500.00"]);
  });

  it("oculta cuentas en cero por defecto y las muestra con la casilla", async () => {
    await cargar();
    expect(screen.queryByText("1.2 - Banco")).toBeNull();

    fireEvent.click(screen.getByLabelText("Mostrar cuentas en cero"));

    expect(screen.getByText("1.2 - Banco")).toBeInTheDocument();
  });

  it("muestra Cuadra cuando el balance cuadra", async () => {
    await cargar();

    expect(screen.getByText("Cuadra")).toHaveClass("text-success");
    expect(screen.queryByText("Diferencia")).toBeNull();
  });

  it("muestra la diferencia en rojo cuando no cuadra", async () => {
    await cargar({ ...REPORTE, cuadra: false, diferencia: -25.5 });

    expect(celdasDe("Diferencia")).toEqual(["Diferencia", "25.50"]);
    expect(screen.getByText("25.50")).toHaveClass("text-danger");
    expect(screen.queryByText("Cuadra")).toBeNull();
  });

  it("muestra la fuente de cierre y el aviso de periodos abiertos", async () => {
    await cargar({
      ...REPORTE,
      fuente: "Cierre",
      periodo: periodoReporte({ cierres: 1, estado: "Cerrado" }),
      incluyePeriodosAbiertos: true,
    });

    expect(screen.getByText("Periodo cerrado — cierre 1")).toBeInTheDocument();
    expect(screen.getByText(/Incluye periodos abiertos anteriores/)).toBeInTheDocument();
  });

  it("muestra el error cuando falla la consulta", async () => {
    reportesApi.balanceGeneral.mockRejectedValue(new Error("red"));
    render(<BalanceGeneral />);

    expect(await screen.findByText("No se pudo cargar el balance general.")).toBeInTheDocument();
  });
});
