using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class ReporteRetencionService
{
    public const string SentidoEfectuada = "EFECTUADA";
    public const string SentidoRecibida = "RECIBIDA";
    public static readonly string[] SentidosValidos = { SentidoEfectuada, SentidoRecibida };
    public static readonly string[] ConceptosValidos = { Retencion.ConceptoIsr, Retencion.ConceptoIva };
    public static readonly string[] EstadosConstanciaValidos =
    {
        RetencionRules.EstadoPendiente, RetencionRules.EstadoVencida, ConstanciaRetencion.EstadoEmitida, ConstanciaRetencion.EstadoAnulada,
    };

    private readonly DeltaErpDbContext _db;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly TimeProvider _reloj;

    public ReporteRetencionService(DeltaErpDbContext db, ConfiguracionFiscalService configuracion, TimeProvider reloj)
    {
        _db = db;
        _configuracion = configuracion;
        _reloj = reloj;
    }

    public static string? ValidarFiltro(FiltroReporteRetencion filtro)
    {
        if (filtro.Sentido is not null && !SentidosValidos.Contains(filtro.Sentido))
        {
            return $"Sentido inválido. Debe ser uno de: {string.Join(", ", SentidosValidos)}.";
        }
        if (filtro.Concepto is not null && !ConceptosValidos.Contains(filtro.Concepto))
        {
            return $"Concepto inválido. Debe ser uno de: {string.Join(", ", ConceptosValidos)}.";
        }
        return filtro.EstadoConstancia is not null && !EstadosConstanciaValidos.Contains(filtro.EstadoConstancia)
            ? $"Estado de constancia inválido. Debe ser uno de: {string.Join(", ", EstadosConstanciaValidos)}."
            : null;
    }

    public async Task<ReporteRetencionRespuesta> GenerarAsync(FiltroReporteRetencion filtro)
    {
        var inicio = new DateOnly(filtro.Anio, filtro.Mes, 1);
        var consulta = _db.ReporteRetenciones.Where(r => r.PeriodoFiscal == inicio);
        if (filtro.Sentido is not null)
        {
            consulta = consulta.Where(r => r.Sentido == filtro.Sentido);
        }
        if (filtro.Concepto is not null)
        {
            consulta = consulta.Where(r => r.Concepto == filtro.Concepto);
        }
        var delMes = await consulta.OrderBy(r => r.FechaDocumento).ThenBy(r => r.Nit).ThenBy(r => r.DocumentoNumero).ToListAsync();

        var nit = NitRules.Normalizar(filtro.Nit);
        var plazos = await _db.ParametrosRetencion.Where(p => p.Codigo == ParametroRetencion.CodigoPlazoConstanciaIsr && p.Activo).ToListAsync();
        var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);
        var filas = delMes
            .Where(r => nit is null || r.Nit == nit)
            .Select(r => ConstruirFila(r, hoy, plazos))
            .Where(f => filtro.EstadoConstancia is null || f.EstadoConstancia == filtro.EstadoConstancia)
            .ToList();
        return new ReporteRetencionRespuesta(filtro.Anio, filtro.Mes, filas, Totalizar(filas), await ConciliarAsync(inicio, filtro, delMes));
    }

    private static FilaReporteRetencion ConstruirFila(VwRetencionReporte r, DateOnly hoy, List<ParametroRetencion> plazos)
    {
        var diaLimite = plazos
            .FirstOrDefault(p => VigenciaRules.EstaVigente(p.VigenteDesde, p.VigenteHasta, r.FechaDocumento))?.DiaLimite;
        var estado = RetencionRules.EstadoConstancia(r.Concepto, r.FechaDocumento, hoy, diaLimite, r.ConstanciaEstado);
        var mismoMes = r.FechaAsiento.Year == r.FechaDocumento.Year && r.FechaAsiento.Month == r.FechaDocumento.Month;
        return new FilaReporteRetencion(
            r.RetencionId, r.Sentido, r.Concepto, r.ContraparteId, r.Nit, r.Nombre, r.DocumentoTipo, r.DocumentoNumero, r.DteSerie,
            r.DteNumero, r.DteUuid, r.FechaDocumento, r.FechaOperacion, r.Base, r.Tasa, RetencionRules.TasaEfectiva(r.Base, r.MontoRetenido),
            r.MontoRetenido, r.ConstanciaId, r.ConstanciaNumero, r.ConstanciaFecha, r.Formulario, estado, !mismoMes);
    }

    private static List<TotalReporteRetencion> Totalizar(List<FilaReporteRetencion> filas) =>
        filas
            .GroupBy(f => (f.Sentido, f.Concepto))
            .OrderBy(g => g.Key.Sentido)
            .Select(g => new TotalReporteRetencion(
                g.Key.Sentido,
                g.Key.Concepto,
                g.Count(),
                g.Sum(f => f.Base),
                g.Sum(f => f.Monto),
                g.Count(f => f.EstadoConstancia != ConstanciaRetencion.EstadoEmitida)))
            .ToList();

    private async Task<List<ConciliacionReporteRetencion>> ConciliarAsync(
        DateOnly inicio, FiltroReporteRetencion filtro, List<VwRetencionReporte> delMes)
    {
        var configuracion = await _configuracion.ObtenerAsync();
        var objetivos = new (string Concepto, string Sentido, int? CuentaId, bool EsCredito)[]
        {
            (Retencion.ConceptoIsr, SentidoEfectuada, configuracion.CuentaIsrRetenidoPagarId, true),
            (Retencion.ConceptoIva, SentidoRecibida, configuracion.CuentaIvaRetenidoClientesId, false),
        };
        var conciliacion = new List<ConciliacionReporteRetencion>();
        foreach (var (concepto, sentido, cuentaId, esCredito) in objetivos)
        {
            if (cuentaId is not { } cuenta || (filtro.Concepto ?? concepto) != concepto || (filtro.Sentido ?? sentido) != sentido)
            {
                continue;
            }
            var totalReporte = delMes.Where(r => r.Concepto == concepto).Sum(r => r.MontoRetenido);
            var movimiento = await MovimientoDelMesAsync(cuenta, inicio, esCredito);
            var codigo = await _db.CuentasContables.Where(c => c.Id == cuenta).Select(c => c.Codigo).FirstAsync();
            conciliacion.Add(new ConciliacionReporteRetencion(concepto, cuenta, codigo, totalReporte, movimiento, movimiento - totalReporte));
        }
        return conciliacion;
    }

    private async Task<decimal> MovimientoDelMesAsync(int cuentaId, DateOnly inicio, bool esCredito)
    {
        var fin = inicio.AddMonths(1);
        var lineas =
            from l in _db.LineasAsiento
            join a in _db.AsientosContables on l.AsientoId equals a.Id
            where l.CuentaId == cuentaId && a.Estado == AsientoContable.EstadoConfirmado && a.Fecha >= inicio && a.Fecha < fin
            select l;
        return esCredito ? await lineas.SumAsync(l => l.Credito) : await lineas.SumAsync(l => l.Debito);
    }
}
