import { useCallback, useEffect, useState } from "react";
import { conciliacionesApi } from "../../services/api";
import { indexarPorId } from "../../utils/cuentas";
import { formatoMoneda, mensajeError, monto } from "../../utils/formato";
import { FormularioCatalogo } from "../Catalogo/CatalogoCrud";
import FormularioMovimiento from "./FormularioMovimiento";
import TablaMovimientos from "./TablaMovimientos";

const sinAccion = async () => {};

function ConciliacionDetalle({ id, cuentasBancarias, cuentasContables, onCambio }) {
  const [detalle, setDetalle] = useState(null);
  const [edicion, setEdicion] = useState({ fecha: "", saldoExtracto: "" });
  const [registrando, setRegistrando] = useState(false);
  const [error, setError] = useState("");

  const ejecutar = useCallback(
    async (accion, porDefecto) => {
      setError("");
      try {
        await accion();
        const { data } = await conciliacionesApi.obtener(id);
        setDetalle(data);
        setEdicion({ fecha: data.resumen.fecha, saldoExtracto: String(data.resumen.saldoExtracto) });
      } catch (err) {
        setError(mensajeError(err, porDefecto));
      }
    },
    [id]
  );

  useEffect(() => {
    setRegistrando(false);
    void ejecutar(sinAccion, "No se pudo cargar la conciliación.");
  }, [ejecutar]);

  if (detalle === null) {
    return error ? <p className="error-chip">{error}</p> : <p>Cargando conciliación...</p>;
  }

  const { resumen, marcados, disponibles } = detalle;
  const pendiente = resumen.estado === "Pendiente";
  const cuadrada = Number(resumen.diferencia) === 0;
  const cuentasPorId = indexarPorId(cuentasBancarias);
  const lineasResumen = [
    ["Saldo inicial", resumen.saldoInicial],
    ["Total marcado", resumen.totalMarcado],
    ["Saldo conciliado", resumen.saldoConciliado],
    ["Saldo del extracto", resumen.saldoExtracto],
  ];

  function accionSobreMovimiento(etiqueta, llamar, porDefecto) {
    if (!pendiente) return undefined;
    return { etiqueta, onClick: (movimiento) => void ejecutar(() => llamar(id, movimiento.id), porDefecto) };
  }

  async function finalizar() {
    await conciliacionesApi.finalizar(id);
    onCambio();
  }

  async function cancelar() {
    await conciliacionesApi.cancelar(id);
    onCambio();
  }

  async function guardarCambios() {
    await conciliacionesApi.actualizar(id, { fecha: edicion.fecha, saldoExtracto: monto(edicion.saldoExtracto) });
    onCambio();
  }

  function handleEditar(e) {
    e.preventDefault();
    void ejecutar(guardarCambios, "No se pudo actualizar la conciliación.");
  }

  function handleCancelar() {
    if (!confirm("¿Cancelar esta conciliación? Sus movimientos marcados quedarán disponibles.")) return;
    void ejecutar(cancelar, "No se pudo cancelar la conciliación.");
  }

  return (
    <section aria-label="Detalle de conciliación">
      <h3>Conciliación {resumen.id} ({resumen.estado})</h3>

      {lineasResumen.map(([etiqueta, valor]) => (
        <p key={etiqueta}>{etiqueta}: {formatoMoneda.format(valor)}</p>
      ))}
      <p className="partida-indicator" data-balance={cuadrada ? "ok" : "off"}>Diferencia: {formatoMoneda.format(resumen.diferencia)}</p>

      {error && <p className="error-chip">{error}</p>}

      <div className="lineas-toolbar">
        {pendiente && (
          <button type="button" className="btn btn-outline btn-sm" onClick={() => setRegistrando((actual) => !actual)}>
            {registrando ? "Ocultar formulario de movimiento" : "Registrar movimiento"}
          </button>
        )}
        {pendiente && (
          <button type="button" className="btn btn-danger-outline btn-sm" onClick={handleCancelar}>Cancelar conciliación</button>
        )}
        <button
          type="button"
          className="btn btn-primary btn-sm"
          disabled={!pendiente || !cuadrada}
          onClick={() => void ejecutar(finalizar, "No se pudo finalizar la conciliación.")}
        >
          Finalizar conciliación
        </button>
      </div>

      {pendiente && registrando && (
        <FormularioMovimiento
          cuentasBancarias={cuentasBancarias}
          cuentasContables={cuentasContables}
          inicial={{ cuentaBancariaId: resumen.cuentaBancariaId, fecha: resumen.fecha }}
          onRegistrado={() => ejecutar(sinAccion, "No se pudo cargar la conciliación.")}
        />
      )}

      {pendiente && (
        <FormularioCatalogo onSubmit={handleEditar} etiquetaBoton="Guardar cambios">
          <input type="date" className="input" aria-label="Nueva fecha de corte" value={edicion.fecha} onChange={(e) => setEdicion({ ...edicion, fecha: e.target.value })} required />
          <input type="number" className="input input-money" aria-label="Nuevo saldo del extracto" step="0.01" value={edicion.saldoExtracto} onChange={(e) => setEdicion({ ...edicion, saldoExtracto: e.target.value })} required />
        </FormularioCatalogo>
      )}

      {pendiente && (
        <>
          <h4>Movimientos disponibles</h4>
          <TablaMovimientos
            movimientos={disponibles}
            cuentasPorId={cuentasPorId}
            accion={accionSobreMovimiento("Marcar", conciliacionesApi.marcar, "No se pudo marcar el movimiento.")}
          />
        </>
      )}

      <h4>Movimientos marcados</h4>
      <TablaMovimientos
        movimientos={marcados}
        cuentasPorId={cuentasPorId}
        accion={accionSobreMovimiento("Desmarcar", conciliacionesApi.desmarcar, "No se pudo desmarcar el movimiento.")}
      />
    </section>
  );
}

export default ConciliacionDetalle;
