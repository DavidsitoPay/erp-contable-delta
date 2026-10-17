import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import { useCambioDocumento } from "./useCambioDocumento";

vi.mock("../../services/api", () => ({ tiposCambioApi: { consultar: vi.fn() } }));

const disponible = { data: { disponible: true, tipoCambio: { tasa: 7.6, fuente: "Banguat", arrastrado: false }, propuesta: null, mensaje: null } };
const origenUsd = { moneda: "USD", tipoCambioAplicado: 7.6, tipoCambioFuente: "Banguat", tipoCambioArrastrado: false };

async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("useCambioDocumento", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    tiposCambioApi.consultar.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("en GTQ usa tasa 1 y no consulta", async () => {
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "GTQ", fecha: "2026-10-12", esNotaCredito: false }));

    await avanzar(500);

    expect(result.current.moneda).toBe("GTQ");
    expect(result.current.tasa).toBe(1);
    expect(result.current.bloqueado).toBe(false);
    expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
  });

  it("en USD consulta la tasa y queda bloqueado hasta recibirla", async () => {
    tiposCambioApi.consultar.mockResolvedValue(disponible);
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "USD", fecha: "2026-10-12", esNotaCredito: false }));

    expect(result.current.bloqueado).toBe(true);
    expect(result.current.tasa).toBeNull();

    await avanzar(300);

    expect(result.current.tasa).toBe(7.6);
    expect(result.current.bloqueado).toBe(false);
    expect(result.current.estado.tipoCambio.tasa).toBe(7.6);
  });

  it("una nota de crédito hereda la moneda y la tasa de su origen sin consultar", () => {
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "GTQ", fecha: "2026-10-12", origen: origenUsd, esNotaCredito: true }));

    expect(result.current.moneda).toBe("USD");
    expect(result.current.tasa).toBe(7.6);
    expect(result.current.bloqueado).toBe(false);
    expect(result.current.estado.heredado).toBe(true);
    expect(tiposCambioApi.consultar).not.toHaveBeenCalled();
  });

  it("una nota de crédito sin origen usa GTQ", () => {
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "GTQ", fecha: "2026-10-12", origen: undefined, esNotaCredito: true }));

    expect(result.current.moneda).toBe("GTQ");
    expect(result.current.tasa).toBe(1);
    expect(result.current.bloqueado).toBe(false);
  });

  it("una nota de crédito sobre un origen en GTQ usa tasa 1", () => {
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "GTQ", fecha: "2026-10-12", origen: {}, esNotaCredito: true }));

    expect(result.current.moneda).toBe("GTQ");
    expect(result.current.tasa).toBe(1);
  });

  it("bloquea cuando Banguat no entrega tasa", async () => {
    tiposCambioApi.consultar.mockResolvedValue({ data: { disponible: false, tipoCambio: null, propuesta: null, mensaje: "Banguat no respondió; capture la tasa manual o reintente." } });
    const { result } = renderHook(() => useCambioDocumento({ monedaCodigo: "USD", fecha: "2026-10-12", esNotaCredito: false }));

    await avanzar(300);

    expect(result.current.bloqueado).toBe(true);
  });
});
