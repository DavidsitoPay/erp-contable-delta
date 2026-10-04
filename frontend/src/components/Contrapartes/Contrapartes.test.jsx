import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { contrapartesApi } from "../../services/api";
import Contrapartes from "./Contrapartes";

vi.mock("../../services/api", () => ({ contrapartesApi: { listar: vi.fn(), crear: vi.fn() } }));

const clientes = [
  { id: 1, nombre: "ACME", nit: "123456", direccion: "Calle 1" },
  { id: 2, nombre: "Otra Empresa", nit: "789012", direccion: "Calle 2" },
];
const proveedores = [
  { id: 3, nombre: "Proveedor A", nit: "345678", direccion: "Calle 3" },
];

describe("Contrapartes", () => {
  beforeEach(() => {
    contrapartesApi.listar.mockReset();
    contrapartesApi.crear.mockReset();
    contrapartesApi.listar.mockResolvedValue({ data: clientes });
    contrapartesApi.crear.mockResolvedValue({ data: {} });
  });

  it("carga y renderiza la lista de clientes al inicio", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    expect(contrapartesApi.listar).toHaveBeenCalledWith("Cliente");
    expect(screen.getByText("ACME")).toBeInTheDocument();
    expect(screen.getByText("Otra Empresa")).toBeInTheDocument();
  });

  it("filtra por tipo cuando se cambia el selector", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    contrapartesApi.listar.mockResolvedValue({ data: proveedores });
    fireEvent.click(screen.getByRole("button", { name: "Proveedores" }));

    await screen.findByText("Proveedor A");
    expect(contrapartesApi.listar).toHaveBeenLastCalledWith("Proveedor");
  });

  it("crea una contraparte y recarga la lista", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Nueva Empresa" } });
    fireEvent.change(screen.getByLabelText("NIT"), { target: { value: "999999" } });
    fireEvent.change(screen.getByLabelText("Dirección"), { target: { value: "Calle Nueva" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() => expect(contrapartesApi.crear).toHaveBeenCalledTimes(1));
    expect(contrapartesApi.crear).toHaveBeenCalledWith({
      tipo: "Cliente",
      nombre: "Nueva Empresa",
      nit: "999999",
      direccion: "Calle Nueva",
    });
  });

  it("resetea el formulario después de crear exitosamente", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Nueva Empresa" } });
    fireEvent.change(screen.getByLabelText("NIT"), { target: { value: "999999" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() => expect(contrapartesApi.crear).toHaveBeenCalled());
    expect(screen.getByLabelText("Nombre")).toHaveValue("");
    expect(screen.getByLabelText("NIT")).toHaveValue("");
    expect(screen.getByLabelText("Dirección")).toHaveValue("");
  });

  it("muestra el mensaje de error cuando la creación falla", async () => {
    contrapartesApi.crear.mockRejectedValue({ response: { data: { error: "Nombre duplicado." } } });
    render(<Contrapartes />);
    await screen.findByText("ACME");

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "ACME" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    expect(await screen.findByText("Nombre duplicado.")).toBeInTheDocument();
  });

  it("muestra un mensaje genérico cuando falla sin respuesta del servidor", async () => {
    contrapartesApi.crear.mockRejectedValue(new Error("Network Error"));
    render(<Contrapartes />);
    await screen.findByText("ACME");

    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Nueva" } });
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    expect(await screen.findByText("No se pudo crear el/la cliente.")).toBeInTheDocument();
  });
});
