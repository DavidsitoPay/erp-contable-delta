import { describe, expect, it } from "vitest";
import { formatoMoneda } from "./formato";
import {
  AJUSTE_VACIO,
  RETENCION_VACIA,
  ajusteValido,
  aplicacionesConRetencion,
  aplicacionesConTipoRenta,
  csvRetenciones,
  descripcionEscala,
  errorRetencionIva,
  filtrosReporte,
  hayErrorRetencionIva,
  netoAPagar,
  nombreArchivoCsv,
  payloadAjuste,
  payloadConstancia,
  permiteAjuste,
  resumenCobro,
  retencionActiva,
  retencionAEnviar,
  tipoRentaInicial,
  valorRetencion,
} from "./retenciones";

const conMonto = (monto, cambios = {}) => ({ ...RETENCION_VACIA, monto, ...cambios });

describe("retención de ISR en la factura", () => {
  it("descripcionEscala describe la tasa simple y la escala con tramo", () => {
    expect(descripcionEscala({ tasaBase: 5, tramoLimite: null, tasaExcedente: null })).toBe("5 %");
    expect(descripcionEscala({ tasaBase: 5, tramoLimite: undefined })).toBe("5 %");
    expect(descripcionEscala({ tasaBase: 5, tramoLimite: 30000, tasaExcedente: 7 })).toBe(
      `5 % hasta ${formatoMoneda.format(30000)} y 7 % sobre el excedente`
    );
  });

  it("permiteAjuste solo cuando hay vista previa y no es NoAplica", () => {
    expect(permiteAjuste(null)).toBe(false);
    expect(permiteAjuste({ estado: "NoAplica" })).toBe(false);
    expect(permiteAjuste({ estado: "Retenida" })).toBe(true);
    expect(permiteAjuste({ estado: "PendienteRevision" })).toBe(true);
  });

  it("ajusteValido exige monto desde 0 y menor al total, y un motivo", () => {
    expect(ajusteValido(AJUSTE_VACIO, 100)).toBe(false);
    expect(ajusteValido({ activo: true, monto: "0", motivo: "Exención" }, 100)).toBe(true);
    expect(ajusteValido({ activo: true, monto: "99.99", motivo: "Exención" }, 100)).toBe(true);
    expect(ajusteValido({ activo: true, monto: "100", motivo: "Exención" }, 100)).toBe(false);
    expect(ajusteValido({ activo: true, monto: "-1", motivo: "Exención" }, 100)).toBe(false);
    expect(ajusteValido({ activo: true, monto: "10", motivo: "  " }, 100)).toBe(false);
  });

  it("payloadAjuste convierte el monto y recorta el motivo", () => {
    expect(payloadAjuste({ activo: true, monto: "12.5", motivo: " Acuerdo " })).toEqual({ monto: 12.5, motivo: "Acuerdo" });
  });

  it("netoAPagar usa el neto de la vista previa o el total menos el ajuste", () => {
    expect(netoAPagar({ neto: 42600 }, false, AJUSTE_VACIO, 44800)).toBe(42600);
    expect(netoAPagar({ neto: 42600 }, true, { monto: "1000.10", motivo: "x" }, 44800)).toBe(43799.9);
  });
});

