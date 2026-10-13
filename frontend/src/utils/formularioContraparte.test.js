import { describe, expect, it } from "vitest";
import { contraparteCompleta, formularioContraparteVacio, payloadContraparte, requiereFundamento, requiereTipoAgente } from "./formularioContraparte";

describe("formularioContraparte", () => {
  it("el proveedor inicia con régimen de ISR No definido y el cliente conserva UTILIDADES", () => {
    expect(formularioContraparteVacio("Proveedor").regimenIsr).toBe("NO_DEFINIDO");
    expect(formularioContraparteVacio("Cliente").regimenIsr).toBe("UTILIDADES");
  });

  it("el fundamento se exige solo a proveedores EXENTO o PAGO_DIRECTO", () => {
    const proveedor = formularioContraparteVacio("Proveedor");

    expect(requiereFundamento({ ...proveedor, regimenIsr: "EXENTO" })).toBe(true);
    expect(requiereFundamento({ ...proveedor, regimenIsr: "PAGO_DIRECTO" })).toBe(true);
    expect(requiereFundamento({ ...proveedor, regimenIsr: "SIMPLIFICADO" })).toBe(false);
    expect(requiereFundamento({ ...formularioContraparteVacio("Cliente"), regimenIsr: "EXENTO" })).toBe(false);
  });

  it("el tipo de agente se exige solo a clientes marcados como agente", () => {
    const cliente = formularioContraparteVacio("Cliente");

    expect(requiereTipoAgente({ ...cliente, esAgenteRetencionIva: true })).toBe(true);
    expect(requiereTipoAgente(cliente)).toBe(false);
    expect(requiereTipoAgente({ ...formularioContraparteVacio("Proveedor"), esAgenteRetencionIva: true })).toBe(false);
  });

  it("contraparteCompleta exige fundamento y tipo de agente cuando corresponden", () => {
    const proveedor = { ...formularioContraparteVacio("Proveedor"), regimenIsr: "EXENTO" };
    const cliente = { ...formularioContraparteVacio("Cliente"), esAgenteRetencionIva: true };

    expect(contraparteCompleta(proveedor)).toBe(false);
    expect(contraparteCompleta({ ...proveedor, isrFundamento: "  Resolución 12  " })).toBe(true);
    expect(contraparteCompleta(cliente)).toBe(false);
    expect(contraparteCompleta({ ...cliente, tipoAgenteIva: "AGENTE_PRIVADO" })).toBe(true);
    expect(contraparteCompleta(formularioContraparteVacio("Proveedor"))).toBe(true);
  });

  it("payloadContraparte normaliza el NIT, recorta el fundamento y anula lo que no aplica", () => {
    const proveedor = { ...formularioContraparteVacio("Proveedor"), nit: " 1234567-9 ", regimenIsr: "EXENTO", isrFundamento: " Res. 5 " };
    const cliente = { ...formularioContraparteVacio("Cliente"), nit: "cf", isrFundamento: "sobrante", esAgenteRetencionIva: true, tipoAgenteIva: "SECTOR_PUBLICO" };

    expect(payloadContraparte(proveedor)).toMatchObject({ nit: "1234567-9", isrFundamento: "Res. 5", tipoAgenteIva: null });
    expect(payloadContraparte(cliente)).toMatchObject({ nit: "CF", isrFundamento: null, tipoAgenteIva: "SECTOR_PUBLICO" });
    expect(payloadContraparte(formularioContraparteVacio("Cliente"))).toMatchObject({ isrFundamento: null, tipoAgenteIva: null });
    expect(payloadContraparte({ ...formularioContraparteVacio("Proveedor"), esAgenteRetencionIva: true })).toMatchObject({ esAgenteRetencionIva: false, tipoAgenteIva: null });
  });
});
