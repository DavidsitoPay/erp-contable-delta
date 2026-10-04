import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { login } from "../services/api";
import Login from "./Login";

vi.mock("../services/api", () => ({ login: vi.fn() }));

function completarYEnviar(email = "ana@delta.test", password = "secreto") {
  fireEvent.change(screen.getByLabelText("Correo"), { target: { value: email } });
  fireEvent.change(screen.getByLabelText("Contraseña"), { target: { value: password } });
  fireEvent.click(screen.getByRole("button", { name: "Ingresar" }));
}

describe("Login", () => {
  beforeEach(() => {
    login.mockReset();
  });

  it("guarda token y usuario y avisa al padre cuando el login es exitoso", async () => {
    const usuario = { id: 1, nombre: "Ana", rol: "Contador" };
    login.mockResolvedValue({ data: { token: "jwt-123", usuario } });
    const onLoginExitoso = vi.fn();
    render(<Login onLoginExitoso={onLoginExitoso} />);

    completarYEnviar();

    await waitFor(() => expect(onLoginExitoso).toHaveBeenCalledWith(usuario));
    expect(login).toHaveBeenCalledWith("ana@delta.test", "secreto");
    expect(localStorage.getItem("delta_token")).toBe("jwt-123");
    expect(JSON.parse(localStorage.getItem("delta_usuario"))).toEqual(usuario);
  });

  it("muestra el mensaje del servidor y no guarda sesión cuando el login falla", async () => {
    login.mockRejectedValue({ response: { data: { error: "Credenciales inválidas." } } });
    const onLoginExitoso = vi.fn();
    render(<Login onLoginExitoso={onLoginExitoso} />);

    completarYEnviar();

    expect(await screen.findByRole("alert")).toHaveTextContent("Credenciales inválidas.");
    expect(onLoginExitoso).not.toHaveBeenCalled();
    expect(localStorage.getItem("delta_token")).toBeNull();
  });

  it("muestra un mensaje genérico cuando no hay respuesta del servidor", async () => {
    login.mockRejectedValue(new Error("Network Error"));
    render(<Login onLoginExitoso={vi.fn()} />);

    completarYEnviar();

    expect(await screen.findByRole("alert")).toHaveTextContent("No se pudo iniciar sesión.");
  });

  it("deshabilita el botón mientras la petición está en curso", async () => {
    let resolver;
    login.mockReturnValue(new Promise((resolve) => { resolver = resolve; }));
    render(<Login onLoginExitoso={vi.fn()} />);

    completarYEnviar();

    expect(await screen.findByRole("button", { name: "Ingresando..." })).toBeDisabled();
    resolver({ data: { token: "t", usuario: {} } });
    await waitFor(() => expect(screen.getByRole("button", { name: "Ingresar" })).toBeEnabled());
  });
});
