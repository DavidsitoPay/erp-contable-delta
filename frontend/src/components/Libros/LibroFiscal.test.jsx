import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { librosFiscalesApi } from "../../services/api";
import { descargarCsv } from "../../utils/csv";
import { AVISO_DTE } from "../../utils/fiscal";
import { formatoMoneda } from "../../utils/formato";
import LibroFiscal from "./LibroFiscal";

vi.mock("../../services/api", () => ({ librosFiscalesApi: { ventas: vi.fn(), compras: vi.fn() } }));
vi.mock("../../utils/csv", async (importOriginal) => ({ ...(await importOriginal()), descargarCsv: vi.fn() }));

function fila(cambios) {
  return {
    fecha: "2026-10-03",
    tipoDocumento: "Factura",
    serie: "A1B2C3D4",
    numero: "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
    numeroInterno: "F-0001",
    nit: "1234567-9",
    nombre: "ACME",
    estado: "Vigente",
    legado: false,
    baseBienes: 0,
    baseServicios: 178.57,
    exento: 50.5,
    iva: 21.43,
    total: 250.5,
    referenciaSerie: null,
    referenciaNumero: null,
    referenciaUuid: null,
    ...cambios,
  };
}

const reporte = {
  tipo: "VENTAS",
  anio: 2026,
  mes: 10,
  contribuyente: { nit: "1234567-9", nombre: "Delta S.A." },
  filas: [
    fila(),
    fila({ tipoDocumento: "NotaCredito", numeroInterno: "NC-1", nombre: "Cliente NC", baseServicios: -89.29, exento: 0, iva: -10.71, total: -100 }),
    fila({ numeroInterno: "F-0002", nombre: "Anulada SA", estado: "Anulado", baseServicios: 0, exento: 0, iva: 0, total: 0 }),
    fila({ numeroInterno: "F-0003", nombre: "Viejo SA", legado: true, numero: "uuid-viejo" }),
  ],
  totales: { baseBienes: 44.44, baseServicios: 111.11, exento: 22.22, iva: 33.33, total: 166.66 },
  advertencias: ["El libro incluye 3 documento(s) anteriores a la configuración fiscal (calculo legado)."],
};

function escribir(etiqueta, valor) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

async function consultar(anio = "2026", mes = "10") {
  escribir("Año", anio);
  escribir("Mes", mes);
  fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
  await screen.findByText("Delta S.A.", { exact: false });
}

