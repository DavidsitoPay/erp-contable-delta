import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import MenuPrincipal from "./MenuPrincipal";

const grupos = [
  { id: "catalogo", titulo: "Catálogo", items: [{ to: "/catalogo/cuentas", texto: "Cuentas contables" }] },
  {
    id: "libros",
    titulo: "Libros",
    items: [
      { to: "/libros/diario", texto: "Libro diario" },
      { to: "/libros/mayor", texto: "Libro mayor" },
    ],
  },
];

function renderMenu(ruta = "/") {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <MenuPrincipal grupos={grupos} />
    </MemoryRouter>
  );
}

function boton(nombre) {
  return screen.getByRole("button", { name: nombre });
}

describe("MenuPrincipal", () => {
  it("marca el grupo de la ruta actual con aria-current", () => {
    renderMenu("/libros/diario");

    expect(boton("Libros")).toHaveAttribute("aria-current", "true");
    expect(boton("Catálogo")).not.toHaveAttribute("aria-current");
  });

  it("abre el panel al primer clic y lo cierra al segundo", () => {
    renderMenu();

    fireEvent.click(boton("Catálogo"));
    expect(boton("Catálogo")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("link", { name: "Cuentas contables" })).toBeInTheDocument();

    fireEvent.click(boton("Catálogo"));
    expect(boton("Catálogo")).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("link", { name: "Cuentas contables" })).toBeNull();
  });

  it("Escape cierra el panel y devuelve el foco al botón", () => {
    renderMenu();

    fireEvent.click(boton("Libros"));
    fireEvent.keyDown(screen.getByRole("navigation"), { key: "Escape" });

    expect(boton("Libros")).toHaveAttribute("aria-expanded", "false");
    expect(boton("Libros")).toHaveFocus();
  });

  it("un clic fuera del menú cierra el panel", () => {
    renderMenu();

    fireEvent.click(boton("Libros"));
    fireEvent.mouseDown(document.body);

    expect(boton("Libros")).toHaveAttribute("aria-expanded", "false");
  });

  it("navegar desde un enlace cierra el panel", () => {
    renderMenu();

    fireEvent.click(boton("Libros"));
    fireEvent.click(screen.getByRole("link", { name: "Libro diario" }));

    expect(boton("Libros")).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("link", { name: "Libro mayor" })).toBeNull();
  });
});
