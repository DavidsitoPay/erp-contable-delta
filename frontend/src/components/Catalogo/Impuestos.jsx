import { useEffect, useState } from "react";
import { impuestosApi } from "../../services/api";
import { AMBITOS_IMPUESTO, TIPOS_IMPUESTO, etiquetaDe } from "../../utils/etiquetasFiscales";
import { FORMULARIO_VACIO, formularioDesdeImpuesto, payloadCierre, payloadCreacion, payloadEdicion } from "../../utils/formularioImpuesto";
import { hoyIso, mensajeError } from "../../utils/formato";
import CampoFormulario from "../CampoFormulario";
import { TablaCatalogo } from "./CatalogoCrud";
import FormularioImpuesto from "./FormularioImpuesto";

const ENCABEZADOS = ["Nombre", "Tipo", "Tasa (%)", "Aplica a", "Crédito fiscal", "Artículo legal", "Vigencia", "Estado"];

function AccionesImpuesto({ impuesto, onEditar, onVersion, onCerrar, onDesactivar }) {
  return (
    <td>
      <div className="acciones-fila">
        <button type="button" className="btn btn-outline btn-sm" aria-label={`Editar ${impuesto.nombre}`} onClick={() => onEditar(impuesto)}>
          Editar
        </button>
        <button type="button" className="btn btn-outline btn-sm" aria-label={`Nueva versión de ${impuesto.nombre}`} onClick={() => onVersion(impuesto)}>
          Nueva versión
        </button>
        {impuesto.activo && !impuesto.vigenteHasta && (
          <button type="button" className="btn btn-outline btn-sm" aria-label={`Cerrar vigencia de ${impuesto.nombre}`} onClick={() => onCerrar(impuesto)}>
            Cerrar vigencia
          </button>
        )}
        {impuesto.activo && (
          <button type="button" className="btn btn-danger-outline btn-sm" aria-label={`Desactivar ${impuesto.nombre}`} onClick={() => onDesactivar(impuesto)}>
            Desactivar
          </button>
        )}
      </div>
    </td>
  );
}

function Impuestos() {
  const [impuestos, setImpuestos] = useState([]);
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [modo, setModo] = useState("crear");
  const [origen, setOrigen] = useState(null);
  const [form, setForm] = useState(FORMULARIO_VACIO);
  const [cierre, setCierre] = useState(null);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  async function cargar() {
    const { data } = await impuestosApi.listar({ incluirInactivos });
    setImpuestos(data);
  }

  useEffect(() => {
    cargar().catch((err) => setError(mensajeError(err, "No se pudieron cargar los impuestos.")));
  }, [incluirInactivos]);

  async function ejecutar(accion, mensajeFallo) {
    setError("");
    setCargando(true);
    try {
      await accion();
      await cargar();
      return true;
    } catch (err) {
      setError(mensajeError(err, mensajeFallo));
      return false;
    } finally {
      setCargando(false);
    }
  }

  function volverACrear() {
    setModo("crear");
    setOrigen(null);
    setForm(FORMULARIO_VACIO);
  }

  function iniciar(nuevoModo, impuesto) {
    setError("");
    setModo(nuevoModo);
    setOrigen(impuesto);
    setForm(formularioDesdeImpuesto(impuesto, nuevoModo));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    const accion =
      modo === "editar"
        ? () => impuestosApi.actualizar(origen.id, payloadEdicion(form, origen))
        : () => impuestosApi.crear(payloadCreacion(form));
    if (await ejecutar(accion, "No se pudo guardar el impuesto.")) volverACrear();
  }

  async function handleCierre(e) {
    e.preventDefault();
    const { impuesto, fecha } = cierre;
    if (await ejecutar(() => impuestosApi.actualizar(impuesto.id, payloadCierre(impuesto, fecha)), "No se pudo cerrar la vigencia.")) {
      setCierre(null);
    }
  }

  async function handleDesactivar(impuesto) {
    if (!confirm(`¿Desactivar el impuesto ${impuesto.nombre}?`)) return;
    await ejecutar(() => impuestosApi.desactivar(impuesto.id), "No se pudo desactivar el impuesto.");
  }

  return (
    <div>
      <h2>Impuestos</h2>

      <FormularioImpuesto
        form={form}
        onCambio={setForm}
        modo={modo}
        enUso={Boolean(origen?.enUso)}
        cargando={cargando}
        error={error}
        onSubmit={handleSubmit}
        onCancelar={volverACrear}
      />

      {cierre && (
        <form className="catalog-form" onSubmit={handleCierre}>
          <CampoFormulario id="imp-cierre" etiqueta={`Fecha final de vigencia de ${cierre.impuesto.nombre}`}>
            <input id="imp-cierre" type="date" className="input" min={cierre.impuesto.vigenteDesde} value={cierre.fecha} onChange={(e) => setCierre({ ...cierre, fecha: e.target.value })} required />
          </CampoFormulario>
          <button type="submit" className="btn btn-primary" disabled={cargando}>
            Confirmar cierre
          </button>
          <button type="button" className="btn btn-outline" onClick={() => setCierre(null)}>
            Cancelar cierre
          </button>
        </form>
      )}

      <label>
        <input type="checkbox" checked={incluirInactivos} onChange={(e) => setIncluirInactivos(e.target.checked)} />
        <span>Incluir inactivos</span>
      </label>

      <TablaCatalogo encabezados={ENCABEZADOS}>
        {impuestos.length === 0 ? (
          <tr>
            <td colSpan={ENCABEZADOS.length + 1}>No hay impuestos para mostrar.</td>
          </tr>
        ) : (
          impuestos.map((i) => (
            <tr key={i.id}>
              <td>
                <div>{i.nombre}</div>
                {i.nota && <small className="nota-fiscal">{i.nota}</small>}
              </td>
              <td>{etiquetaDe(TIPOS_IMPUESTO, i.tipo)}</td>
              <td className="numeric">{i.tasa}</td>
              <td>{etiquetaDe(AMBITOS_IMPUESTO, i.aplicaA)}</td>
              <td>{i.generaCredito ? "Sí" : "No"}</td>
              <td>{i.articuloLegal}</td>
              <td>{`${i.vigenteDesde} – ${i.vigenteHasta ?? "vigente"}`}</td>
              <td>{i.activo ? "Activo" : "Inactivo"}</td>
              <AccionesImpuesto
                impuesto={i}
                onEditar={(imp) => iniciar("editar", imp)}
                onVersion={(imp) => iniciar("version", imp)}
                onCerrar={(imp) => setCierre({ impuesto: imp, fecha: hoyIso() })}
                onDesactivar={handleDesactivar}
              />
            </tr>
          ))
        )}
      </TablaCatalogo>
    </div>
  );
}

export default Impuestos;
