import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { asientosApi, centrosCostoApi, librosApi, periodosApi } from "../../services/api";
import LibroDiario from "./LibroDiario";

vi.mock("../../services/api", () => ({
  librosApi: { diario: vi.fn() },
  periodosApi: { listar: vi.fn() },
  centrosCostoApi: { listar: vi.fn() },
  asientosApi: { reversar: vi.fn() },
}));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const PERIODOS = [{ id: 1, nombre: "Enero 2025" }, { id: 2, nombre: "Febrero 2025" }];
const ASIENTOS = [
  {
    id: 1,
    fecha: "2025-01-05",
    numero: "AS-001",
    lineas: [
      { cuentaCodigo: "1.1", cuentaNombre: "Caja", centroCostoId: 10, debito: 150.5, credito: 0 },
      { cuentaCodigo: "4.1", cuentaNombre: "Ingresos", centroCostoId: null, debito: 0, credito: 150.5 },
    ],
  },
];

beforeEach(() => {
  localStorage.clear();
  [librosApi, periodosApi, centrosCostoApi, asientosApi].forEach((api) => Object.values(api).forEach((fn) => fn.mockReset()));
  periodosApi.listar.mockResolvedValue({ data: PERIODOS });
  centrosCostoApi.listar.mockResolvedValue({ data: [{ id: 10, nombre: "Ventas" }] });
  librosApi.diario.mockResolvedValue({ data: ASIENTOS });
});

afterEach(() => {
  vi.restoreAllMocks();
});

async function seleccionarPeriodo(valor = "1") {
  render(<LibroDiario />);
  await screen.findByRole("option", { name: "Enero 2025" });
  fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: valor } });
}

describe("LibroDiario", () => {
  it("carga periodos y centros de costo incluyendo inactivos", async () => {
    render(<LibroDiario />);
    await screen.findByRole("option", { name: "Febrero 2025" });
    expect(centrosCostoApi.listar).toHaveBeenCalledWith(true);
    expect(screen.getByRole("heading", { name: "Libro diario" })).toBeInTheDocument();
  });

  it("no consulta el diario sin periodo seleccionado", async () => {
    render(<LibroDiario />);
    await screen.findByRole("option", { name: "Enero 2025" });
    expect(librosApi.diario).not.toHaveBeenCalled();
    expect(screen.queryByText("No hay asientos confirmados en este periodo.")).toBeNull();
  });

  it("muestra los asientos del periodo con centro de costo", async () => {
    await seleccionarPeriodo();
    await screen.findByText("Ventas");
    await screen.findByText("AS-001");
    expect(librosApi.diario).toHaveBeenCalledWith(1);
    const filas = screen.getAllByRole("row");
    expect(filas).toHaveLength(3);
    expect(within(filas[1]).getByText("2025-01-05")).toBeInTheDocument();
    expect(within(filas[1]).getByText("AS-001")).toBeInTheDocument();
    expect(within(filas[1]).getByText("1.1 - Caja")).toBeInTheDocument();
    expect(within(filas[1]).getByText("Ventas")).toBeInTheDocument();
    expect(within(filas[1]).getByText("150.50")).toBeInTheDocument();
    expect(within(filas[2]).queryByText("AS-001")).toBeNull();
    expect(within(filas[2]).getByText("4.1 - Ingresos")).toBeInTheDocument();
    expect(within(filas[2]).getByText("—")).toBeInTheDocument();
    expect(within(filas[2]).getByText("150.50")).toBeInTheDocument();
  });

  it("muestra Cargando mientras llega el diario", async () => {
    const d = deferred();
    librosApi.diario.mockReturnValue(d.promise);
    await seleccionarPeriodo();
    await screen.findByText("Cargando...");
    await act(async () => {
      d.resolve({ data: ASIENTOS });
    });
    await screen.findByText("AS-001");
    expect(screen.queryByText("Cargando...")).toBeNull();
  });

  it("avisa cuando el periodo no tiene asientos", async () => {
    librosApi.diario.mockResolvedValue({ data: [] });
    await seleccionarPeriodo();
    expect(await screen.findByText("No hay asientos confirmados en este periodo.")).toBeInTheDocument();
  });

  it("muestra el error del servidor y oculta el aviso de vacio", async () => {
    librosApi.diario.mockRejectedValue({ response: { data: { error: "Periodo cerrado" } } });
    await seleccionarPeriodo();
    await screen.findByText("Periodo cerrado");
    expect(screen.queryByText("No hay asientos confirmados en este periodo.")).toBeNull();
  });

  it("usa el mensaje por defecto si el error no trae respuesta", async () => {
    librosApi.diario.mockRejectedValue(new Error("red"));
    await seleccionarPeriodo();
    expect(await screen.findByText("No se pudo cargar el libro diario.")).toBeInTheDocument();
  });

  it("limpia los asientos al deseleccionar el periodo", async () => {
    await seleccionarPeriodo();
    await screen.findByText("AS-001");
    fireEvent.change(screen.getByLabelText("Periodo"), { target: { value: "" } });
    await waitFor(() => expect(screen.queryByText("AS-001")).toBeNull());
    expect(librosApi.diario).toHaveBeenCalledTimes(1);
  });
});

