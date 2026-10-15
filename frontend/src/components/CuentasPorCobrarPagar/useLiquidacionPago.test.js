import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { tiposCambioApi } from "../../services/api";
import { useLiquidacionPago } from "./useLiquidacionPago";

vi.mock("../../services/api", () => ({ tiposCambioApi: { consultar: vi.fn() } }));

const usd = { id: 7, numero: "F-USD", moneda: "USD", saldoPendiente: 1120, saldoPendienteGtq: 8512, tipoCambioAplicado: 7.6 };
const gtq = { id: 1, numero: "F-1", saldoPendiente: 100 };
const cuentaGtq = { id: 7, monedaCodigo: "GTQ" };
const cuentaUsd = { id: 9, monedaCodigo: "USD" };
const tasa77 = { data: { disponible: true, tipoCambio: { id: 2, tasa: 7.7, fuente: "Banguat", arrastrado: false }, propuesta: null, mensaje: null } };

async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

function armar(props) {
  return renderHook((p) => useLiquidacionPago(p), {
    initialProps: { cuenta: cuentaGtq, fecha: "2026-11-10", facturas: [], montosAplicados: {}, esCobro: true, ...props },
  });
}

describe("useLiquidacionPago", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    tiposCambioApi.consultar.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("cuenta GTQ con factura GTQ no pide efectivo ni estima diferencial", () => {
    const { result } = armar({ facturas: [gtq], montosAplicados: { 1: "50" } });

    expect(result.current.tabla.pideEfectivo).toBe(false);
    expect(result.current.resumen).toBeNull();
    expect(result.current.listo).toBe(true);
    const aplicaciones = [{ documentoId: 1, montoAplicado: 50 }];
    expect(result.current.conEfectivo(aplicaciones)).toEqual(aplicaciones);
  });

  it("cuenta GTQ con factura USD pide el efectivo en quetzales y estima el diferencial al completarlo", () => {
    const { result } = armar({ facturas: [usd], montosAplicados: { 7: "400" } });

    expect(result.current.tabla.pideEfectivo).toBe(true);
    expect(result.current.listo).toBe(false);
    expect(result.current.resumen).toBeNull();

    act(() => result.current.tabla.onEfectivo(7, "3080"));

    expect(result.current.listo).toBe(true);
    expect(result.current.resumen).toEqual({ totalAplicado: 400, totalValor: 3080, diferencial: 40 });
    expect(result.current.conEfectivo([{ documentoId: 7, montoAplicado: 400 }])).toEqual([{ documentoId: 7, montoAplicado: 400, valorEfectivoGtq: 3080 }]);
  });

  it("cuenta USD valora el pago con la tasa de la fecha", async () => {
    tiposCambioApi.consultar.mockResolvedValue(tasa77);
    const { result } = armar({ cuenta: cuentaUsd, facturas: [usd], montosAplicados: { 7: "400" } });

    expect(result.current.listo).toBe(false);
    expect(result.current.resumen).toBeNull();

    await avanzar(300);

    expect(result.current.cambioPago.tasa).toBe(7.7);
    expect(result.current.tabla.pideEfectivo).toBe(false);
    expect(result.current.listo).toBe(true);
    expect(result.current.resumen).toEqual({ totalAplicado: 400, totalValor: 3080, diferencial: 40 });
  });

  it("en un pago el diferencial tiene signo contrario", async () => {
    tiposCambioApi.consultar.mockResolvedValue(tasa77);
    const { result } = armar({ cuenta: cuentaUsd, facturas: [usd], montosAplicados: { 7: "400" }, esCobro: false });

    await avanzar(300);

    expect(result.current.resumen.diferencial).toBe(-40);
  });

  it("rechaza la mezcla de una factura GTQ con una cuenta en dólares (RN-27)", () => {
    const { result } = armar({ cuenta: cuentaUsd, facturas: [gtq], montosAplicados: { 1: "100" } });

    expect(result.current.errorMonedas).toMatch(/^RN-27/);
    expect(result.current.listo).toBe(false);
  });

  it("reiniciar limpia los efectivos capturados", () => {
    const { result } = armar({ facturas: [usd], montosAplicados: { 7: "400" } });
    act(() => result.current.tabla.onEfectivo(7, "3080"));

    act(() => result.current.reiniciar());

    expect(result.current.tabla.efectivos).toEqual({});
  });
});
