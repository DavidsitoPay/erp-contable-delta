import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { cuentasApi } from "../../services/api";
import CuentasContables from "./CuentasContables";

vi.mock("../../services/api", () => ({ cuentasApi: { listar: vi.fn(), crear: vi.fn(), desactivar: vi.fn() } }));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const base = { naturaleza: "Deudora", cuentaPadreId: null };
const JERARQUIA = [
  { ...base, id: 1, codigo: "1", nombre: "Activo", tipo: "Activo" },
  { ...base, id: 2, codigo: "1.1", nombre: "Caja", tipo: "Activo", cuentaPadreId: 1 },
  { ...base, id: 3, codigo: "1.1.1", nombre: "Caja chica", tipo: "Activo", cuentaPadreId: 2 },
  { ...base, id: 4, codigo: "2", nombre: "Pasivo", tipo: "Pasivo", naturaleza: "Acreedora" },
];

beforeEach(() => {
  Object.values(cuentasApi).forEach((fn) => fn.mockReset());
  cuentasApi.listar.mockResolvedValue({ data: JERARQUIA });
  cuentasApi.crear.mockResolvedValue({ data: {} });
  cuentasApi.desactivar.mockResolvedValue({ data: {} });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

async function renderizar(cantidad = JERARQUIA.length) {
  render(<CuentasContables />);
  await screen.findByText(`Mostrando ${cantidad} de ${cantidad} cuentas`);
}

const filas = () => screen.getAllByRole("row").slice(1);
const codigos = () => filas().map((f) => f.cells[0].textContent);
const celdaCodigo = (codigo) => filas().map((f) => f.cells[0]).find((c) => c.textContent === codigo);
const escribir = (etiqueta, valor) => fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });

describe("listado y jerarquia", () => {
  it("lista las cuentas en orden jerarquico por codigo", async () => {
    await renderizar();
    expect(codigos()).toEqual(["1", "1.1", "1.1.1", "2"]);
    expect(cuentasApi.listar).toHaveBeenCalledWith(false);
    expect(filas()[1].cells[4].textContent).toBe("1");
    expect(filas()[0].cells[4].textContent).toBe("-");
  });

  it("indenta segun la profundidad", async () => {
    await renderizar();
    expect(celdaCodigo("1").style.paddingLeft).toBe("0.75rem");
    expect(celdaCodigo("1.1").style.paddingLeft).toBe("2rem");
    expect(celdaCodigo("1.1.1").style.paddingLeft).toBe("3.25rem");
    expect(celdaCodigo("2").style.paddingLeft).toBe("0.75rem");
  });

  it("resalta con negrita las cuentas con hijos", async () => {
    await renderizar();
    expect(celdaCodigo("1").style.fontWeight).toBe("600");
    expect(celdaCodigo("1.1").style.fontWeight).toBe("600");
    expect(celdaCodigo("1.1.1").style.fontWeight).toBe("400");
    expect(celdaCodigo("2").style.fontWeight).toBe("400");
  });

  it("no indenta una cuenta cuyo padre no existe", async () => {
    cuentasApi.listar.mockResolvedValue({ data: [{ ...base, id: 5, codigo: "X", nombre: "Huerfana", tipo: "Gasto", cuentaPadreId: 99 }] });
    await renderizar(1);
    expect(celdaCodigo("X").style.paddingLeft).toBe("0.75rem");
    expect(filas()[0].cells[4].textContent).toBe("-");
  });

  it("limita la profundidad ante ciclos de padres", async () => {
    cuentasApi.listar.mockResolvedValue({
      data: [
        { ...base, id: 5, codigo: "A", nombre: "Alfa", tipo: "Gasto", cuentaPadreId: 6 },
        { ...base, id: 6, codigo: "B", nombre: "Beta", tipo: "Gasto", cuentaPadreId: 5 },
      ],
    });
    await renderizar(2);
    expect(celdaCodigo("A").style.paddingLeft).toBe("13.25rem");
    expect(celdaCodigo("B").style.paddingLeft).toBe("13.25rem");
  });

  it("muestra el conteo de cuentas", async () => {
    await renderizar();
    expect(screen.getByText("Mostrando 4 de 4 cuentas")).toBeInTheDocument();
  });
});

