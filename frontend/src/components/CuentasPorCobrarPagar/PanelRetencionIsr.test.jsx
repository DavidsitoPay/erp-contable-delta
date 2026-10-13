import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import { AJUSTE_VACIO } from "../../utils/retenciones";
import PanelRetencionIsr from "./PanelRetencionIsr";

const SIN_ESTADO = { previa: null, error: "", bloqueada: false, cargando: false };
const retenida = {
  estado: "Retenida",
  motivo: null,
  base: 40000,
  tasaBase: 5,
  tramoLimite: 30000,
  tasaExcedente: 7,
  umbralMinimo: 2500,
  retencion: 2200,
  neto: 42600,
  parametroId: 1,
  referencial: true,
  advertencias: [],
};

function renderPanel({ estado = SIN_ESTADO, ajuste = AJUSTE_VACIO, total = 44800 } = {}) {
  const onAjuste = vi.fn();
  const { container } = render(<PanelRetencionIsr estado={estado} total={total} ajuste={ajuste} onAjuste={onAjuste} />);
  return { onAjuste, container };
}

describe("PanelRetencionIsr", () => {
  it("no muestra nada mientras no haya consulta, resultado ni error", () => {
    const { container } = renderPanel();

    expect(container).toBeEmptyDOMElement();
  });

  it("muestra el avance de la consulta y el error del servidor", () => {
    renderPanel({ estado: { ...SIN_ESTADO, cargando: true, error: "Falta configurar la cuenta de ISR retenido por pagar." } });

    expect(screen.getByText("Calculando retención...")).toBeInTheDocument();
    expect(screen.getByText("Falta configurar la cuenta de ISR retenido por pagar.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Ajustar retención")).toBeNull();
  });

  it("con retención muestra base, tasa, retención, neto y la leyenda referencial", () => {
    renderPanel({ estado: { ...SIN_ESTADO, previa: retenida } });

    expect(screen.getByText(`Base sin IVA: ${formatoMoneda.format(40000)}`)).toBeInTheDocument();
    expect(screen.getByText(`Tasa: 5 % hasta ${formatoMoneda.format(30000)} y 7 % sobre el excedente`)).toBeInTheDocument();
    expect(screen.getByText(`Retención de ISR: ${formatoMoneda.format(2200)}`)).toBeInTheDocument();
    expect(screen.getByText(`Neto a pagar al proveedor: ${formatoMoneda.format(42600)}`)).toBeInTheDocument();
    expect(screen.getByText("Referencial, validar con asesor")).toBeInTheDocument();
  });

  it("cuando no aplica muestra el motivo y no ofrece ajuste", () => {
    renderPanel({ estado: { ...SIN_ESTADO, previa: { ...retenida, estado: "NoAplica", motivo: "proveedor en régimen sobre utilidades", retencion: 0, neto: 44800 } } });

    expect(screen.getByText("No se retiene ISR: proveedor en régimen sobre utilidades")).toBeInTheDocument();
    expect(screen.getByText(`Neto a pagar al proveedor: ${formatoMoneda.format(44800)}`)).toBeInTheDocument();
    expect(screen.queryByLabelText("Ajustar retención")).toBeNull();
  });

  it("con revisión pendiente advierte el dato faltante, muestra las advertencias y permite ajustar", () => {
    const previa = {
      ...retenida,
      estado: "PendienteRevision",
      motivo: "El proveedor ACME no tiene régimen de ISR definido.",
      retencion: 0,
      neto: 44800,
      advertencias: ["La retención de IVA como agente de retención no está disponible en esta versión; no se calculó."],
    };
    renderPanel({ estado: { ...SIN_ESTADO, previa } });

    expect(screen.getByText("El proveedor ACME no tiene régimen de ISR definido.")).toBeInTheDocument();
    expect(screen.getByText(/quedará marcada para revisión/)).toBeInTheDocument();
    expect(screen.getByText(/La retención de IVA como agente de retención no está disponible/)).toBeInTheDocument();
    expect(screen.getByLabelText("Ajustar retención")).toBeInTheDocument();
  });

  it("al marcar el ajuste notifica el cambio", () => {
    const { onAjuste } = renderPanel({ estado: { ...SIN_ESTADO, previa: retenida } });

    fireEvent.click(screen.getByLabelText("Ajustar retención"));

    expect(onAjuste).toHaveBeenCalledWith({ activo: true, monto: "", motivo: "" });
  });

  it("con el ajuste activo recalcula retención y neto, notifica los campos y avisa si está incompleto", () => {
    const ajuste = { activo: true, monto: "1000", motivo: "" };
    const { onAjuste } = renderPanel({ estado: { ...SIN_ESTADO, previa: retenida }, ajuste });

    expect(screen.getByText(`Retención de ISR: ${formatoMoneda.format(1000)}`)).toBeInTheDocument();
    expect(screen.getByText(`Neto a pagar al proveedor: ${formatoMoneda.format(43800)}`)).toBeInTheDocument();
    expect(screen.getByText("Indique un monto desde 0 y menor al total de la factura, y el motivo del ajuste.")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Monto de retención ajustado"), { target: { value: "500" } });
    fireEvent.change(screen.getByLabelText("Motivo del ajuste"), { target: { value: "Acuerdo" } });

    expect(onAjuste).toHaveBeenNthCalledWith(1, { ...ajuste, monto: "500" });
    expect(onAjuste).toHaveBeenNthCalledWith(2, { ...ajuste, motivo: "Acuerdo" });
  });

  it("con el ajuste completo no muestra el aviso de datos faltantes", () => {
    renderPanel({ estado: { ...SIN_ESTADO, previa: retenida }, ajuste: { activo: true, monto: "0", motivo: "Constancia de exención" } });

    expect(screen.queryByText(/Indique un monto desde 0/)).toBeNull();
  });
});
