import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { aplicarTema } from "../tema";
import ToggleTema from "./ToggleTema";

beforeEach(() => {
  delete document.documentElement.dataset.theme;
});

afterEach(() => {
  vi.restoreAllMocks();
  delete document.documentElement.dataset.theme;
});

describe("ToggleTema", () => {
  it("muestra el boton para cambiar a modo oscuro por defecto", () => {
    render(<ToggleTema />);
    const button = screen.getByRole("button", { name: "Cambiar a modo oscuro" });
    expect(button).toHaveAttribute("aria-pressed", "false");
    expect(button).toHaveAttribute("title", "Cambiar a modo oscuro");
    const { container } = render(<ToggleTema />);
    expect(container.querySelector("circle")).not.toBeNull();
    expect(container.querySelector("path")).toBeNull();
  });

  it("arranca en modo oscuro si el documento ya lo tiene", () => {
    document.documentElement.dataset.theme = "oscuro";
    render(<ToggleTema />);
    const button = screen.getByRole("button", { name: "Cambiar a modo claro" });
    expect(button).toHaveAttribute("aria-pressed", "true");
    const { container } = render(<ToggleTema />);
    expect(container.querySelector("path")).not.toBeNull();
    expect(container.querySelector("circle")).toBeNull();
  });

  it("alterna el tema al hacer clic", () => {
    render(<ToggleTema />);
    const button = screen.getByRole("button");
    fireEvent.click(button);
    expect(document.documentElement.dataset.theme).toBe("oscuro");
    expect(localStorage.getItem("delta_tema")).toBe("oscuro");
    expect(button).toHaveAttribute("aria-pressed", "true");
    expect(button).toHaveAttribute("aria-label", "Cambiar a modo claro");
    fireEvent.click(button);
    expect(document.documentElement.dataset.theme).toBe("claro");
    expect(localStorage.getItem("delta_tema")).toBe("claro");
    expect(button).toHaveAttribute("aria-pressed", "false");
    expect(button).toHaveAttribute("aria-label", "Cambiar a modo oscuro");
  });

  it("se actualiza cuando otro codigo cambia el tema", () => {
    render(<ToggleTema />);
    const button = screen.getByRole("button");
    expect(button).toHaveAttribute("aria-pressed", "false");
    act(() => {
      aplicarTema("oscuro");
    });
    expect(button).toHaveAttribute("aria-pressed", "true");
    act(() => {
      aplicarTema("claro");
    });
    expect(button).toHaveAttribute("aria-pressed", "false");
  });

  it("quita el listener al desmontar", () => {
    const espia = vi.spyOn(window, "removeEventListener");
    const { unmount } = render(<ToggleTema />);
    unmount();
    expect(espia).toHaveBeenCalledWith("delta:tema-cambiado", expect.any(Function));
  });
});