describe("busqueda y filtro", () => {
  it("busca por nombre sin distinguir mayusculas y aplana la jerarquia", async () => {
    await renderizar();
    escribir("Buscar cuenta", "CAJA");
    expect(codigos()).toEqual(["1.1", "1.1.1"]);
    expect(celdaCodigo("1.1.1").style.paddingLeft).toBe("0.75rem");
    expect(screen.getByText("Mostrando 2 de 4 cuentas")).toBeInTheDocument();
  });

  it("busca por codigo", async () => {
    await renderizar();
    escribir("Buscar cuenta", "2");
    expect(codigos()).toEqual(["2"]);
  });

  it("ignora una busqueda de solo espacios", async () => {
    await renderizar();
    escribir("Buscar cuenta", "   ");
    expect(codigos()).toEqual(["1", "1.1", "1.1.1", "2"]);
    expect(celdaCodigo("1.1").style.paddingLeft).toBe("2rem");
  });

  it("filtra por tipo", async () => {
    await renderizar();
    escribir("Filtrar por tipo", "Pasivo");
    expect(codigos()).toEqual(["2"]);
  });

  it("combina busqueda y tipo", async () => {
    await renderizar();
    escribir("Buscar cuenta", "caja");
    escribir("Filtrar por tipo", "Activo");
    expect(codigos()).toEqual(["1.1", "1.1.1"]);
    escribir("Filtrar por tipo", "Pasivo");
    expect(screen.getByText("No se encontraron cuentas con esos criterios.")).toBeInTheDocument();
    expect(screen.getByText("Mostrando 0 de 4 cuentas")).toBeInTheDocument();
  });
});

describe("ordenamiento", () => {
  const ORDENABLES = [
    { ...base, id: 1, codigo: "1", nombre: "Zeta", tipo: "Gasto" },
    { ...base, id: 2, codigo: "2", nombre: "Alfa", tipo: "Activo", naturaleza: "Acreedora" },
    { ...base, id: 3, codigo: "3", nombre: "Mu", tipo: "Pasivo" },
  ];

  beforeEach(() => {
    cuentasApi.listar.mockResolvedValue({ data: ORDENABLES });
  });

  it.each([
    ["nombre", "Ordenar por nombre", ["2", "3", "1"], ["1", "3", "2"]],
    ["tipo", "Ordenar por tipo", ["2", "1", "3"], ["3", "1", "2"]],
    ["naturaleza", "Ordenar por naturaleza", ["2", "1", "3"], ["1", "3", "2"]],
  ])("ordena por %s ascendente y descendente", async (_campo, boton, asc, desc) => {
    await renderizar(3);
    fireEvent.click(screen.getByRole("button", { name: boton }));
    expect(codigos()).toEqual(asc);
    fireEvent.click(screen.getByRole("button", { name: boton }));
    expect(codigos()).toEqual(desc);
  });

  it("alterna el orden por codigo y muestra el indicador", async () => {
    await renderizar(3);
    expect(screen.getByRole("button", { name: "Ordenar por código" })).toHaveTextContent("Código ▲");
    fireEvent.click(screen.getByRole("button", { name: "Ordenar por código" }));
    expect(codigos()).toEqual(["3", "2", "1"]);
    expect(screen.getByRole("button", { name: "Ordenar por código" })).toHaveTextContent("Código ▼");
    fireEvent.click(screen.getByRole("button", { name: "Ordenar por código" }));
    expect(codigos()).toEqual(["1", "2", "3"]);
    expect(screen.getByRole("button", { name: "Ordenar por código" })).toHaveTextContent("Código ▲");
  });
});

it("aplana la jerarquia al ordenar por otro campo", async () => {
  cuentasApi.listar.mockResolvedValue({ data: JERARQUIA });
  await renderizar(4);
  fireEvent.click(screen.getByRole("button", { name: "Ordenar por nombre" }));
  expect(celdaCodigo("1.1").style.paddingLeft).toBe("0.75rem");
});