describe("retención de IVA recibida en el cobro", () => {
  it("retencionActiva usa la casilla marcada o, sin tocarla, la condición de agente del cliente", () => {
    expect(retencionActiva(RETENCION_VACIA, true)).toBe(true);
    expect(retencionActiva(RETENCION_VACIA, false)).toBe(false);
    expect(retencionActiva(RETENCION_VACIA, undefined)).toBe(false);
    expect(retencionActiva({ ...RETENCION_VACIA, activa: false }, true)).toBe(false);
    expect(retencionActiva({ ...RETENCION_VACIA, activa: true }, false)).toBe(true);
  });

  it("valorRetencion devuelve el valor vacío cuando no hay captura", () => {
    expect(valorRetencion({}, 3)).toBe(RETENCION_VACIA);
    expect(valorRetencion({ 3: conMonto("5") }, 3).monto).toBe("5");
  });

  it.each([
    ["sin monto", conMonto(""), 100, undefined, true, ""],
    ["ya retenida", conMonto("10"), 100, { yaRetenida: true }, true, ""],
    ["más de dos decimales", conMonto("10.123"), 100, undefined, true, "El IVA retenido debe tener máximo 2 decimales."],
    ["igual al monto aplicado", conMonto("100"), 100, undefined, true, ""],
    ["mayor al monto aplicado", conMonto("101"), 100, undefined, true, "El IVA retenido no puede exceder el monto aplicado a la factura."],
    [
      "sobre el IVA retenible",
      conMonto("30"),
      100,
      { ivaRetenible: 20 },
      true,
      `El IVA retenido no puede exceder el IVA retenible de la factura (${formatoMoneda.format(20)}).`,
    ],
    ["cliente no agente sin confirmar", conMonto("10"), 100, undefined, false, "Confirme que el cliente retuvo IVA aunque no está registrado como agente de retención."],
    ["cliente no agente confirmado", conMonto("10", { confirma: true }), 100, undefined, false, ""],
    ["constancia sin fecha", conMonto("10", { constanciaNumero: "A-1" }), 100, undefined, true, "Indique la fecha de la constancia."],
    ["válida", conMonto("10", { constanciaNumero: "A-1", constanciaFecha: "2026-10-20" }), 100, { ivaRetenible: 12 }, true, ""],
  ])("errorRetencionIva %s", (_caso, valor, aplicado, sugerencia, agente, esperado) => {
    expect(errorRetencionIva(valor, aplicado, sugerencia, agente)).toBe(esperado);
  });

  it("retencionAEnviar devuelve null si está inactiva, sin monto o ya retenida", () => {
    expect(retencionAEnviar(conMonto("10", { activa: false }), undefined, true)).toBeNull();
    expect(retencionAEnviar(conMonto(""), undefined, true)).toBeNull();
    expect(retencionAEnviar(conMonto("10"), { yaRetenida: true }, true)).toBeNull();
  });

  it("retencionAEnviar arma el cuerpo con la confirmación del cliente no agente y la constancia opcional", () => {
    expect(retencionAEnviar(conMonto("10"), undefined, true)).toEqual({ monto: 10, confirmaClienteNoAgente: false });
    expect(retencionAEnviar(conMonto("10", { activa: true, confirma: true }), undefined, false)).toEqual({ monto: 10, confirmaClienteNoAgente: true });
    expect(retencionAEnviar(conMonto("10", { constanciaNumero: " A-1 ", constanciaFecha: "2026-10-20" }), undefined, true)).toEqual({
      monto: 10,
      confirmaClienteNoAgente: false,
      constancia: { numero: "A-1", fecha: "2026-10-20" },
    });
  });

  it("aplicacionesConRetencion agrega retencionIva solo a las aplicaciones con retención válida", () => {
    const aplicaciones = [
      { documentoId: 1, montoAplicado: 100 },
      { documentoId: 2, montoAplicado: 50 },
    ];

    const resultado = aplicacionesConRetencion(aplicaciones, { 1: conMonto("10") }, {}, true);

    expect(resultado).toEqual([
      { documentoId: 1, montoAplicado: 100, retencionIva: { monto: 10, confirmaClienteNoAgente: false } },
      { documentoId: 2, montoAplicado: 50 },
    ]);
  });

  it("hayErrorRetencionIva detecta capturas inválidas solo en filas con retención activa", () => {
    const aplicaciones = [{ documentoId: 1, montoAplicado: 100 }];

    expect(hayErrorRetencionIva(aplicaciones, { 1: conMonto("101") }, {}, true)).toBe(true);
    expect(hayErrorRetencionIva(aplicaciones, { 1: conMonto("101", { activa: false }) }, {}, true)).toBe(false);
    expect(hayErrorRetencionIva(aplicaciones, { 1: conMonto("10") }, {}, true)).toBe(false);
    expect(hayErrorRetencionIva(aplicaciones, {}, {}, true)).toBe(false);
  });

  it("resumenCobro separa total aplicado, IVA retenido y neto a recibir", () => {
    const aplicaciones = [
      { documentoId: 1, montoAplicado: 11200, retencionIva: { monto: 300 } },
      { documentoId: 2, montoAplicado: 100.5 },
    ];

    expect(resumenCobro(aplicaciones)).toEqual({ totalAplicado: 11300.5, ivaRetenido: 300, neto: 11000.5 });
    expect(resumenCobro([])).toEqual({ totalAplicado: 0, ivaRetenido: 0, neto: 0 });
  });
});

