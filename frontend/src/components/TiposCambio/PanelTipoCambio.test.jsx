import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import { AVISO_BANGUAT, AVISO_SIN_PERMISO_TASA } from "../../utils/cambio";
import PanelTipoCambio from "./PanelTipoCambio";

vi.mock("../../services/api", () => ({ tiposCambioApi: { capturarManual: vi.fn() } }));

function estado(cambios = {}) {
  return {
    cargando: false,
    consultado: true,
    error: "",
    disponible: true,
    tipoCambio: { id: 1, tasa: 7.6, fuente: "Banguat", arrastrado: false, fechaReferencia: "2026-10-12" },
    propuesta: null,
    mensaje: "",
    recargar: vi.fn(),
    ...cambios,
  };
}

function iniciarSesionComo(perfil) {
  localStorage.setItem("delta_usuario", JSON.stringify({ nombre: "Ana", perfil }));
}

const SIN_TASA = { consultado: true, disponible: false, mensaje: "Banguat no respondió; capture la tasa manual o reintente." };

describe("PanelTipoCambio", () => {
  beforeEach(() => {
    localStorage.clear();
    tiposCambioApi.capturarManual.mockReset();
    iniciarSesionComo("Contador");
  });

  it("no renderiza nada para GTQ", () => {
    const { container } = render(<PanelTipoCambio estado={estado()} moneda="GTQ" fecha="2026-10-12" />);

    expect(container).toBeEmptyDOMElement();
  });

  it("muestra la tasa vigente y el aviso de Banguat", () => {
    render(<PanelTipoCambio estado={estado()} moneda="USD" fecha="2026-10-12" />);

    expect(screen.getByText("Tipo de cambio: 7.600000 (Banguat)")).toBeInTheDocument();
    expect(screen.getByText(AVISO_BANGUAT)).toBeInTheDocument();
    expect(screen.queryByText("Arrastrado")).toBeNull();
  });

  it("indica tasa arrastrada con su referencia", () => {
    render(<PanelTipoCambio estado={estado({ tipoCambio: { id: 1, tasa: 7.6, fuente: "Banguat", arrastrado: true, fechaReferencia: "2026-10-09" } })} moneda="USD" fecha="2026-10-12" />);

    expect(screen.getByText("Tipo de cambio: 7.600000 (Banguat (arrastrado))")).toBeInTheDocument();
    expect(screen.getByText("Arrastrado")).toBeInTheDocument();
    expect(screen.getByText("Referencia del 2026-10-09")).toBeInTheDocument();
  });

  it("muestra la consulta en curso sin aviso de Banguat caído", () => {
    render(<PanelTipoCambio estado={estado({ cargando: true, consultado: false, disponible: false })} moneda="USD" fecha="2026-10-12" />);

    expect(screen.getByText("Consultando tipo de cambio...")).toBeInTheDocument();
    expect(screen.queryByText(SIN_TASA.mensaje)).toBeNull();
  });

  it("sin consulta aún no muestra el aviso de Banguat caído ni botones", () => {
    render(<PanelTipoCambio estado={estado({ consultado: false, disponible: false })} moneda="USD" fecha="" />);

    expect(screen.queryByText(SIN_TASA.mensaje)).toBeNull();
    expect(screen.queryByRole("button", { name: "Reintentar" })).toBeNull();
  });

  it("un error muestra el mensaje sin botón de reintento", () => {
    render(<PanelTipoCambio estado={estado({ error: "No hay tipo de cambio para fechas futuras.", consultado: false, disponible: false })} moneda="USD" fecha="2026-12-01" />);

    expect(screen.getByText("No hay tipo de cambio para fechas futuras.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reintentar" })).toBeNull();
  });

  it("sin tasa con propuesta permite usar el último valor como sugerencia para un Contador", async () => {
    const recargar = vi.fn();
    tiposCambioApi.capturarManual.mockResolvedValue({});
    const propuesta = { fecha: "2026-10-09", tasa: 7.64036, fuente: "Banguat" };
    const { container } = render(
      <form onSubmit={vi.fn()}>
        <PanelTipoCambio estado={estado({ ...SIN_TASA, propuesta, recargar })} moneda="USD" fecha="2026-10-12" />
      </form>,
    );

    expect(screen.getByText(SIN_TASA.mensaje)).toBeInTheDocument();
    expect(screen.getByText("Último valor guardado: 7.640360 del 2026-10-09")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Usar este valor" }));

    expect(screen.getByLabelText("Tasa de cambio (GTQ por unidad)")).toHaveValue(7.64036);
    expect(screen.getByLabelText("Fecha de la tasa")).toBeDisabled();
    expect(container.querySelectorAll("form")).toHaveLength(1);

    fireEvent.change(screen.getByLabelText("Motivo de la tasa manual"), { target: { value: "Banguat caído" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar tasa manual" }));

    await waitFor(() => expect(recargar).toHaveBeenCalledTimes(1));
    expect(screen.queryByLabelText("Motivo de la tasa manual")).toBeNull();
  });

  it("Reintentar llama recargar", () => {
    const recargar = vi.fn();
    render(<PanelTipoCambio estado={estado({ ...SIN_TASA, recargar })} moneda="USD" fecha="2026-10-12" />);

    fireEvent.click(screen.getByRole("button", { name: "Reintentar" }));

    expect(recargar).toHaveBeenCalledTimes(1);
  });

  it("sin propuesta el Administrador ve el botón de captura manual", () => {
    iniciarSesionComo("Administrador del sistema");
    render(<PanelTipoCambio estado={estado(SIN_TASA)} moneda="USD" fecha="2026-10-12" />);

    expect(screen.getByRole("button", { name: "Capturar tasa manual" })).toBeInTheDocument();
  });

  it("el Vendedor no puede capturar y ve el aviso de permiso", () => {
    iniciarSesionComo("Vendedor");
    render(<PanelTipoCambio estado={estado({ ...SIN_TASA, propuesta: { fecha: "2026-10-09", tasa: 7.64036, fuente: "Banguat" } })} moneda="USD" fecha="2026-10-12" />);

    expect(screen.queryByRole("button", { name: "Usar este valor" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Capturar tasa manual" })).toBeNull();
    expect(screen.getByText(AVISO_SIN_PERMISO_TASA)).toBeInTheDocument();
  });

  it("indica que la tasa fue heredada", () => {
    render(<PanelTipoCambio estado={estado({ heredado: true })} moneda="USD" fecha="2026-10-12" />);

    expect(screen.getByText("Heredado de la factura de origen.")).toBeInTheDocument();
  });

  it("titulo reemplaza la leyenda por defecto", () => {
    render(<PanelTipoCambio estado={estado()} moneda="USD" fecha="2026-10-12" titulo="Referencia del día" />);

    expect(screen.getByText("Referencia del día")).toBeInTheDocument();
    expect(screen.queryByText("Tipo de cambio USD")).toBeNull();
  });
});
