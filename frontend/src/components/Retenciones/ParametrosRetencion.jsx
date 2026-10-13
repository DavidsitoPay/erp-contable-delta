import { useEffect, useState } from "react";
import { parametrosRetencionApi } from "../../services/api";
import { CONCEPTOS_PARAMETRO, etiquetaDe } from "../../utils/etiquetasFiscales";
import { hoyIso, mensajeError } from "../../utils/formato";
import {
  FORMULARIO_PARAMETRO_VACIO,
  descripcionTasa,
  descripcionUmbral,
  formularioDesdeParametro,
  payloadActualizacion,
  payloadCreacion,
} from "../../utils/parametrosRetencion";
import { perfilActual, puedeAdministrarConfiguracionFiscal } from "../../utils/perfiles";
import { TablaCatalogo } from "../Catalogo/CatalogoCrud";
import CasillaInactivos from "../Catalogo/CasillaInactivos";
import CierreVigencia from "../Catalogo/CierreVigencia";
import { useAccionCatalogo } from "../Catalogo/useAccionCatalogo";
import FormularioParametroRetencion from "./FormularioParametroRetencion";

const ENCABEZADOS = ["Parámetro", "Concepto", "Tasa", "Umbral mínimo", "Referencia legal", "Vigencia", "Estado"];
const AVISO_REFERENCIAL = "Parámetros referenciales: validar con el asesor fiscal antes de aplicarlos.";
const AVISO_SOLO_LECTURA = "Solo el Administrador del sistema puede modificar los parámetros.";

function AccionesParametro({ parametro, onVersion, onCerrar, onDesactivar }) {
  return (
    <td>
      <div className="acciones-fila">
        <button type="button" className="btn btn-outline btn-sm" aria-label={`Nueva versión de ${parametro.nombre}`} onClick={() => onVersion(parametro)}>
          Nueva versión
        </button>
        {parametro.activo && !parametro.vigenteHasta && (
          <button type="button" className="btn btn-outline btn-sm" aria-label={`Cerrar vigencia de ${parametro.nombre}`} onClick={() => onCerrar(parametro)}>
            Cerrar vigencia
          </button>
        )}
        {parametro.activo && (
          <button type="button" className="btn btn-danger-outline btn-sm" aria-label={`Desactivar ${parametro.nombre}`} onClick={() => onDesactivar(parametro)}>
            Desactivar
          </button>
        )}
      </div>
    </td>
  );
}

function ParametrosRetencion() {
  const [puedeEditar] = useState(() => puedeAdministrarConfiguracionFiscal(perfilActual()));
  const [parametros, setParametros] = useState([]);
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [form, setForm] = useState(FORMULARIO_PARAMETRO_VACIO);
  const [cierre, setCierre] = useState(null);

  async function cargar() {
    const { data } = await parametrosRetencionApi.listar({ incluirInactivos });
    setParametros(data);
  }

  const { error, setError, cargando, ejecutar } = useAccionCatalogo(cargar);

  useEffect(() => {
    cargar().catch((err) => setError(mensajeError(err, "No se pudieron cargar los parámetros de retención.")));
  }, [incluirInactivos]);

  async function handleSubmit(e) {
    e.preventDefault();
    if (await ejecutar(() => parametrosRetencionApi.crear(payloadCreacion(form)), "No se pudo guardar el parámetro.")) {
      setForm(FORMULARIO_PARAMETRO_VACIO);
    }
  }

  async function handleCierre(e) {
    e.preventDefault();
    const { item: parametro, fecha } = cierre;
    const cambios = payloadActualizacion(parametro, { vigenteHasta: fecha });
    if (await ejecutar(() => parametrosRetencionApi.actualizar(parametro.id, cambios), "No se pudo cerrar la vigencia.")) {
      setCierre(null);
    }
  }

  async function handleDesactivar(parametro) {
    if (!confirm(`¿Desactivar el parámetro ${parametro.nombre}?`)) return;
    const cambios = payloadActualizacion(parametro, { activo: false });
    await ejecutar(() => parametrosRetencionApi.actualizar(parametro.id, cambios), "No se pudo desactivar el parámetro.");
  }

  return (
    <div>
      <h2>Parámetros de retención</h2>
      <p className="warning-chip">{AVISO_REFERENCIAL}</p>

      {puedeEditar ? (
        <FormularioParametroRetencion form={form} onCambio={setForm} cargando={cargando} onSubmit={handleSubmit} onLimpiar={() => setForm(FORMULARIO_PARAMETRO_VACIO)} />
      ) : (
        <p>{AVISO_SOLO_LECTURA}</p>
      )}

      {cierre && (
        <CierreVigencia
          id="par-cierre"
          cierre={cierre}
          cargando={cargando}
          onCambio={setCierre}
          onConfirmar={handleCierre}
        />
      )}
      {error && <p className="error-chip">{error}</p>}

      <CasillaInactivos marcada={incluirInactivos} onCambio={setIncluirInactivos} />

      <TablaCatalogo encabezados={ENCABEZADOS}>
        {parametros.length === 0 ? (
          <tr>
            <td colSpan={ENCABEZADOS.length + 1}>No hay parámetros de retención para mostrar.</td>
          </tr>
        ) : (
          parametros.map((p) => (
            <tr key={p.id}>
              <td>
                <div>{p.nombre}</div>
                <small className="nota-fiscal">{p.codigo}</small>
                {p.nota && <small className="nota-fiscal">{p.nota}</small>}
              </td>
              <td>{etiquetaDe(CONCEPTOS_PARAMETRO, p.concepto)}</td>
              <td>{descripcionTasa(p)}</td>
              <td>{descripcionUmbral(p)}</td>
              <td>{p.articuloLegal}</td>
              <td>{`${p.vigenteDesde} – ${p.vigenteHasta ?? "vigente"}`}</td>
              <td>
                {p.activo ? "Activo" : "Inactivo"}
                {p.enUso && <small className="nota-fiscal">En uso</small>}
              </td>
              {puedeEditar ? (
                <AccionesParametro
                  parametro={p}
                  onVersion={(parametro) => setForm(formularioDesdeParametro(parametro))}
                  onCerrar={(parametro) => setCierre({ item: parametro, fecha: hoyIso() })}
                  onDesactivar={handleDesactivar}
                />
              ) : (
                <td />
              )}
            </tr>
          ))
        )}
      </TablaCatalogo>
    </div>
  );
}

export default ParametrosRetencion;
