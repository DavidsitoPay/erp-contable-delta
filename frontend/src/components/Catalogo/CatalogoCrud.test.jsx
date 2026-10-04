import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { FormularioCatalogo, TablaCatalogo } from "./CatalogoCrud";

describe("CatalogoCrud", () => {
  it("envia el formulario y muestra el error", () => {
    const onSubmit = vi.fn((e) => e.preventDefault());
    render(<FormularioCatalogo onSubmit={onSubmit} cargando={false} error="Fallo"><input aria-label="Campo" /></FormularioCatalogo>);

    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));

    expect(onSubmit).toHaveBeenCalled();
    expect(screen.getByText("Fallo")).toBeInTheDocument();
  });

  it("deshabilita el boton mientras carga y no muestra error vacio", () => {
    render(<FormularioCatalogo onSubmit={vi.fn()} cargando error=""><input aria-label="Campo" /></FormularioCatalogo>);

    expect(screen.getByRole("button", { name: "Agregar" })).toBeDisabled();
    expect(document.querySelector(".error-chip")).toBeNull();
  });

  it("renderiza encabezados y filas", () => {
    render(<TablaCatalogo encabezados={["A", "B"]}><tr><td>1</td><td>2</td><td></td></tr></TablaCatalogo>);

    expect(screen.getByRole("columnheader", { name: "A" })).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
  });
});
