import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { avanzar, usarTemporizadoresFalsos } from "../../test/temporizadores";
import { useRetencionPreviaPago } from "./useRetencionPreviaPago";

const previa = { aplica: true, lineas: [], totalBaseGtq: 40, totalRetencionGtq: 6, netoBancoGtq: 34, advertencias: [] };
const aplicaciones = [{ documentoId: 21, montoAplicado: 40, tipoRentaCodigo: "SERV" }];
const datos = { activo: true, proveedorId: "5", fecha: "2026-10-05", aplicaciones };

describe("useRetencionPreviaPago", () => {
  usarTemporizadoresFalsos();

  it.each([
    ["está inactivo", { activo: false }],
    ["no hay proveedor", { proveedorId: "" }],
    ["no hay fecha", { fecha: "" }],
    ["no hay aplicaciones", { aplicaciones: [] }],
  ])("no consulta cuando %s", async (_caso, cambios) => {
    const api = { retencionPreviaPago: vi.fn() };
    const { result } = renderHook(() => useRetencionPreviaPago({ api, ...datos, ...cambios }));

    await avanzar(500);

    expect(api.retencionPreviaPago).not.toHaveBeenCalled();
    expect(result.current.previa).toBeNull();
  });

  it("consulta con las aplicaciones con tipo de renta y publica la vista previa", async () => {
    const api = { retencionPreviaPago: vi.fn().mockResolvedValue({ data: previa }) };
    const { result } = renderHook(() => useRetencionPreviaPago({ api, ...datos }));

    await avanzar(300);

    expect(api.retencionPreviaPago).toHaveBeenCalledWith({ proveedorId: 5, fecha: "2026-10-05", aplicaciones });
    expect(result.current).toMatchObject({ previa, error: "", bloqueada: false, cargando: false });
  });

  it("un 409 publica el error y bloquea; un 400 publica su mensaje sin bloquear", async () => {
    const api = {
      retencionPreviaPago: vi
        .fn()
        .mockRejectedValueOnce({ response: { status: 409, data: { error: "Falta el tipo de renta." } } })
        .mockRejectedValueOnce({ response: { status: 400, data: { error: "Tipo de renta inválido." } } }),
    };
    const { result, rerender } = renderHook((props) => useRetencionPreviaPago(props), { initialProps: { api, ...datos } });

    await avanzar(300);
    expect(result.current).toMatchObject({ error: "Falta el tipo de renta.", bloqueada: true, previa: null });

    rerender({ api, ...datos, aplicaciones: [{ ...aplicaciones[0], tipoRentaCodigo: "OTRO" }] });
    await avanzar(300);
    expect(result.current).toMatchObject({ error: "Tipo de renta inválido.", bloqueada: false });
  });
});
