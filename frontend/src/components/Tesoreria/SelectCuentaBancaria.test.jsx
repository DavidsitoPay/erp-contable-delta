import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SelectCuentaBancaria from "./SelectCuentaBancaria";

const cuentas = [
  { id: 7, banco: "BAC", numero: "123", activa: true },
  { id: 8, banco: "Viejo", numero: "000", activa: false },
];

describe("SelectCuentaBancaria", () => {
  it("por defecto es obligatorio y solo ofrece cuentas activas", () => {
    const onChange = vi.fn();
    render(<SelectCuentaBancaria etiqueta="Cuenta bancaria" valor="" onChange={onChange} cuentasBancarias={cuentas} />);

    const select = screen.getByLabelText("Cuenta bancaria");
    expect(select).toBeRequired();
    expect(within(select).getByRole("option", { name: "Selecciona cuenta bancaria" })).toBeInTheDocument();
    expect(within(select).queryByRole("option", { name: "Viejo 000" })).toBeNull();

    fireEvent.change(select, { target: { value: "7" } });
    expect(onChange).toHaveBeenCalledWith("7");
  });

  it("como filtro opcional incluye inactivas y la opción Todas las cuentas", () => {
    render(<SelectCuentaBancaria etiqueta="Filtrar por cuenta" valor="" onChange={vi.fn()} cuentasBancarias={cuentas} soloActivas={false} requerido={false} />);

    const select = screen.getByLabelText("Filtrar por cuenta");
    expect(select).not.toBeRequired();
    expect(within(select).getByRole("option", { name: "Todas las cuentas" })).toBeInTheDocument();
    expect(within(select).getByRole("option", { name: "Viejo 000" })).toBeInTheDocument();
  });
});
