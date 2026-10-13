import { formatoMoneda } from "../../utils/formato";

const COLUMNAS_BASE = 10;
const COLUMNAS_RETENCION = 2;

function RevisionRetencion({ factura, onResolver }) {
  if (factura.revisionRetencion === "PENDIENTE") {
    return (
      <>
        <span className="badge badge-warning">Por revisar</span>
        <button type="button" className="btn btn-outline btn-sm" aria-label={`Resolver revisión de ${factura.numero}`} onClick={() => onResolver(factura)}>
          Resolver revisión
        </button>
      </>
    );
  }
  if (factura.revisionRetencion === "RESUELTA") return <small className="nota-fiscal">{`Resuelta: ${factura.revisionMotivo}`}</small>;
  return "—";
}

function TablaFacturas({ facturas, etiquetaContraparte, campoContraparteNombre, conRetencion = false, onResolverRevision }) {
  const columnas = COLUMNAS_BASE + (conRetencion ? COLUMNAS_RETENCION : 0);

  return (
    <div className="table-wrap">
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th>Número</th>
              <th>{etiquetaContraparte}</th>
              <th>Fecha</th>
              <th>Vencimiento</th>
              <th className="numeric">Base</th>
              <th className="numeric">IVA</th>
              <th className="numeric">Monto total</th>
              {conRetencion && <th className="numeric">Retención ISR</th>}
              <th className="numeric">Saldo pendiente</th>
              <th>DTE</th>
              <th>Estado</th>
              {conRetencion && <th>Revisión</th>}
            </tr>
          </thead>
          <tbody>
            {facturas.length === 0 ? (
              <tr><td colSpan={columnas}>No hay facturas registradas todavía.</td></tr>
            ) : (
              facturas.map((f) => (
                <tr key={f.id}>
                  <td>
                    {f.numero}
                    {f.calculoLegado && <small className="nota-fiscal">(legado)</small>}
                  </td>
                  <td>{f[campoContraparteNombre]}</td>
                  <td>{f.fecha}</td>
                  <td>{f.fechaVencimiento}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoBase)}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoIva)}</td>
                  <td className="numeric">{formatoMoneda.format(f.montoTotal)}</td>
                  {conRetencion && <td className="numeric">{formatoMoneda.format(f.montoRetencionIsr ?? 0)}</td>}
                  <td className="numeric">{formatoMoneda.format(f.saldoPendiente)}</td>
                  <td>{f.dteSerie ? `${f.dteSerie}-${f.dteNumero}` : "—"}</td>
                  <td>{f.estado}</td>
                  {conRetencion && (
                    <td>
                      <RevisionRetencion factura={f} onResolver={onResolverRevision} />
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TablaFacturas;
