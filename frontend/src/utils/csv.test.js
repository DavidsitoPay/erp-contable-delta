import { afterEach, describe, expect, it, vi } from "vitest";
import { construirCsv, descargarCsv } from "./csv";

function leerBytes(blob) {
  return new Promise((resolve) => {
    const lector = new FileReader();
    lector.onload = () => resolve(new Uint8Array(lector.result));
    lector.readAsArrayBuffer(blob);
  });
}

describe("construirCsv", () => {
  const columnas = [
    { clave: "nombre", titulo: "Nombre" },
    { clave: "total", titulo: "Total", numerica: true },
  ];

  it("separa con comas, usa punto decimal y dos decimales en columnas numéricas", () => {
    expect(construirCsv(columnas, [{ nombre: "ACME", total: 1234.5 }])).toBe("Nombre,Total\r\nACME,1234.50");
  });

  it("entrecomilla y duplica comillas cuando el texto contiene separadores", () => {
    const csv = construirCsv(columnas, [{ nombre: 'Casa "Luz", S.A.', total: 1 }]);

    expect(csv.split("\r\n")[1]).toBe('"Casa ""Luz"", S.A.",1.00');
  });

  it("deja vacíos los textos nulos y trata los montos ausentes como cero", () => {
    expect(construirCsv(columnas, [{ nombre: null }])).toBe("Nombre,Total\r\n,0.00");
  });
});

describe("descargarCsv", () => {
  afterEach(() => {
    delete URL.createObjectURL;
    delete URL.revokeObjectURL;
    vi.restoreAllMocks();
  });

  it("genera un blob UTF-8 con BOM, lo descarga con el nombre indicado y libera la URL", async () => {
    const crear = vi.fn(() => "blob:fake");
    URL.createObjectURL = crear;
    URL.revokeObjectURL = vi.fn();
    let descarga = null;
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function registrar() {
      descarga = { href: this.href, download: this.download };
    });

    descargarCsv("libro-ventas-2026-10.csv", "a,b");

    expect(descarga).toEqual({ href: "blob:fake", download: "libro-ventas-2026-10.csv" });
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:fake");
    const blob = crear.mock.calls[0][0];
    expect(blob.type).toBe("text/csv;charset=utf-8");
    expect([...(await leerBytes(blob)).slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf]);
  });
});
