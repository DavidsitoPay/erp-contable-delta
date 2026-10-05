import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cuentasApi, cuentasBancariasApi } from "../../services/api";
import { useCatalogosTesoreria } from "./useCatalogosTesoreria";

vi.mock("../../services/api", () => ({
  cuentasApi: { listar: vi.fn() },
  cuentasBancariasApi: { listar: vi.fn() },
}));

describe("useCatalogosTesoreria", () => {
  beforeEach(() => {
    cuentasApi.listar.mockReset();
    cuentasBancariasApi.listar.mockReset();
    cuentasApi.listar.mockResolvedValue({ data: [] });
    cuentasBancariasApi.listar.mockResolvedValue({ data: [] });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("carga cuentas bancarias y contables", async () => {
    const { result } = renderHook(() => useCatalogosTesoreria("cuentas"));

    await waitFor(() => expect(cuentasBancariasApi.listar).toHaveBeenCalledWith(true));
    await waitFor(() => expect(cuentasApi.listar).toHaveBeenCalledWith(true));

    expect(result.current.cuentasBancarias).toEqual([]);
    expect(result.current.cuentasContables).toEqual([]);
    expect(result.current.error).toBe("");
  });

  it("establece error cuando falla cargar cuentas bancarias", async () => {
    cuentasBancariasApi.listar.mockRejectedValueOnce(new Error("red"));
    const { result } = renderHook(() => useCatalogosTesoreria("cuentas"));

    await waitFor(() => expect(result.current.error).toBe("No se pudieron cargar las cuentas bancarias."));
  });

  it("establece error cuando falla cargar cuentas contables", async () => {
    cuentasApi.listar.mockRejectedValueOnce(new Error("red"));
    const { result } = renderHook(() => useCatalogosTesoreria("cuentas"));

    await waitFor(() => expect(result.current.error).toBe("No se pudieron cargar las cuentas contables."));
  });
});
