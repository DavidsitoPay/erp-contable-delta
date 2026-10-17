using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class AjusteRetencionService
{
    private const string Tabla = "ajuste_retencion";

    private sealed record Fila(AjusteRetencion Ajuste, string NotaCredito, string Origen, int OrigenId, string Proveedor, DateOnly FechaOperacion);

    private readonly DeltaErpDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly TimeProvider _tiempo;

    public AjusteRetencionService(DeltaErpDbContext db, AuditoriaService auditoria, TimeProvider tiempo)
    {
        _db = db;
        _auditoria = auditoria;
        _tiempo = tiempo;
    }

    // RN-34. Corre dentro de la transaccion del registro de la factura.
    public async Task RegistrarSiAplicaAsync(IDocumentoFactura documento, int usuarioId)
    {
        if (documento.TipoDocumento != TiposDocumento.NotaCredito || documento.DocumentoOrigenId is not { } origenId)
        {
            return;
        }
        var retenciones = (await _db.Retenciones.AsNoTracking()
                .Where(r => r.DocumentoCxpId == origenId && r.Concepto == Retencion.ConceptoIsrNoResidente)
                .Select(r => new { r.Id, r.FechaOperacion })
                .ToListAsync())
            .Select(r => (r.Id, Periodo: new DateOnly(r.FechaOperacion.Year, r.FechaOperacion.Month, 1)))
            .ToList();
        if (retenciones.Count == 0)
        {
            return;
        }
        var periodos = retenciones.Select(r => r.Periodo).Distinct().ToList();
        var enterados = await _db.Enteramientos.AsNoTracking()
            .Where(e => e.Estado == Enteramiento.EstadoVigente && periodos.Contains(e.Periodo))
            .Select(e => e.Periodo)
            .ToListAsync();
        foreach (var retencion in retenciones.Where(r => enterados.Contains(r.Periodo)))
        {
            _db.AjustesRetencion.Add(new AjusteRetencion
            {
                RetencionId = retencion.Id,
                NotaCreditoId = documento.Id,
                MontoNc = documento.MontoTotal,
                CreadoPor = usuarioId,
                CreadoEn = _tiempo.GetUtcNow(),
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AjusteRetencionDto>> ListarAsync(string? estado)
    {
        var normalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        var filas = await Consulta(normalizado, null).ToListAsync();
        return filas.Select(ADto).ToList();
    }

    public async Task<AjusteRetencionDto?> ObtenerAsync(int id) =>
        (await Consulta(null, id).ToListAsync()).Select(ADto).FirstOrDefault();

    public async Task<(AjusteRetencionDto? Ajuste, ErrorValidacion? Error)> ResolverAsync(int id, SolicitudResolverAjuste req, int usuarioId)
    {
        var errorDatos = EnteramientoRules.ValidarMotivo(req.Motivo) ?? EnteramientoRules.ValidarReferencia(req.Referencia);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var ajuste = await _db.AjustesRetencion.FirstOrDefaultAsync(a => a.Id == id);
        if (ajuste is null)
        {
            return (null, ErrorValidacion.NoEncontrado("El ajuste de retención indicado no existe."));
        }
        if (ajuste.Estado == AjusteRetencion.EstadoResuelto)
        {
            return (null, ErrorValidacion.Conflicto("El ajuste de retención ya está resuelto."));
        }
        var motivo = req.Motivo!.Trim();
        var referencia = req.Referencia!.Trim();
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "resolver_ajuste_retencion", Tabla, async () =>
        {
            ajuste.Estado = AjusteRetencion.EstadoResuelto;
            ajuste.MotivoResolucion = motivo;
            ajuste.ReferenciaResolucion = referencia;
            ajuste.ResueltoPor = usuarioId;
            ajuste.ResueltoEn = _tiempo.GetUtcNow();
            await _db.SaveChangesAsync();
            return $"Ajuste de retención {id} resuelto, referencia {referencia}, motivo: {motivo}";
        });
        return error is null ? (await ObtenerAsync(id), null) : (null, error);
    }

    private IQueryable<Fila> Consulta(string? estado, int? id) =>
        from a in _db.AjustesRetencion.AsNoTracking()
        where (estado == null || a.Estado == estado) && (id == null || a.Id == id)
        join r in _db.Retenciones on a.RetencionId equals r.Id
        join nc in _db.DocumentosCxP on a.NotaCreditoId equals nc.Id
        join o in _db.DocumentosCxP on r.DocumentoCxpId equals (int?)o.Id
        join c in _db.Contrapartes on r.ContraparteId equals c.Id
        orderby a.Id descending
        select new Fila(a, nc.Numero, o.Numero, o.Id, c.Nombre, r.FechaOperacion);

    private static AjusteRetencionDto ADto(Fila f) => new(
        f.Ajuste.Id, f.Ajuste.RetencionId, f.Ajuste.NotaCreditoId, f.OrigenId, f.NotaCredito, f.Origen, f.Proveedor,
        new DateOnly(f.FechaOperacion.Year, f.FechaOperacion.Month, 1), f.Ajuste.MontoNc, f.Ajuste.Estado, f.Ajuste.CreadoEn,
        f.Ajuste.MotivoResolucion, f.Ajuste.ReferenciaResolucion, f.Ajuste.ResueltoEn);
}
