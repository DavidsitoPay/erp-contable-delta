import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatoMoneda } from "../../utils/formato";
import ResumenCobro from "./ResumenCobro";

describe("ResumenCobro", () => {
  it("muestra el total aplicado, el IVA retenido y el neto a recibir en banco", () => {
    render(<ResumenCobro resumen={{ totalAplicado: 11200, ivaRetenido: 300, neto: 10900 }} />);

    expect(screen.getByText(`Total aplicado: ${formatoMoneda.format(11200)}`)).toBeInTheDocument();
    expect(screen.getByText(`IVA retenido: ${formatoMoneda.format(300)}`)).toBeInTheDocument();
    expect(screen.getByText(`Neto a recibir en banco: ${formatoMoneda.format(10900)}`)).toBeInTheDocument();
    expect(screen.queryByText(/debe ser mayor a cero/)).toBeNull();
  });

  it("advierte cuando el neto a recibir no es positivo", () => {
    render(<ResumenCobro resumen={{ totalAplicado: 300, ivaRetenido: 300, neto: 0 }} />);

    expect(screen.getByText("El cobro neto (total aplicado menos IVA retenido) debe ser mayor a cero.")).toBeInTheDocument();
  });
});