const PERFIL_ADMIN = "Administrador del sistema";
const ASIENTOS_REVERSA = [
  { id: 1, numero: "AS-001", fecha: "2025-01-05", estado: "Confirmado", reversible: true, reversaDeId: null, reversadoPorId: null, lineas: ASIENTOS[0].lineas },
  { id: 2, numero: "AS-002", fecha: "2025-01-06", estado: "Anulado", reversible: false, reversaDeId: null, reversadoPorId: 3, reversadoPorNumero: "REV-AS-002", lineas: ASIENTOS[0].lineas },
  { id: 3, numero: "REV-AS-002", fecha: "2025-01-07", estado: "Confirmado", reversible: false, reversaDeId: 2, reversaDeNumero: "AS-002", reversadoPorId: null, lineas: ASIENTOS[0].lineas },
];

async function abrirDialogoReversa() {
  localStorage.setItem("delta_usuario", JSON.stringify({ perfil: PERFIL_ADMIN }));
  librosApi.diario.mockResolvedValue({ data: ASIENTOS_REVERSA });
  await seleccionarPeriodo();
  const boton = await screen.findByRole("button", { name: "Reversar" });
  boton.focus();
  fireEvent.click(boton);
  return screen.findByRole("dialog", { name: "Reversar asiento" });
}

function confirmarConMotivo(motivo) {
  fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: motivo } });
  fireEvent.click(screen.getByRole("button", { name: "Reversar asiento" }));
}

function cancelarDialogo(dialogo) {
  return fireEvent(dialogo, new Event("cancel", { cancelable: true }));
}

