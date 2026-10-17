import { describe, expect, it } from "vitest";
import { contraparteCompleta, formularioContraparteVacio, payloadContraparte, requiereFundamento, requierePais, requiereTipoAgente, requiereTipoRenta } from "./formularioContraparte";

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

  it("el tipo de renta se exige solo a proveedores no residentes fuera de EXENTO", () => {
    const noResidente = { ...formularioContraparteVacio("Proveedor"), esResidente: false };

    expect(requiereTipoRenta(noResidente)).toBe(true);
    expect(requiereTipoRenta({ ...noResidente, regimenIsr: "EXENTO" })).toBe(false);
    expect(requiereTipoRenta(formularioContraparteVacio("Proveedor"))).toBe(false);
    expect(requiereTipoRenta({ ...formularioContraparteVacio("Cliente"), esResidente: false })).toBe(false);
  });

  it("contraparteCompleta exige tipo de renta a proveedores no residentes", () => {
    const noResidente = { ...formularioContraparteVacio("Proveedor"), esResidente: false, regimenIsr: "SIMPLIFICADO" };

    expect(contraparteCompleta(noResidente)).toBe(false);
    expect(contraparteCompleta({ ...noResidente, tipoRentaNrCodigo: "NR_SERVICIOS" })).toBe(true);
    expect(contraparteCompleta({ ...noResidente, regimenIsr: "EXENTO", isrFundamento: "Res. 1" })).toBe(true);
  });

  it("payloadContraparte envía el tipo de renta solo a proveedores no residentes", () => {
    const noResidente = { ...formularioContraparteVacio("Proveedor"), esResidente: false, tipoRentaNrCodigo: "NR_SERVICIOS" };

    expect(payloadContraparte(noResidente)).toMatchObject({ tipoRentaNrCodigo: "NR_SERVICIOS" });
    expect(payloadContraparte({ ...noResidente, esResidente: true })).toMatchObject({ tipoRentaNrCodigo: null });
    expect(payloadContraparte({ ...formularioContraparteVacio("Cliente"), esResidente: false, tipoRentaNrCodigo: "NR_SERVICIOS" })).toMatchObject({ tipoRentaNrCodigo: null });
  });

  it("el país ISO-2 se exige solo a contrapartes no residentes y se normaliza en el payload", () => {
    const residente = formularioContraparteVacio("Cliente");
    const extranjero = { ...residente, esResidente: false };

    expect(requierePais(residente)).toBe(false);
    expect(payloadContraparte(residente)).toMatchObject({ pais: null });
    expect(contraparteCompleta(residente)).toBe(true);
    expect(contraparteCompleta({ ...extranjero, pais: "" })).toBe(false);
    expect(contraparteCompleta({ ...extranjero, pais: "G" })).toBe(false);
    expect(contraparteCompleta({ ...extranjero, pais: "G1" })).toBe(false);
    expect(contraparteCompleta({ ...extranjero, pais: "gt" })).toBe(true);
    expect(payloadContraparte({ ...extranjero, pais: " gt " })).toMatchObject({ pais: "GT" });
  });
});
