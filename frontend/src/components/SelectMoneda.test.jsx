import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SelectMoneda from "./SelectMoneda";

describe("SelectMoneda", () => {
  it("ofrece GTQ, USD y EUR por defecto y refleja el valor", () => {
    render(<SelectMoneda valor="USD" onChange={vi.fn()} />);

    const select = screen.getByRole("combobox", { name: "Moneda" });
    expect(select).toHaveValue("USD");
    expect([...select.options].map((o) => o.value)).toEqual(["GTQ", "USD", "EUR"]);
  });

  it("notifica el código elegido", () => {
    const onChange = vi.fn();
    render(<SelectMoneda valor="GTQ" onChange={onChange} />);

    fireEvent.change(screen.getByRole("combobox", { name: "Moneda" }), { target: { value: "USD" } });

    expect(onChange).toHaveBeenCalledWith("USD");
  });

  it("acepta una lista de opciones reducida", () => {
    render(<SelectMoneda valor="GTQ" onChange={vi.fn()} opciones={["GTQ", "USD"]} />);

    expect(screen.queryByRole("option", { name: "EUR" })).toBeNull();
  });

  it("se puede deshabilitar", () => {
    render(<SelectMoneda valor="GTQ" onChange={vi.fn()} deshabilitado />);

    expect(screen.getByRole("combobox", { name: "Moneda" })).toBeDisabled();
  });

  it("usa la etiqueta como nombre accesible", () => {
    render(<SelectMoneda valor="GTQ" onChange={vi.fn()} etiqueta="Moneda de la cuenta" />);

    expect(screen.getByRole("combobox", { name: "Moneda de la cuenta" })).toBeInTheDocument();
  });
});
