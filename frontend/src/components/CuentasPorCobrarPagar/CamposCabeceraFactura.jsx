import { formatoMoneda } from "../../utils/formato";

const TIPOS_DOCUMENTO = ["Factura", "NotaCredito", "NotaDebito"];

function etiquetaOrigen(documento) {
  const legado = documento.calculoLegado ? " (legado)" : "";
  return `${documento.numero}${legado} — saldo ${formatoMoneda.format(documento.saldoPendiente)}`;
}

function CamposCabeceraFactura({ form, onCambio, config, contrapartes, periodosAbiertos, cuentasControl, origenes, esNotaCredito }) {
  const { campoContraparteId, etiquetaContraparte, etiquetaCuentaControl } = config;
  const cambiar = (campo) => (e) => onCambio({ ...form, [campo]: e.target.value });
  const origenVigente = origenes.some((o) => String(o.id) === form.documentoOrigenId) ? form.documentoOrigenId : "";

  return (
    <div className="catalog-form">
      <input className="input" placeholder="Número" aria-label="Número de factura" value={form.numero} onChange={cambiar("numero")} required />
      <select className="select" aria-label="Tipo de documento" value={form.tipoDocumento} onChange={cambiar("tipoDocumento")}>
        {TIPOS_DOCUMENTO.map((t) => <option key={t} value={t}>{t}</option>)}
      </select>
      <select className="select" aria-label={etiquetaContraparte} value={form[campoContraparteId]} onChange={cambiar(campoContraparteId)} required>
        <option value="">Selecciona {etiquetaContraparte.toLowerCase()}</option>
        {contrapartes.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
      </select>
      {esNotaCredito && (
        <select className="select" aria-label="Documento de origen" value={origenVigente} onChange={cambiar("documentoOrigenId")} required>
          <option value="">Selecciona factura de origen</option>
          {origenes.map((o) => <option key={o.id} value={o.id}>{etiquetaOrigen(o)}</option>)}
        </select>
      )}
      <input type="date" className="input" aria-label="Fecha" value={form.fecha} onChange={cambiar("fecha")} required />
      <input type="date" className="input" aria-label="Fecha de vencimiento" value={form.fechaVencimiento} onChange={cambiar("fechaVencimiento")} required />
      <select className="select" aria-label="Periodo" value={form.periodoId} onChange={cambiar("periodoId")} required>
        <option value="">Selecciona periodo</option>
        {periodosAbiertos.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
      </select>
      <select className="select" aria-label={etiquetaCuentaControl} value={form.cuentaControlId} onChange={cambiar("cuentaControlId")} required>
        <option value="">{etiquetaCuentaControl}</option>
        {cuentasControl.map((c) => <option key={c.id} value={c.id}>{c.codigo} - {c.nombre}</option>)}
      </select>
    </div>
  );
}

export default CamposCabeceraFactura;