describe("inactivas", () => {
  it("recarga con inactivas al marcar la casilla", async () => {
    await renderizar();
    fireEvent.click(screen.getByLabelText("Incluir inactivas"));
    await waitFor(() => expect(cuentasApi.listar).toHaveBeenLastCalledWith(true));
    fireEvent.click(screen.getByLabelText("Incluir inactivas"));
    await waitFor(() => expect(cuentasApi.listar).toHaveBeenLastCalledWith(false));
  });
});

describe("crear cuenta", () => {
  it("crea una cuenta sin padre y limpia el formulario", async () => {
    await renderizar();
    escribir("Código", "3");
    escribir("Nombre", "Bancos");
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    await waitFor(() => expect(cuentasApi.crear).toHaveBeenCalledWith({ codigo: "3", nombre: "Bancos", tipo: "Activo", naturaleza: "Deudora", cuentaPadreId: null }));
    await waitFor(() => expect(cuentasApi.listar).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getByLabelText("Código")).toHaveValue(""));
    expect(screen.getByLabelText("Nombre")).toHaveValue("");
  });

  it("crea una cuenta con padre convirtiendo el id a numero", async () => {
    await renderizar();
    escribir("Código", "1.2");
    escribir("Nombre", "Banco");
    escribir("Tipo", "Pasivo");
    escribir("Naturaleza", "Acreedora");
    escribir("Cuenta padre", "1");
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    await waitFor(() => expect(cuentasApi.crear).toHaveBeenCalledWith({ codigo: "1.2", nombre: "Banco", tipo: "Pasivo", naturaleza: "Acreedora", cuentaPadreId: 1 }));
  });

  it("muestra el error del servidor y conserva los valores", async () => {
    cuentasApi.crear.mockRejectedValue({ response: { data: { error: "Codigo duplicado" } } });
    await renderizar();
    escribir("Código", "1");
    escribir("Nombre", "Dup");
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    await screen.findByText("Codigo duplicado");
    expect(screen.getByLabelText("Código")).toHaveValue("1");
    expect(screen.getByLabelText("Nombre")).toHaveValue("Dup");
    expect(cuentasApi.listar).toHaveBeenCalledTimes(1);
  });

  it("usa el mensaje por defecto si el error no trae respuesta", async () => {
    cuentasApi.crear.mockRejectedValue(new Error("red"));
    await renderizar();
    escribir("Código", "X");
    escribir("Nombre", "Test");
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    expect(await screen.findByText("No se pudo crear la cuenta.")).toBeInTheDocument();
  });

  it("deshabilita el boton mientras se crea", async () => {
    const d = deferred();
    cuentasApi.crear.mockReturnValue(d.promise);
    await renderizar();
    escribir("Código", "4");
    escribir("Nombre", "Nueva");
    fireEvent.click(screen.getByRole("button", { name: "Agregar" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Agregar" })).toBeDisabled());
    await act(async () => {
      d.resolve({ data: {} });
    });
    await waitFor(() => expect(screen.getByRole("button", { name: "Agregar" })).toBeEnabled());
  });
});

describe("desactivar", () => {
  it("desactiva tras confirmar", async () => {
    const confirmar = vi.fn(() => true);
    vi.stubGlobal("confirm", confirmar);
    await renderizar();
    fireEvent.click(screen.getAllByRole("button", { name: "Desactivar" })[0]);
    await waitFor(() => expect(cuentasApi.desactivar).toHaveBeenCalledWith(1));
    expect(confirmar).toHaveBeenCalledWith("¿Desactivar esta cuenta?");
    await waitFor(() => expect(cuentasApi.listar).toHaveBeenCalledTimes(2));
  });

  it("no desactiva si se cancela la confirmacion", async () => {
    vi.stubGlobal("confirm", vi.fn(() => false));
    await renderizar();
    fireEvent.click(screen.getAllByRole("button", { name: "Desactivar" })[0]);
    expect(cuentasApi.desactivar).not.toHaveBeenCalled();
    expect(cuentasApi.listar).toHaveBeenCalledTimes(1);
  });
});
