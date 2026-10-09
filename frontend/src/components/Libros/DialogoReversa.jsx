import { useEffect, useId, useRef, useState } from "react";
import { asientosApi } from "../../services/api";

const MAX_MOTIVO = 250;

function DialogoReversa({ asiento, onCancelar, onReversado }) {
  const [motivo, setMotivo] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");
  const tituloId = useId();
  const dialogoRef = useRef(null);
  const motivoRef = useRef(null);

  useEffect(() => {
    const origen = document.activeElement;
    const dialogo = dialogoRef.current;
    if (typeof dialogo.showModal === "function") dialogo.showModal();
    else dialogo.setAttribute("open", "");
    motivoRef.current.focus();
    return () => origen?.focus?.();
  }, []);

  const confirmar = async (e) => {
    e.preventDefault();
    setError("");
    setEnviando(true);
    try {
      const { data } = await asientosApi.reversar(asiento.id, motivo.trim());
      onReversado(data);
    } catch (err) {
      setError(err.response?.data?.error || "No se pudo reversar el asiento.");
      setEnviando(false);
    }
  };

  const alCancelar = (e) => {
    e.preventDefault();
    if (!enviando) onCancelar();
  };

  return (
    <dialog ref={dialogoRef} className="dialogo" aria-labelledby={tituloId} onCancel={alCancelar}>
      <form className="dialogo-form" onSubmit={confirmar}>
        <h3 id={tituloId}>Reversar asiento</h3>
        <p>
          Se anulará el asiento {asiento.numero} y se generará REV-{asiento.numero} con las líneas invertidas. Esta
          acción no se puede deshacer.
        </p>
        <textarea
          ref={motivoRef}
          className="input"
          aria-label="Motivo"
          maxLength={MAX_MOTIVO}
          rows={3}
          required
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
        />
        {error && <p className="error-chip">{error}</p>}
        <div className="dialogo-acciones">
          <button type="button" className="btn btn-outline" onClick={onCancelar} disabled={enviando}>
            Cancelar
          </button>
          <button type="submit" className="btn btn-primary" disabled={enviando || motivo.trim() === ""}>
            Reversar asiento
          </button>
        </div>
      </form>
    </dialog>
  );
}

export default DialogoReversa;
