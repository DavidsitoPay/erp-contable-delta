import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { alternarTema, aplicarTema, guardarTema, iniciarSincronizacionTema, obtenerTemaActual, resolverTemaInicial } from "./tema";

function stubMatchMedia(matches) {
  vi.stubGlobal("matchMedia", vi.fn(() => ({ matches, addEventListener: vi.fn() })));
}

function iniciarYCapturar(mediaQuery) {
  vi.stubGlobal("matchMedia", vi.fn(() => mediaQuery));
  const espia = vi.spyOn(window, "addEventListener").mockImplementation(() => {});
  iniciarSincronizacionTema();
  const handlerStorage = espia.mock.calls.find(([nombre]) => nombre === "storage")[1];
  return handlerStorage;
}

beforeEach(() => {
  delete document.documentElement.dataset.theme;
  localStorage.clear();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  delete document.documentElement.dataset.theme;
});

describe("resolverTemaInicial", () => {
  it("devuelve el tema guardado claro", () => {
    localStorage.setItem("delta_tema", "claro");
    stubMatchMedia(true);
    expect(resolverTemaInicial()).toBe("claro");
  });

  it("devuelve el tema guardado oscuro", () => {
    localStorage.setItem("delta_tema", "oscuro");
    stubMatchMedia(false);
    expect(resolverTemaInicial()).toBe("oscuro");
  });

  it("usa la preferencia del sistema oscura si no hay tema guardado", () => {
    stubMatchMedia(true);
    expect(resolverTemaInicial()).toBe("oscuro");
  });

  it("usa la preferencia del sistema clara si no hay tema guardado", () => {
    stubMatchMedia(false);
    expect(resolverTemaInicial()).toBe("claro");
  });

  it("ignora un valor guardado invalido", () => {
    localStorage.setItem("delta_tema", "otro");
    stubMatchMedia(true);
    expect(resolverTemaInicial()).toBe("oscuro");
  });

  it("cae a la preferencia del sistema si localStorage lanza", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("bloqueado");
    });
    stubMatchMedia(true);
    expect(resolverTemaInicial()).toBe("oscuro");
  });

  it("devuelve claro si matchMedia no esta disponible", () => {
    vi.stubGlobal("matchMedia", undefined);
    expect(resolverTemaInicial()).toBe("claro");
  });
});

describe("aplicarTema", () => {
  it("fija data-theme y emite delta:tema-cambiado", () => {
    const escucha = vi.fn();
    window.addEventListener("delta:tema-cambiado", escucha);
    aplicarTema("oscuro");
    window.removeEventListener("delta:tema-cambiado", escucha);
    expect(document.documentElement.dataset.theme).toBe("oscuro");
    expect(escucha).toHaveBeenCalledTimes(1);
    expect(escucha.mock.calls[0][0].detail).toBe("oscuro");
  });
});

describe("guardarTema", () => {
  it("guarda el tema en localStorage", () => {
    guardarTema("oscuro");
    expect(localStorage.getItem("delta_tema")).toBe("oscuro");
  });

  it("ignora el error si localStorage lanza", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("lleno");
    });
    expect(() => guardarTema("claro")).not.toThrow();
  });
});

describe("alternarTema y obtenerTemaActual", () => {
  it("obtenerTemaActual devuelve claro por defecto", () => {
    expect(obtenerTemaActual()).toBe("claro");
  });

  it("obtenerTemaActual devuelve el tema aplicado", () => {
    aplicarTema("oscuro");
    expect(obtenerTemaActual()).toBe("oscuro");
  });

  it("alterna de claro a oscuro y lo guarda", () => {
    expect(alternarTema()).toBe("oscuro");
    expect(document.documentElement.dataset.theme).toBe("oscuro");
    expect(localStorage.getItem("delta_tema")).toBe("oscuro");
  });

  it("alterna de oscuro a claro", () => {
    aplicarTema("oscuro");
    expect(alternarTema()).toBe("claro");
    expect(localStorage.getItem("delta_tema")).toBe("claro");
  });
});

describe("iniciarSincronizacionTema", () => {
  it("aplica el tema de un evento storage valido", () => {
    const h = iniciarYCapturar({ addEventListener: vi.fn() });
    h({ key: "delta_tema", newValue: "oscuro" });
    expect(document.documentElement.dataset.theme).toBe("oscuro");
  });

  it("ignora eventos storage de otra clave", () => {
    const h = iniciarYCapturar({ addEventListener: vi.fn() });
    h({ key: "otra", newValue: "oscuro" });
    expect(document.documentElement.dataset.theme).toBeUndefined();
  });

  it("ignora eventos storage con valor invalido o nulo", () => {
    const h = iniciarYCapturar({ addEventListener: vi.fn() });
    h({ key: "delta_tema", newValue: "x" });
    h({ key: "delta_tema", newValue: null });
    expect(document.documentElement.dataset.theme).toBeUndefined();
  });

  describe("cambio de preferencia del sistema", () => {
    function capturarChange() {
      const addEventListener = vi.fn();
      iniciarYCapturar({ addEventListener });
      return addEventListener.mock.calls[0][1];
    }

    it("registra el listener change en matchMedia", () => {
      const addEventListener = vi.fn();
      vi.stubGlobal("matchMedia", vi.fn(() => ({ addEventListener })));
      vi.spyOn(window, "addEventListener").mockImplementation(() => {});
      iniciarSincronizacionTema();
      expect(addEventListener).toHaveBeenCalledWith("change", expect.any(Function));
    });

    it("sigue la preferencia del sistema si no hay tema guardado", () => {
      const handler = capturarChange();
      handler({ matches: true });
      expect(document.documentElement.dataset.theme).toBe("oscuro");
      handler({ matches: false });
      expect(document.documentElement.dataset.theme).toBe("claro");
    });

    it("ignora el cambio del sistema si hay tema guardado", () => {
      localStorage.setItem("delta_tema", "claro");
      const handler = capturarChange();
      handler({ matches: true });
      expect(document.documentElement.dataset.theme).toBeUndefined();
    });

    it("sigue la preferencia del sistema si el tema guardado es invalido", () => {
      localStorage.setItem("delta_tema", "x");
      const handler = capturarChange();
      handler({ matches: true });
      expect(document.documentElement.dataset.theme).toBe("oscuro");
    });

    it("ignora el error de localStorage dentro del handler", () => {
      const handler = capturarChange();
      vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
        throw new Error("x");
      });
      expect(() => handler({ matches: true })).not.toThrow();
      expect(document.documentElement.dataset.theme).toBeUndefined();
    });
  });

  it("no falla si matchMedia no esta disponible", () => {
    vi.stubGlobal("matchMedia", undefined);
    const espia = vi.spyOn(window, "addEventListener").mockImplementation(() => {});
    expect(() => iniciarSincronizacionTema()).not.toThrow();
    expect(espia).toHaveBeenCalledWith("storage", expect.any(Function));
  });
});
