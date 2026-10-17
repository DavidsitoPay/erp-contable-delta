import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import { LEYENDA_REFERENCIAL } from "../../utils/retenciones";
import PanelRetencionIsrNoResidente from "./PanelRetencionIsrNoResidente";
import { SIN_RESULTADO } from "./useConsultaPrevia";

const tiposRenta = [
  { id: 3, codigo: "SERV", nombre: "Servicios", tasaBase: 15 },
  { id: 4, codigo: "OTRO", nombre: "Otros", tasaBase: 10 },
];
const facturas = [{ id: 21, numero: "P-1" }];
const aplicaciones = [{ documentoId: 21, montoAplicado: 40, tipoRentaCodigo: "SERV" }];

const previaConLinea = {
  aplica: true,
  lineas: [
    { documentoId: 21, tipoRentaNombre: "Servicios", tasa: 15, montoOrigen: 1000, monedaCodigo: "USD", tipoCambio: 7.8, tipoCambioFecha: "2026-10-05", baseGtq: 7800, retencionGtq: 1170 },
  ],
  totalBaseGtq: 7800,
  totalRetencionGtq: 1170,
  netoBancoGtq: 6630,
  advertencias: ["Revise la constancia del proveedor."],
};

function renderPanel(props) {
  return render(
    <PanelRetencionIsrNoResidente
      estado={SIN_RESULTADO}
      aplicaciones={aplicaciones}
      facturas={facturas}
      tiposRenta={tiposRenta}
      tipos={{}}
      porDefecto="SERV"
      onTipo={vi.fn()}
      {...props}
    />
  );
}

describe("PanelRetencionIsrNoResidente", () => {
  it("sin vista previa muestra el marco y un selector de tipo de renta por factura", () => {
    renderPanel();

    expect(screen.getByText("Retención de ISR a no residentes (art. 104)")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Tipo de renta de P-1" })).toHaveValue("SERV");
    expect(screen.getByRole("option", { name: "Otros (10 %)" })).toBeInTheDocument();
  });

  it("muestra el tipo elegido para cada factura y notifica el cambio", () => {
    const onTipo = vi.fn();
    renderPanel({ tipos: { 21: "OTRO" }, onTipo });

    const selector = screen.getByRole("combobox", { name: "Tipo de renta de P-1" });
    expect(selector).toHaveValue("OTRO");

    fireEvent.change(selector, { target: { value: "SERV" } });
    expect(onTipo).toHaveBeenCalledWith(21, "SERV");
  });

  it("con vista previa muestra líneas, totales, neto, advertencias y la leyenda referencial", () => {
    renderPanel({ estado: { ...SIN_RESULTADO, previa: previaConLinea } });

    expect(screen.getByText("Servicios · 15 %")).toBeInTheDocument();
    expect(screen.getByText(`Monto ${formatoMoneda.format(1000)} USD · TC 7.8 del 2026-10-05`)).toBeInTheDocument();
    expect(screen.getByText(`Base: ${formatoMoneda.format(7800)} · Retención: ${formatoMoneda.format(1170)}`)).toBeInTheDocument();
    expect(screen.getByText(`Total retención: ${formatoMoneda.format(1170)}`)).toBeInTheDocument();
    expect(screen.getByText(`Neto al banco: ${formatoMoneda.format(6630)}`)).toBeInTheDocument();
    expect(screen.getByText("Revise la constancia del proveedor.")).toHaveClass("warning-chip");
    expect(screen.getByText(LEYENDA_REFERENCIAL)).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Tipo de renta de P-1" })).toBeInTheDocument();
  });

  it("cuando la previa no aplica muestra solo el motivo, sin neto ni selectores", () => {
    renderPanel({ estado: { ...SIN_RESULTADO, previa: { aplica: false, motivoNoAplica: "Exento por régimen", netoBancoGtq: null } } });

    expect(screen.getByText("Exento por régimen")).toBeInTheDocument();
    expect(screen.queryByText(/Neto al banco/)).toBeNull();
    expect(screen.queryByRole("combobox")).toBeNull();
  });

  it("muestra el error del servidor y conserva los selectores", () => {
    renderPanel({ estado: { ...SIN_RESULTADO, error: "Falta el tipo de renta.", bloqueada: true } });

    expect(screen.getByText("Falta el tipo de renta.")).toHaveClass("error-chip");
    expect(screen.getByRole("combobox", { name: "Tipo de renta de P-1" })).toBeInTheDocument();
  });

  it("muestra el aviso de cálculo mientras la previa está en curso", () => {
    renderPanel({ estado: { ...SIN_RESULTADO, cargando: true } });

    expect(screen.getByText("Calculando retención...")).toBeInTheDocument();
  });
});
