import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { periodosApi } from "../../services/api";
import { PERIODOS, periodoReporte } from "../../test/reportesFixtures";
import PaginaReporte from "./PaginaReporte";

vi.mock("../../services/api", () => ({ periodosApi: { listar: vi.fn() } }));

const obtener = vi.fn();
const MENSAJE = "No se pudo cargar el reporte de prueba.";

const reporteDe = (id, extra) => ({ periodo: periodoReporte({ id }), fuente: "Preliminar", ...extra });
const responder = (extra) => (id) => Promise.resolve({ data: reporteDe(id, extra) });

function renderizar() {
  return render(
    <PaginaReporte titulo="Reporte de prueba" obtener={obtener} mensajeFallo={MENSAJE}>
      {(r, mostrarCeros) => <p>{`periodo ${r.periodo.id} ceros ${mostrarCeros}`}</p>}
    </PaginaReporte>,
  );
}

beforeEach(() => {
  periodosApi.listar.mockReset();
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  obtener.mockReset();
  obtener.mockImplementation(responder());
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("PaginaReporte", () => {
  it("preselecciona el periodo con fecha fin mas reciente y consulta el reporte", async () => {
    renderizar();

    await screen.findByText("periodo 2 ceros false");
    expect(screen.getByLabelText("Periodo")).toHaveValue("2");
    expect(obtener).toHaveBeenCalledWith(2);
  });

  it("consulta de nuevo al cambiar de periodo", async () => {
    renderizar();
    await screen.findByText("periodo 2 ceros false");

    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "1" } });

    await screen.findByText("periodo 1 ceros false");
    expect(obtener).toHaveBeenLastCalledWith(1);
  });

  it("alterna Mostrar cuentas en cero", async () => {
    renderizar();
    await screen.findByText("periodo 2 ceros false");

    fireEvent.click(screen.getByLabelText("Mostrar cuentas en cero"));

    expect(await screen.findByText("periodo 2 ceros true")).toBeInTheDocument();
  });

  it("muestra Cargando mientras llega el reporte", async () => {
    obtener.mockReturnValue(new Promise(() => {}));
    renderizar();

    await screen.findByText("Cargando...");
    expect(screen.queryByText(/periodo 2 ceros/)).toBeNull();
  });

  it("muestra el error del servidor", async () => {
    obtener.mockRejectedValue({ response: { data: { error: "Sin permisos" } } });
    renderizar();

    await screen.findByText("Sin permisos");
    expect(screen.queryByText(/periodo 2 ceros/)).toBeNull();
  });

  it("usa el mensaje por defecto si el error no trae respuesta", async () => {
    obtener.mockRejectedValue(new Error("red"));
    renderizar();

    expect(await screen.findByText(MENSAJE)).toBeInTheDocument();
  });

  it("avisa si no se pueden cargar los periodos", async () => {
    periodosApi.listar.mockRejectedValue(new Error("red"));
    renderizar();

    await screen.findByText("No se pudieron cargar los periodos.");
    expect(obtener).not.toHaveBeenCalled();
  });

  it("sin periodos muestra la opcion vacia y no consulta", async () => {
    periodosApi.listar.mockResolvedValue({ data: [] });
    renderizar();

    await waitFor(() => expect(screen.queryByText("Cargando...")).toBeNull());
    expect(screen.getByText("Sin periodos")).toBeInTheDocument();
    expect(obtener).not.toHaveBeenCalled();
  });

  it("imprime desde el boton", async () => {
    const imprimir = vi.spyOn(window, "print").mockImplementation(() => {});
    renderizar();
    await screen.findByText("periodo 2 ceros false");

    fireEvent.click(screen.getByRole("button", { name: "Imprimir" }));

    expect(imprimir).toHaveBeenCalledTimes(1);
  });

  it("descarta la respuesta de un periodo anterior que llega tarde", async () => {
    let resolverTardio;
    obtener.mockImplementation((id) =>
      id === 2
        ? new Promise((resolve) => {
            resolverTardio = resolve;
          })
        : Promise.resolve({ data: reporteDe(id) }),
    );
    renderizar();
    await waitFor(() => expect(obtener).toHaveBeenCalledWith(2));

    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "1" } });
    await screen.findByText("periodo 1 ceros false");
    await act(async () => {
      resolverTardio({ data: reporteDe(2) });
    });

    expect(screen.getByText("periodo 1 ceros false")).toBeInTheDocument();
    expect(screen.queryByText("periodo 2 ceros false")).toBeNull();
  });

  it.each([
    ["Cierre", "Periodo cerrado — cierre 2", "badge-ok"],
    ["Preliminar", "Cifras preliminares (periodo abierto)", "badge-warning"],
  ])("muestra la fuente %s", async (fuente, texto, clase) => {
    obtener.mockImplementation((id) =>
      Promise.resolve({ data: { ...reporteDe(id), fuente, periodo: periodoReporte({ id, cierres: 2 }) } }),
    );
    renderizar();

    expect(await screen.findByText(texto)).toHaveClass(clase);
    expect(screen.queryByText(/Incluye periodos abiertos/)).toBeNull();
  });

  it("muestra el aviso cuando incluye periodos abiertos", async () => {
    obtener.mockImplementation(responder({ incluyePeriodosAbiertos: true }));
    renderizar();

    expect(await screen.findByText(/Incluye periodos abiertos anteriores/)).toBeInTheDocument();
  });
});
