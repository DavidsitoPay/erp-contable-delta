import { useEffect, useState } from "react";
import { impuestosApi } from "../../services/api";
import { calcularResumen, dtePayload, dteValido, impuestoDeLinea, impuestoPorDefecto, impuestosPermitidos } from "../../utils/fiscal";
import { hoyIso, mensajeError, monto } from "../../utils/formato";
import { useLineas } from "../useLineas";
import CamposCabeceraFactura from "./CamposCabeceraFactura";
import DatosDte from "./DatosDte";
import LineasFactura from "./LineasFactura";
import ResumenFactura from "./ResumenFactura";

const DTE_VACIO = { dteUuid: "", dteSerie: "", dteNumero: "", dteFechaCertificacion: "" };

function lineaVacia(tipoBienServicio) {
  return {
    id: crypto.randomUUID(),
    descripcion: "",
    cantidad: "1",
    precioUnitario: "",
    impuestoId: "",
    tipoBienServicio,
    centroCostoId: "",
    cuentaContableId: "",
  };
}

function FormularioFactura({ config, contrapartes, hojas, cuentasControl, centros, periodosAbiertos, facturas, onRegistrada }) {
  const { campoContraparteId, api, aplicaImpuestoA, etiquetaIva, etiquetaCuentaLinea, dteObligatorio, tipoBienDefecto } = config;

  function formVacio() {
    return {
      numero: "",
      tipoDocumento: "Factura",
      [campoContraparteId]: "",
      documentoOrigenId: "",
      fecha: hoyIso(),
      fechaVencimiento: hoyIso(),
      periodoId: "",
      cuentaControlId: "",
    };
  }

  const [form, setForm] = useState(formVacio);
  const [dte, setDte] = useState(DTE_VACIO);
  const [impuestos, setImpuestos] = useState([]);
  const [error, setError] = useState("");
  const [enviando, setEnviando] = useState(false);
  const { lineas, actualizarLinea, agregarLinea, quitarLinea, reiniciarLineas } = useLineas(() => lineaVacia(tipoBienDefecto), 1);

  useEffect(() => {
    if (!form.fecha) return undefined;
    let vigente = true;
    impuestosApi
      .listar({ vigenteEn: form.fecha, aplicaA: aplicaImpuestoA })
      .then(({ data }) => {
        if (vigente) setImpuestos(data);
      })
      .catch((err) => {
        if (vigente) setError(mensajeError(err, "No se pudieron cargar los impuestos."));
      });
    return () => {
      vigente = false;
    };
  }, [form.fecha, aplicaImpuestoA]);

  const contraparte = contrapartes.find((c) => String(c.id) === String(form[campoContraparteId]));
  const regimenIva = contraparte?.regimenIva;
  const permitidos = impuestosPermitidos(impuestos, regimenIva);
  const defecto = impuestoPorDefecto(permitidos, regimenIva);
  const resumen = calcularResumen(
    lineas.map((l) => ({
      cantidad: monto(l.cantidad),
      precioUnitario: monto(l.precioUnitario),
      impuesto: impuestoDeLinea(l, permitidos, defecto),
    }))
  );

  const esNotaCredito = form.tipoDocumento === "NotaCredito";
  const origenes = facturas.filter(
    (f) =>
      f.tipoDocumento === "Factura" &&
      f.estado === "Vigente" &&
      f.saldoPendiente > 0 &&
      String(f[campoContraparteId]) === String(form[campoContraparteId])
  );
  const origen = origenes.find((o) => String(o.id) === form.documentoOrigenId);
  const excedeSaldo = esNotaCredito && origen !== undefined && resumen.total > origen.saldoPendiente;
  const origenCompleto = !esNotaCredito || (origen !== undefined && !excedeSaldo);

  const encabezadoCompleto =
    form.numero.trim() !== "" && form[campoContraparteId] !== "" && form.periodoId !== "" && form.cuentaControlId !== "";
  const lineasCompletas = lineas.every((l) => l.cuentaContableId !== "" && monto(l.cantidad) > 0);
  const puedeEnviar =
    !enviando && encabezadoCompleto && lineasCompletas && defecto !== null && dteValido(dte, dteObligatorio) && origenCompleto;

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      const payload = {
        numero: form.numero.trim(),
        tipoDocumento: form.tipoDocumento,
        [campoContraparteId]: Number(form[campoContraparteId]),
        fecha: form.fecha,
        fechaVencimiento: form.fechaVencimiento,
        periodoId: Number(form.periodoId),
        cuentaControlId: Number(form.cuentaControlId),
        ...(esNotaCredito ? { documentoOrigenId: Number(form.documentoOrigenId) } : {}),
        ...dtePayload(dte),
        lineas: lineas.map((l) => ({
          descripcion: l.descripcion || null,
          cantidad: monto(l.cantidad),
          precioUnitario: monto(l.precioUnitario),
          impuestoId: impuestoDeLinea(l, permitidos, defecto).id,
          tipoBienServicio: l.tipoBienServicio,
          centroCostoId: l.centroCostoId ? Number(l.centroCostoId) : null,
          cuentaContableId: Number(l.cuentaContableId),
        })),
      };
      await api.crearFactura(payload);
      setForm({ ...formVacio(), periodoId: form.periodoId, cuentaControlId: form.cuentaControlId });
      setDte(DTE_VACIO);
      reiniciarLineas();
      await onRegistrada();
    } catch (err) {
      setError(mensajeError(err, "No se pudo registrar la factura."));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <CamposCabeceraFactura
        form={form}
        onCambio={setForm}
        config={config}
        contrapartes={contrapartes}
        periodosAbiertos={periodosAbiertos}
        cuentasControl={cuentasControl}
        origenes={origenes}
        esNotaCredito={esNotaCredito}
      />

      <DatosDte valor={dte} onCambio={setDte} obligatorio={dteObligatorio} />

      <LineasFactura
        lineas={lineas}
        resumen={resumen}
        permitidos={permitidos}
        defecto={defecto}
        hojas={hojas}
        centros={centros}
        etiquetaCuentaLinea={etiquetaCuentaLinea}
        onActualizar={actualizarLinea}
        onQuitar={quitarLinea}
      />

      <div className="lineas-toolbar">
        <button type="button" className="btn btn-outline btn-sm" onClick={agregarLinea}>+ Agregar línea</button>
      </div>

      <ResumenFactura resumen={resumen} etiquetaIva={etiquetaIva} esCompra={aplicaImpuestoA === "COMPRAS"} />

      {excedeSaldo && <p className="warning-chip">El monto de la nota de crédito excede el saldo pendiente del documento de origen.</p>}
      {error && <p className="error-chip">{error}</p>}

      <button type="submit" disabled={!puedeEnviar} className="btn btn-primary">
        {enviando ? "Registrando..." : "Registrar factura"}
      </button>
    </form>
  );
}

export default FormularioFactura;
