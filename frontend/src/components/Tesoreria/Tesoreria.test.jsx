import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { cuentasApi, cuentasBancariasApi } from "../../services/api";
import Tesoreria from "./Tesoreria";

vi.mock("../../services/api", () => ({ cuentasApi: { listar: vi.fn() }, cuentasBancariasApi: { listar: vi.fn() } }));
vi.mock("./CuentasBancarias", () => ({ default: ({ cuentasBancarias, cuentasContables }) => <p>Vista cuentas: {cuentasBancarias.length} bancarias, {cuentasContables.length} contables</p> }));
vi.mock("./Movimientos", () => ({ default: () => <p>Vista movimientos</p> }));
vi.mock("./Conciliaciones", () => ({ default: () => <p>Vista conciliaciones</p> }));

describe("Tesoreria", () => {
  beforeEach(() => {
    cuentasApi.listar.mockReset();
    cuentasBancariasApi.listar.mockReset();
    cuentasApi.listar.mockResolvedValue({ data: [{ id: 1 }, { id: 2 }] });
    cuentasBancariasApi.listar.mockResolvedValue({ data: [{ id: 7 }] });
  });

  it("carga los catálogos, incluidas las cuentas inactivas, y muestra Cuentas bancarias al inicio", async () => {
    render(<Tesoreria />);

    expect(await screen.findByText("Vista cuentas: 1 bancarias, 2 contables")).toBeInTheDocument();
    expect(cuentasBancariasApi.listar).toHaveBeenCalledWith(true);
    expect(cuentasApi.listar).toHaveBeenCalledWith(true);
  });

  it("cambia de sub-pestaña y recarga las cuentas bancarias al hacerlo", async () => {
    render(<Tesoreria />);
    await screen.findByText(/Vista cuentas/);

    fireEvent.click(screen.getByRole("button", { name: "Movimientos" }));
    expect(screen.getByText("Vista movimientos")).toBeInTheDocument();
    expect(screen.queryByText(/Vista cuentas/)).toBeNull();
    await waitFor(() => expect(cuentasBancariasApi.listar).toHaveBeenCalledTimes(2));

    fireEvent.click(screen.getByRole("button", { name: "Conciliaciones" }));
    expect(screen.getByText("Vista conciliaciones")).toBeInTheDocument();
    await waitFor(() => expect(cuentasBancariasApi.listar).toHaveBeenCalledTimes(3));
    expect(cuentasApi.listar).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "Cuentas bancarias" }));
    expect(await screen.findByText(/Vista cuentas/)).toBeInTheDocument();
  });
});
