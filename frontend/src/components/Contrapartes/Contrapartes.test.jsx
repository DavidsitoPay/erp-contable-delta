import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { contrapartesApi } from "../../services/api";
import Contrapartes from "./Contrapartes";

vi.mock("../../services/api", () => ({ contrapartesApi: { listar: vi.fn(), crear: vi.fn() } }));

const NIT_VALIDO = "1234567-9";
const clientes = [
  { id: 1, nombre: "ACME", nit: "123456", direccion: "Calle 1", regimenIva: "GENERAL", esResidente: true, esAgenteRetencionIva: false },
  { id: 2, nombre: "Otra Empresa", nit: "789012", direccion: "Calle 2", regimenIva: "EXENTO", esResidente: false, esAgenteRetencionIva: true },
];
const proveedores = [
  { id: 3, nombre: "Proveedor A", nit: "345678", direccion: "Calle 3", regimenIva: "PEQUENO_CONTRIBUYENTE", regimenIsr: "SIMPLIFICADO", esResidente: true, esAgenteRetencionIva: false },
];

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

async function mostrarProveedores() {
  await screen.findByText("ACME");
  fireEvent.click(screen.getByRole("button", { name: "Proveedores" }));
  await screen.findByText("Proveedor A");
}

describe("Contrapartes", () => {
  beforeEach(() => {
    contrapartesApi.listar.mockReset();
    contrapartesApi.crear.mockReset();
    contrapartesApi.listar.mockImplementation((tipo) => Promise.resolve({ data: tipo === "Cliente" ? clientes : proveedores }));
    contrapartesApi.crear.mockResolvedValue({ data: {} });
  });

  it("carga y renderiza la lista de clientes al inicio", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    expect(contrapartesApi.listar).toHaveBeenCalledWith("Cliente");
    expect(screen.getByText("ACME")).toBeInTheDocument();
    expect(screen.getByText("Otra Empresa")).toBeInTheDocument();
  });

  it("muestra el régimen de IVA, la residencia y la condición de agente de cada cliente", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    const tabla = within(screen.getByRole("table"));
    expect(tabla.getByText("General")).toBeInTheDocument();
    expect(tabla.getByText("Exento")).toBeInTheDocument();
    expect(tabla.getAllByText("Sí")).toHaveLength(2);
    expect(tabla.getAllByText("No")).toHaveLength(2);
    expect(screen.queryByRole("columnheader", { name: "Régimen ISR" })).toBeNull();
  });

  it("filtra por tipo y para proveedores agrega la columna de régimen de ISR", async () => {
    render(<Contrapartes />);
    await mostrarProveedores();

    expect(contrapartesApi.listar).toHaveBeenLastCalledWith("Proveedor");
    expect(screen.getByRole("columnheader", { name: "Régimen ISR" })).toBeInTheDocument();
    const tabla = within(screen.getByRole("table"));
    expect(tabla.getByText("Pequeño contribuyente")).toBeInTheDocument();
    expect(tabla.getByText("Opcional simplificado sobre ingresos (SIMPLIFICADO)")).toBeInTheDocument();
  });

  it("muestra guion cuando el servidor no informa el régimen", async () => {
    contrapartesApi.listar.mockResolvedValue({ data: [{ id: 9, nombre: "Sin datos", nit: "", direccion: "" }] });
    render(<Contrapartes />);

    expect(await screen.findByText("Sin datos")).toBeInTheDocument();
    expect(within(screen.getByRole("table")).getAllByText("—")).toHaveLength(3);
  });

  it("crea una contraparte con los valores fiscales por defecto y recarga la lista", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "Nueva Empresa");
    escribir("NIT", NIT_VALIDO);
    escribir("Dirección", "Calle Nueva");
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() => expect(contrapartesApi.crear).toHaveBeenCalledTimes(1));
    expect(contrapartesApi.crear).toHaveBeenCalledWith({
      tipo: "Cliente",
      nombre: "Nueva Empresa",
      nit: NIT_VALIDO,
      direccion: "Calle Nueva",
      regimenIva: "GENERAL",
      regimenIsr: "UTILIDADES",
      esResidente: true,
      esAgenteRetencionIva: false,
    });
    await waitFor(() => expect(contrapartesApi.listar).toHaveBeenCalledTimes(2));
  });

  it("envía los campos fiscales editados de un cliente y no ofrece régimen de ISR", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");
    expect(screen.queryByLabelText("Régimen de ISR")).toBeNull();

    escribir("Nombre", "Cliente exento");
    escribir("Régimen de IVA", "EXENTO");
    fireEvent.click(screen.getByLabelText("Residente en Guatemala"));
    fireEvent.click(screen.getByLabelText("Agente de retención de IVA"));
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() =>
      expect(contrapartesApi.crear).toHaveBeenCalledWith(
        expect.objectContaining({ nombre: "Cliente exento", nit: "", regimenIva: "EXENTO", esResidente: false, esAgenteRetencionIva: true })
      )
    );
  });

  it("crea un proveedor con su régimen de ISR", async () => {
    render(<Contrapartes />);
    await mostrarProveedores();

    escribir("Nombre", "Proveedor Nuevo");
    escribir("NIT", NIT_VALIDO);
    escribir("Régimen de IVA", "PEQUENO_CONTRIBUYENTE");
    escribir("Régimen de ISR", "SIMPLIFICADO");
    fireEvent.click(screen.getByRole("button", { name: "Agregar proveedor" }));

    await waitFor(() =>
      expect(contrapartesApi.crear).toHaveBeenCalledWith({
        tipo: "Proveedor",
        nombre: "Proveedor Nuevo",
        nit: NIT_VALIDO,
        direccion: "",
        regimenIva: "PEQUENO_CONTRIBUYENTE",
        regimenIsr: "SIMPLIFICADO",
        esResidente: true,
        esAgenteRetencionIva: false,
      })
    );
  });

  it("resetea el formulario después de crear exitosamente", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "Nueva Empresa");
    escribir("NIT", NIT_VALIDO);
    escribir("Régimen de IVA", "EXENTO");
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() => expect(contrapartesApi.crear).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByLabelText("Nombre")).toHaveValue(""));
    expect(screen.getByLabelText("NIT")).toHaveValue("");
    expect(screen.getByLabelText("Dirección")).toHaveValue("");
    expect(screen.getByLabelText("Régimen de IVA")).toHaveValue("GENERAL");
  });

  it("avisa y bloquea el envío cuando el dígito verificador del NIT es incorrecto", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "Nueva");
    escribir("NIT", "1234567-8");

    expect(screen.getByText("El NIT no es válido (dígito verificador incorrecto).")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Agregar cliente" })).toBeDisabled();

    escribir("NIT", NIT_VALIDO);
    expect(screen.queryByText("El NIT no es válido (dígito verificador incorrecto).")).toBeNull();
    expect(screen.getByRole("button", { name: "Agregar cliente" })).toBeEnabled();
  });

  it("un cliente puede quedar sin NIT o con CF y se envía en mayúsculas", async () => {
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "Consumidor");
    expect(screen.getByRole("button", { name: "Agregar cliente" })).toBeEnabled();
    escribir("NIT", " cf ");
    expect(screen.getByRole("button", { name: "Agregar cliente" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    await waitFor(() => expect(contrapartesApi.crear).toHaveBeenCalledWith(expect.objectContaining({ nit: "CF" })));
  });

  it("un proveedor exige NIT y rechaza CF", async () => {
    render(<Contrapartes />);
    await mostrarProveedores();

    expect(screen.getByText("El NIT del proveedor es obligatorio.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Agregar proveedor" })).toBeDisabled();

    escribir("NIT", "CF");
    expect(screen.getByText("CF solo es válido para clientes.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Agregar proveedor" })).toBeDisabled();
  });

  it("muestra el mensaje de error cuando la creación falla", async () => {
    contrapartesApi.crear.mockRejectedValue({ response: { data: { error: "Nombre duplicado." } } });
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "ACME");
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    expect(await screen.findByText("Nombre duplicado.")).toBeInTheDocument();
  });

  it("muestra un mensaje genérico cuando falla sin respuesta del servidor", async () => {
    contrapartesApi.crear.mockRejectedValue(new Error("Network Error"));
    render(<Contrapartes />);
    await screen.findByText("ACME");

    escribir("Nombre", "Nueva");
    fireEvent.click(screen.getByRole("button", { name: "Agregar cliente" }));

    expect(await screen.findByText("No se pudo crear el/la cliente.")).toBeInTheDocument();
  });
});