describe("reporte de retenciones", () => {
  const reporte = {
    filas: [
      {
        sentido: "EFECTUADA",
        concepto: "ISR_ROS",
        nit: "1234567-8",
        nombre: "Proveedor, S.A.",
        documentoNumero: "F-001",
        dteSerie: "A",
        dteNumero: "99",
        dteUuid: "uuid-1",
        fechaDocumento: "2026-10-05",
        fechaOperacion: "2026-10-05",
        base: 40000,
        tasa: 5,
        tasaEfectiva: 5.5,
        monto: 2200,
        constanciaNumero: null,
        constanciaFecha: null,
        formulario: null,
        estadoConstancia: "PENDIENTE",
      },
    ],
  };

  it("nombreArchivoCsv rellena el mes a dos dígitos", () => {
    expect(nombreArchivoCsv(2026, 3)).toBe("retenciones-2026-03.csv");
    expect(nombreArchivoCsv("2026", "10")).toBe("retenciones-2026-10.csv");
  });

  it("csvRetenciones usa los encabezados fijos y escribe una fila por retención", () => {
    const lineas = csvRetenciones(reporte).split("\r\n");

    expect(lineas[0]).toBe(
      "Sentido,Impuesto,NIT,Nombre,Documento,Serie DTE,Numero DTE,UUID,Fecha documento,Fecha pago o cobro,Base,Tasa,Retencion,Constancia,Fecha constancia,Formulario,Estado constancia,Tipo de renta (codigo),Tipo de renta,Moneda,Monto origen,Tipo de cambio,Fecha tipo de cambio,Enterado,Saldo por enterar"
    );
    expect(lineas[1]).toBe('Efectuada,ISR,1234567-8,"Proveedor, S.A.",F-001,A,99,uuid-1,2026-10-05,2026-10-05,40000.00,5.5,2200.00,,,,PENDIENTE,,,,,,,0.00,0.00');
    expect(lineas).toHaveLength(2);
  });

  it("csvRetenciones escribe ISR no residentes con su tipo de renta, moneda y tipo de cambio", () => {
    const noResidente = {
      filas: [
        {
          ...reporte.filas[0],
          concepto: "ISR_NORES",
          tasaEfectiva: 15,
          tipoRentaCodigo: "SERV",
          tipoRentaNombre: "Servicios",
          monedaCodigo: "USD",
          montoOrigen: 1000.5,
          tipoCambio: 7.8,
          tipoCambioFecha: "2026-10-05",
          enteradoGtq: 2000,
          saldoPorEnterarGtq: 1200,
        },
      ],
    };

    expect(csvRetenciones(noResidente).split("\r\n")[1]).toBe(
      'Efectuada,ISR no residentes,1234567-8,"Proveedor, S.A.",F-001,A,99,uuid-1,2026-10-05,2026-10-05,40000.00,15,2200.00,,,,PENDIENTE,SERV,Servicios,USD,1000.5,7.8,2026-10-05,2000.00,1200.00'
    );
  });

  it("csvRetenciones sin filas deja solo el encabezado y usa la tasa base si no hay tasa efectiva", () => {
    expect(csvRetenciones({ filas: [] }).split("\r\n")).toHaveLength(1);

    const sinEfectiva = { filas: [{ ...reporte.filas[0], tasaEfectiva: undefined, concepto: "IVA_CLIENTE", sentido: "RECIBIDA" }] };
    expect(csvRetenciones(sinEfectiva).split("\r\n")[1]).toMatch(/^Recibida,IVA,.*,40000\.00,5,2200\.00,/);
  });

  it("filtrosReporte convierte año y mes a número y omite los filtros vacíos", () => {
    expect(filtrosReporte({ anio: "2026", mes: "10", sentido: "", concepto: "ISR_ROS", nit: " 123 ", estadoConstancia: "" })).toEqual({
      anio: 2026,
      mes: 10,
      concepto: "ISR_ROS",
      nit: "123",
    });
  });

  it("payloadConstancia recorta el número y convierte el período declarado al primer día", () => {
    expect(
      payloadConstancia({ numero: " A-1 ", fecha: "2026-10-05", formulario: "SAT-1331", fechaPagoFormulario: "2026-11-02", periodoDeclarado: "2026-10" })
    ).toEqual({ numero: "A-1", fecha: "2026-10-05", formulario: "SAT-1331", fechaPagoFormulario: "2026-11-02", periodoDeclarado: "2026-10-01" });
    expect(payloadConstancia({ numero: "B", fecha: "2026-10-05", formulario: "", fechaPagoFormulario: "", periodoDeclarado: "" })).toEqual({
      numero: "B",
      fecha: "2026-10-05",
      formulario: null,
      fechaPagoFormulario: null,
      periodoDeclarado: null,
    });
  });
});

describe("tipo de renta en el pago a no residentes", () => {
  it("tipoRentaInicial toma el tipo del proveedor o vacío", () => {
    expect(tipoRentaInicial({ tipoRentaNrCodigo: "SERV" })).toBe("SERV");
    expect(tipoRentaInicial({ tipoRentaNrCodigo: null })).toBe("");
    expect(tipoRentaInicial(undefined)).toBe("");
  });

  it("aplicacionesConTipoRenta usa el tipo elegido por factura y, si no hay, el de por defecto", () => {
    const aplicaciones = [
      { documentoId: 1, montoAplicado: 10 },
      { documentoId: 2, montoAplicado: 20 },
    ];

    expect(aplicacionesConTipoRenta(aplicaciones, { 2: "OTRO" }, "SERV")).toEqual([
      { documentoId: 1, montoAplicado: 10, tipoRentaCodigo: "SERV" },
      { documentoId: 2, montoAplicado: 20, tipoRentaCodigo: "OTRO" },
    ]);
    expect(aplicacionesConTipoRenta(aplicaciones, {}, "")).toEqual(aplicaciones);
  });
});
