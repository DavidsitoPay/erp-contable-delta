import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { celdasDe, cuentaReporte } from "../../test/reportesFixtures";
import { FilaMonto, SeccionReporte, TablaReporte } from "./TablaReporte";

const SECCION = {
  total: 200,
  cuentas: [
    cuentaReporte({ cuentaId: 1, codigo: "1", nombre: "Activo", esHoja: false, saldo: 200 }),
    cuentaReporte({ cuentaId: 2, codigo: "1.1", nombre: "Caja", nivel: 2, saldo: 200 }),
    cuentaReporte({ cuentaId: 3, codigo: "1.2", nombre: "Banco", nivel: 2, saldo: 0 }),
  ],
};

function renderizar(props) {
  return render(
    <TablaReporte>
      <SeccionReporte titulo="Activo" seccion={SECCION} mostrarCeros={false} {...props} />
    </TablaReporte>,
  );
}

describe("TablaReporte", () => {
  it("indenta, marca padres y muestra el total", () => {
    renderizar();

    expect(screen.getByText("1 - Activo").closest("tr")).toHaveClass("row-parent");
    expect(screen.getByText("1.1 - Caja").closest("tr")).not.toHaveClass("row-parent");
    expect(screen.getByText("1.1 - Caja")).toHaveStyle({ paddingLeft: "2rem" });
    expect(celdasDe("Total Activo")).toEqual(["Total Activo", "200.00"]);
  });

  it("oculta cuentas en cero salvo que se pidan", () => {
    const { unmount } = renderizar();
    expect(screen.queryByText("1.2 - Banco")).toBeNull();
    unmount();

    renderizar({ mostrarCeros: true });
    expect(screen.getByText("1.2 - Banco")).toBeInTheDocument();
  });

  it("permite total y etiqueta propios y filas extra", () => {
    render(
      <TablaReporte>
        <SeccionReporte titulo="Capital" seccion={SECCION} mostrarCeros={false} total={150} etiquetaTotal="Total capital">
          <FilaMonto etiqueta="Extra" valor={50} />
        </SeccionReporte>
      </TablaReporte>,
    );

    expect(celdasDe("Extra")).toEqual(["Extra", "50.00"]);
    expect(celdasDe("Total capital")).toEqual(["Total capital", "150.00"]);
  });

  it("FilaMonto aplica la clase indicada a la celda del valor", () => {
    render(
      <table>
        <tbody>
          <FilaMonto etiqueta="Diferencia" valor={25.5} clase="text-danger" />
        </tbody>
      </table>,
    );

    const celdas = within(screen.getByText("Diferencia").closest("tr")).getAllByRole("cell");
    expect(celdas[1]).toHaveClass("text-danger");
    expect(celdas[1]).toHaveTextContent("25.50");
  });
});
