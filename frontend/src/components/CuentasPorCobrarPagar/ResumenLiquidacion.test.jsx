import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoDiferencial, formatoImporte, formatoQuetzales } from "../../utils/formato";
import ResumenLiquidacion from "./ResumenLiquidacion";

describe("ResumenLiquidacion", () => {
  it("no renderiza nada sin resumen", () => {
    const { container } = render(<ResumenLiquidacion resumen={null} moneda="USD" />);

    expect(container).toBeEmptyDOMElement();
  });

  it("muestra el total aplicado, el valor en quetzales y el diferencial estimado", () => {
    render(<ResumenLiquidacion resumen={{ totalAplicado: 400, totalValor: 3080, diferencial: 40 }} moneda="USD" />);

    expect(screen.getByText(`Total aplicado: ${formatoImporte(400, "USD")}`)).toBeInTheDocument();
    expect(screen.getByText(`Valor en Q: ${formatoQuetzales(3080)}`)).toBeInTheDocument();
    expect(screen.getByText(`Diferencial estimado: ${formatoDiferencial(40)} (el sistema recalcula)`)).toBeInTheDocument();
  });

  it("muestra el signo negativo de una pérdida", () => {
    render(<ResumenLiquidacion resumen={{ totalAplicado: 950, totalValor: 7315, diferencial: -72 }} moneda="USD" />);

    expect(screen.getByText(/Diferencial estimado: -Q/)).toBeInTheDocument();
  });

  it("un diferencial cero no lleva signo", () => {
    render(<ResumenLiquidacion resumen={{ totalAplicado: 100, totalValor: 764, diferencial: 0 }} moneda="GTQ" />);

    expect(screen.getByText(`Diferencial estimado: ${formatoQuetzales(0)} (el sistema recalcula)`)).toBeInTheDocument();
  });
});
