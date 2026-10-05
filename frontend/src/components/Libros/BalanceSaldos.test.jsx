import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { librosApi } from "../../services/api";
import BalanceSaldos from "./BalanceSaldos";

vi.mock("../../services/api", () => ({ librosApi: { balanceSaldos: vi.fn() } }));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const fila = (o) => ({
  naturaleza: "Deudora",
  totalDebito: 0,
  totalCredito: 0,
  saldo: 0,
  nivel: 1,
  cuentaPadreId: null,
  esHoja: true,
  activa: true,
  debitoPropio: 0,
  creditoPropio: 0,
  ...o,
});

const FILAS = [
  fila({ cuentaId: 1, codigo: "1", nombre: "Activo", totalDebito: 300, totalCredito: 100, saldo: 200, esHoja: false }),
  fila({ cuentaId: 2, codigo: "1.1", nombre: "Caja", totalDebito: 300, totalCredito: 100, saldo: 200, nivel: 2, cuentaPadreId: 1 }),
  fila({ cuentaId: 3, codigo: "2", nombre: "Pasivo", naturaleza: "Acreedora", totalDebito: 20, totalCredito: 70, saldo: 50, esHoja: false }),
  fila({ cuentaId: 4, codigo: "2.1", nombre: "Proveedores", naturaleza: "Acreedora", totalDebito: 20, totalCredito: 70, saldo: 50, nivel: 2, cuentaPadreId: 3 }),
];

const celdasDe = (texto) => within(screen.getByText(texto).closest("tr")).getAllByRole("cell").map((c) => c.textContent);

async function cargar(filas = FILAS) {
  librosApi.balanceSaldos.mockResolvedValue({ data: filas });
  render(<BalanceSaldos />);
  await screen.findByText("Totales");
}

beforeEach(() => {
  librosApi.balanceSaldos.mockReset();
  librosApi.balanceSaldos.mockResolvedValue({ data: FILAS });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("BalanceSaldos", () => {
  it("muestra Cargando mientras espera y luego la tabla", async () => {
    const d = deferred();
    librosApi.balanceSaldos.mockReturnValue(d.promise);
    render(<BalanceSaldos />);
    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    await act(async () => {
      d.resolve({ data: FILAS });
    });
    await screen.findByText("Caja");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("lista las cuentas con saldo deudor y acreedor", async () => {
    await cargar();
    expect(celdasDe("Caja")).toEqual(["1.1", "Caja", "Deudora", "300.00", "100.00", "200.00", "0.00"]);
    expect(celdasDe("Proveedores")).toEqual(["2.1", "Proveedores", "Acreedora", "20.00", "70.00", "0.00", "50.00"]);
    expect(screen.getByRole("heading", { name: "Balance de saldos" })).toBeInTheDocument();
  });

  it("muestra 0.00 en una cuenta sin movimiento", async () => {
    await cargar([fila({ cuentaId: 9, codigo: "3.1", nombre: "Capital", naturaleza: "Acreedora" })]);
    expect(celdasDe("Capital")).toEqual(["3.1", "Capital", "Acreedora", "0.00", "0.00", "0.00", "0.00"]);
  });

  it("indenta por nivel, marca padres e inactivas", async () => {
    await cargar([...FILAS, fila({ cuentaId: 5, codigo: "1.2", nombre: "Banco", nivel: 2, activa: false })]);
    const padre = screen.getByText("Activo").closest("tr");
    const hija = screen.getByText("Caja").closest("tr");
    expect(padre).toHaveClass("row-parent");
    expect(hija).not.toHaveClass("row-parent");
    expect(within(padre).getByText("1")).toHaveStyle({ paddingLeft: "0.75rem" });
    expect(within(hija).getByText("1.1")).toHaveStyle({ paddingLeft: "2rem" });
    expect(screen.getByText("Banco (inactiva)")).toBeInTheDocument();
  });

  it("totaliza solo las cuentas de nivel 1 y señala la diferencia en rojo", async () => {
    await cargar();
    expect(celdasDe("Totales")).toEqual(["Totales", "320.00", "170.00", "200.00", "50.00"]);
    expect(celdasDe("Diferencia")).toEqual(["Diferencia", "150.00"]);
    expect(screen.getByText("150.00")).toHaveClass("text-danger");
  });

  it("no muestra la diferencia cuando débitos y créditos cuadran", async () => {
    await cargar([
      fila({ cuentaId: 1, codigo: "1", nombre: "Caja", totalDebito: 0.1, saldo: 0.1 }),
      fila({ cuentaId: 2, codigo: "2", nombre: "Banco", totalDebito: 0.2, saldo: 0.2 }),
      fila({ cuentaId: 3, codigo: "3", nombre: "Capital", naturaleza: "Acreedora", totalCredito: 0.3, saldo: 0.3 }),
    ]);
    expect(celdasDe("Totales")).toEqual(["Totales", "0.30", "0.30", "0.30", "0.30"]);
    expect(screen.queryByText("Diferencia")).toBeNull();
  });

  it("oculta las cuentas sin movimiento sin alterar los totales", async () => {
    await cargar([...FILAS, fila({ cuentaId: 7, codigo: "3", nombre: "Capital", naturaleza: "Acreedora" })]);
    expect(screen.getByText("Capital")).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("Ocultar cuentas sin movimiento"));
    expect(screen.queryByText("Capital")).toBeNull();
    expect(screen.getByText("Caja")).toBeInTheDocument();
    expect(celdasDe("Totales")[1]).toBe("320.00");
  });

  it("filtra por nivel máximo sin alterar los totales", async () => {
    await cargar();
    const selector = screen.getByLabelText("Nivel máximo");
    expect(within(selector).getAllByRole("option").map((o) => o.textContent)).toEqual(["Todos los niveles", "1", "2"]);
    fireEvent.change(selector, { target: { value: "1" } });
    expect(screen.queryByText("Caja")).toBeNull();
    expect(screen.getByText("Activo")).toBeInTheDocument();
    expect(celdasDe("Totales")[1]).toBe("320.00");
  });

  it("muestra el error del servidor", async () => {
    librosApi.balanceSaldos.mockRejectedValue({ response: { data: { error: "Sin permisos" } } });
    render(<BalanceSaldos />);
    await screen.findByText("Sin permisos");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("muestra el mensaje por defecto si el error no trae respuesta", async () => {
    librosApi.balanceSaldos.mockRejectedValue(new Error("red"));
    render(<BalanceSaldos />);
    expect(await screen.findByText("No se pudo cargar el balance de saldos.")).toBeInTheDocument();
  });
});