describe("LibroDiario reversa de asientos", () => {
  it("muestra las insignias de Anulado y Reversa a cualquier perfil", async () => {
    localStorage.setItem("delta_usuario", JSON.stringify({ perfil: "Contador" }));
    librosApi.diario.mockResolvedValue({ data: ASIENTOS_REVERSA });
    await seleccionarPeriodo();
    expect(await screen.findByText("Anulado — reversado por REV-AS-002")).toBeInTheDocument();
    expect(screen.getByText("Reversa de AS-002")).toBeInTheDocument();
  });

  it("oculta el boton Reversar al Contador", async () => {
    localStorage.setItem("delta_usuario", JSON.stringify({ perfil: "Contador" }));
    librosApi.diario.mockResolvedValue({ data: ASIENTOS_REVERSA });
    await seleccionarPeriodo();
    await screen.findByText("AS-001");
    expect(screen.queryByRole("button", { name: "Reversar" })).toBeNull();
  });

  it("al Administrador le muestra Reversar solo en el asiento reversible y abre el dialogo con el texto de confirmacion", async () => {
    await abrirDialogoReversa();
    expect(screen.getByText(/Se anulará el asiento/).textContent).toBe(
      "Se anulará el asiento AS-001 y se generará REV-AS-001 con las líneas invertidas. Esta acción no se puede deshacer."
    );
    expect(screen.getAllByRole("button", { name: "Reversar" })).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(asientosApi.reversar).not.toHaveBeenCalled();
  });

  it("deshabilita confirmar mientras el motivo esta en blanco", async () => {
    await abrirDialogoReversa();
    const confirmar = screen.getByRole("button", { name: "Reversar asiento" });
    expect(confirmar).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "   " } });
    expect(confirmar).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Error de captura" } });
    expect(confirmar).toBeEnabled();
  });

  it("cierra el dialogo al cancelar con Escape sin llamar a la API", async () => {
    const dialogo = await abrirDialogoReversa();
    expect(cancelarDialogo(dialogo)).toBe(false);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(asientosApi.reversar).not.toHaveBeenCalled();
  });

  it("confirma la reversa con el motivo, recarga el diario y muestra el exito", async () => {
    asientosApi.reversar.mockResolvedValue({
      data: { originalId: 1, originalNumero: "AS-001", reversaId: 4, reversaNumero: "REV-AS-001" },
    });
    await abrirDialogoReversa();
    confirmarConMotivo("  Error de captura  ");
    expect(await screen.findByText("Asiento AS-001 anulado. Reversa REV-AS-001 registrada.")).toBeInTheDocument();
    expect(asientosApi.reversar).toHaveBeenCalledWith(1, "Error de captura");
    expect(librosApi.diario).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("muestra el mensaje del API cuando la reversa responde 409 y conserva el dialogo", async () => {
    asientosApi.reversar.mockRejectedValue({ response: { data: { error: "El asiento 1 pertenece a un periodo cerrado." } } });
    await abrirDialogoReversa();
    confirmarConMotivo("Motivo");
    expect(await screen.findByText("El asiento 1 pertenece a un periodo cerrado.")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(librosApi.diario).toHaveBeenCalledTimes(1);
  });

  it("usa un mensaje por defecto si el error no trae respuesta", async () => {
    asientosApi.reversar.mockRejectedValue(new Error("red"));
    await abrirDialogoReversa();
    confirmarConMotivo("Motivo");
    expect(await screen.findByText("No se pudo reversar el asiento.")).toBeInTheDocument();
  });

  it("abre el dialogo como modal cuando el navegador implementa showModal", async () => {
    const showModal = vi.fn(function abrir() {
      this.setAttribute("open", "");
    });
    HTMLDialogElement.prototype.showModal = showModal;
    try {
      await abrirDialogoReversa();
      expect(showModal).toHaveBeenCalledTimes(1);
      expect(screen.getByLabelText("Motivo")).toHaveFocus();
    } finally {
      delete HTMLDialogElement.prototype.showModal;
    }
  });

  it("limita el motivo a 250 caracteres y enfoca el campo al abrir", async () => {
    await abrirDialogoReversa();
    const motivo = screen.getByLabelText("Motivo");
    expect(motivo).toHaveAttribute("maxlength", "250");
    expect(motivo).toHaveFocus();
  });

  it("devuelve el foco al boton Reversar al cerrar el dialogo", async () => {
    await abrirDialogoReversa();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("button", { name: "Reversar" })).toHaveFocus();
  });

  it("bloquea confirmar y Escape mientras se envia la reversa", async () => {
    const d = deferred();
    asientosApi.reversar.mockReturnValue(d.promise);
    const dialogo = await abrirDialogoReversa();
    confirmarConMotivo("Motivo");
    await waitFor(() => expect(screen.getByRole("button", { name: "Reversar asiento" })).toBeDisabled());
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeDisabled();
    expect(cancelarDialogo(dialogo)).toBe(false);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    await act(async () => {
      d.resolve({ data: { originalNumero: "AS-001", reversaNumero: "REV-AS-001" } });
    });
    expect(await screen.findByText("Asiento AS-001 anulado. Reversa REV-AS-001 registrada.")).toBeInTheDocument();
  });
});