describe("LibroFiscal", () => {
  beforeEach(() => {
    librosFiscalesApi.ventas.mockReset();
    librosFiscalesApi.compras.mockReset();
    descargarCsv.mockReset();
    librosFiscalesApi.ventas.mockResolvedValue({ data: reporte });
    librosFiscalesApi.compras.mockResolvedValue({ data: { ...reporte, tipo: "COMPRAS" } });
  });

  it("muestra el aviso de no certificación y el año y mes actuales por defecto", () => {
    render(<LibroFiscal />);

    expect(screen.getByText("Libro de compras y ventas")).toBeInTheDocument();
    expect(screen.getByText(AVISO_DTE)).toBeInTheDocument();
    expect(screen.getByLabelText("Año")).toHaveValue(new Date().getFullYear());
    expect(screen.getByLabelText("Mes")).toHaveValue(String(new Date().getMonth() + 1));
    expect(screen.queryByRole("button", { name: "Exportar CSV" })).toBeNull();
  });

  it("consulta las ventas del año y mes elegidos y muestra el contribuyente, las filas y los totales", async () => {
    render(<LibroFiscal />);

    await consultar();

    expect(librosFiscalesApi.ventas).toHaveBeenCalledWith(2026, 10);
    expect(screen.getByText("Contribuyente: Delta S.A. (NIT 1234567-9)")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Exento / no afecto" })).toBeInTheDocument();
    const filaAcme = within(screen.getByText("ACME").closest("tr"));
    expect(filaAcme.getByText(formatoMoneda.format(178.57))).toBeInTheDocument();
    expect(filaAcme.getByText(formatoMoneda.format(21.43))).toBeInTheDocument();
    expect(filaAcme.getByText(formatoMoneda.format(250.5))).toBeInTheDocument();
    const totales = within(screen.getByText("Totales").closest("tr"));
    [44.44, 111.11, 22.22, 33.33, 166.66].forEach((valor) => expect(totales.getByText(formatoMoneda.format(valor))).toBeInTheDocument());
  });

  it("muestra la nota de crédito con importes negativos, el documento anulado y el marcador de legado", async () => {
    render(<LibroFiscal />);

    await consultar();

    const filaNc = within(screen.getByText("Cliente NC").closest("tr"));
    expect(filaNc.getByText("NotaCredito")).toBeInTheDocument();
    expect(filaNc.getByText(formatoMoneda.format(-100))).toBeInTheDocument();
    expect(within(screen.getByText("Anulada SA").closest("tr")).getByText("Factura (Anulado)")).toBeInTheDocument();
    expect(within(screen.getByText("Viejo SA").closest("tr")).getByText("(legado)")).toBeInTheDocument();
  });

  it("lista las advertencias sobre la tabla", async () => {
    render(<LibroFiscal />);

    await consultar();

    expect(screen.getByText("El libro incluye 3 documento(s) anteriores a la configuración fiscal (calculo legado).")).toBeInTheDocument();
  });

  it("consulta compras con el botón de tipo y descarta el reporte anterior al cambiar de tipo", async () => {
    render(<LibroFiscal />);
    await consultar();

    fireEvent.click(screen.getByRole("button", { name: "Compras" }));

    expect(screen.queryByText("ACME")).toBeNull();
    expect(screen.queryByRole("button", { name: "Exportar CSV" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    await screen.findByText("ACME");
    expect(librosFiscalesApi.compras).toHaveBeenCalledWith(2026, 10);
  });

  it("muestra un mensaje cuando el mes no tiene documentos", async () => {
    librosFiscalesApi.ventas.mockResolvedValue({ data: { ...reporte, filas: [], advertencias: [] } });
    render(<LibroFiscal />);

    await consultar();

    expect(screen.getByText("No hay documentos en el mes seleccionado.")).toBeInTheDocument();
  });

  it("muestra el error del servidor y uno genérico, y oculta el reporte", async () => {
    librosFiscalesApi.ventas.mockRejectedValueOnce({ response: { data: { error: "El mes debe estar entre 1 y 12." } } });
    render(<LibroFiscal />);

    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    expect(await screen.findByText("El mes debe estar entre 1 y 12.")).toBeInTheDocument();

    librosFiscalesApi.ventas.mockRejectedValueOnce(new Error("Network Error"));
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    expect(await screen.findByText("No se pudo consultar el libro.")).toBeInTheDocument();
    expect(screen.queryByText("Totales")).toBeNull();
  });

  it("muestra Cargando mientras llega la respuesta", () => {
    librosFiscalesApi.ventas.mockReturnValue(new Promise(() => {}));
    render(<LibroFiscal />);

    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));

    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Consultar" })).toBeDisabled();
  });

  it("exporta el CSV del reporte consultado con el nombre del año y mes", async () => {
    render(<LibroFiscal />);
    await consultar("2026", "3");
    escribir("Año", "2030");

    fireEvent.click(screen.getByRole("button", { name: "Exportar CSV" }));

    await waitFor(() => expect(descargarCsv).toHaveBeenCalledTimes(1));
    const [nombre, contenido] = descargarCsv.mock.calls[0];
    expect(nombre).toBe("libro-ventas-2026-03.csv");
    expect(contenido.split("\r\n")[0]).toMatch(/^Fecha,Tipo,Serie,Número,NIT,Nombre,/);
    expect(contenido).toContain(",,,,,TOTAL,44.44,111.11,22.22,33.33,166.66");
  });
});
