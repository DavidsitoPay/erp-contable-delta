using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class RevaluacionService
{
    private const string Tabla = "revaluacion";
    private const string EfectoGanancia = "Ganancia";
    private const string EfectoPerdida = "Perdida";
    private const string ContextoRevaluacion = "revaluaciones";

    private sealed record Preparacion(
        Moneda Moneda, DateOnly Cierre, DateOnly Reversa, HistorialTipoCambio Tasa, int PeriodoCierreId, int PeriodoReversaId,
        IReadOnlyList<PartidaRevaluacion> Partidas, AsientoRevaluacionLineas Lineas);

    private readonly DeltaErpDbContext _db;
    private readonly TipoCambioService _tipos;
    private readonly SaldoAlCorteService _saldos;
    private readonly TesoreriaService _tesoreria;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly AuditoriaService _auditoria;
    private readonly TimeProvider _tiempo;

    public RevaluacionService(
        DeltaErpDbContext db, TipoCambioService tipos, SaldoAlCorteService saldos, TesoreriaService tesoreria,
        ConfiguracionFiscalService configuracion, AuditoriaService auditoria, TimeProvider tiempo)
    {
        _db = db;
        _tipos = tipos;
        _saldos = saldos;
        _tesoreria = tesoreria;
        _configuracion = configuracion;
        _auditoria = auditoria;
        _tiempo = tiempo;
    }

    public async Task<(RevaluacionVistaPrevia? Vista, ErrorValidacion? Error)> VistaPreviaAsync(SolicitudRevaluacion solicitud)
    {
        var (p, error) = await PrepararAsync(solicitud);
        if (p is null)
        {
            return (null, error);
        }
        return (new RevaluacionVistaPrevia(
            p.Moneda.Codigo, solicitud.Anio, solicitud.Mes, p.Cierre, ResumenTasa(p.Tasa),
            p.Partidas.Where(x => x.Ajuste != 0).Select(Dto).ToList(), p.Lineas.Ganancia, p.Lineas.Perdida, RevaluacionRules.Leyenda), null);
    }

    public async Task<(RevaluacionRespuesta? Revaluacion, ErrorValidacion? Error)> EjecutarAsync(SolicitudRevaluacion solicitud, int usuarioId)
    {
        var (p, errorPrep) = await PrepararAsync(solicitud);
        if (p is null)
        {
            return (null, errorPrep);
        }
        var numero = $"REVAL-{p.Moneda.Codigo}-{solicitud.Anio:0000}{solicitud.Mes:00}";
        Revaluacion? creada = null;
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "ejecutar_revaluacion", Tabla, async () =>
        {
            var cierre = NuevoAsiento(numero, p.Cierre, p.PeriodoCierreId, usuarioId, p.Lineas.Lineas, p.Tasa.Tasa, null);
            var reversa = NuevoAsiento($"{numero}-R", p.Reversa, p.PeriodoReversaId, usuarioId, RevaluacionRules.Invertir(p.Lineas.Lineas), p.Tasa.Tasa, null);
            _db.AsientosContables.AddRange(cierre, reversa);
            await _db.SaveChangesAsync();

            creada = new Revaluacion
            {
                MonedaId = p.Moneda.Id,
                Anio = (short)solicitud.Anio,
                Mes = (short)solicitud.Mes,
                FechaCierre = p.Cierre,
                TipoCambioId = p.Tasa.Id,
                Tasa = p.Tasa.Tasa,
                TotalGanancia = p.Lineas.Ganancia,
                TotalPerdida = p.Lineas.Perdida,
                AsientoId = cierre.Id,
                AsientoReversaId = reversa.Id,
                CreadoPor = usuarioId,
                CreadoEn = _tiempo.GetUtcNow(),
            };
            _db.Revaluaciones.Add(creada);
            await _db.SaveChangesAsync();

            _db.PartidasRevaluacion.AddRange(p.Partidas.Where(x => x.Ajuste != 0).Select(x => NuevaPartida(creada.Id, x)));
            await _db.SaveChangesAsync();
            return $"Revaluación {p.Moneda.Codigo} {solicitud.Mes:00}/{solicitud.Anio} a {CambioRules.FormatoTasa(p.Tasa.Tasa)}: ganancia {p.Lineas.Ganancia}, pérdida {p.Lineas.Perdida}, asientos {cierre.Id} y {reversa.Id}";
        });
        if (error is not null)
        {
            return (null, error);
        }
        return (await ComoRespuestaAsync(creada!), null);
    }

    public async Task<ErrorValidacion?> AnularAsync(int id, string? motivo, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > CambioRules.LongitudMaximaMotivo)
        {
            return ErrorValidacion.Solicitud("El motivo de la anulación es obligatorio y no puede exceder 300 caracteres.");
        }
        var revaluacion = await _db.Revaluaciones.FirstOrDefaultAsync(r => r.Id == id);
        if (revaluacion is null)
        {
            return ErrorValidacion.NoEncontrado("La revaluación indicada no existe.");
        }
        if (revaluacion.Estado == Revaluacion.EstadoAnulada)
        {
            return ErrorValidacion.Conflicto("La revaluación ya está anulada.");
        }
        var cierre = await _db.AsientosContables.Include(a => a.Lineas).FirstAsync(a => a.Id == revaluacion.AsientoId);
        var reversa = await _db.AsientosContables.Include(a => a.Lineas).FirstAsync(a => a.Id == revaluacion.AsientoReversaId);
        var errorPeriodo = await ValidarPeriodoAbiertoAsync(cierre.PeriodoId) ?? await ValidarPeriodoAbiertoAsync(reversa.PeriodoId);
        if (errorPeriodo is not null)
        {
            return errorPeriodo;
        }

        var motivoLimpio = motivo.Trim();
        return await _auditoria.EjecutarComoErrorAsync(usuarioId, "anular_revaluacion", Tabla, async () =>
        {
            var anulaCierre = NuevoAsiento($"ANU-{cierre.Numero}", cierre.Fecha, cierre.PeriodoId, usuarioId, Invertidas(cierre), revaluacion.Tasa, cierre.Id);
            var anulaReversa = NuevoAsiento($"ANU-{reversa.Numero}", reversa.Fecha, reversa.PeriodoId, usuarioId, Invertidas(reversa), revaluacion.Tasa, reversa.Id);
            _db.AsientosContables.AddRange(anulaCierre, anulaReversa);
            await _db.SaveChangesAsync();

            cierre.Estado = AsientoContable.EstadoAnulado;
            reversa.Estado = AsientoContable.EstadoAnulado;
            revaluacion.Estado = Revaluacion.EstadoAnulada;
            revaluacion.MotivoAnulacion = motivoLimpio;
            revaluacion.AnuladaPor = usuarioId;
            revaluacion.AnuladaEn = _tiempo.GetUtcNow();
            revaluacion.AsientoAnulacionId = anulaCierre.Id;
            revaluacion.AsientoAnulacionReversaId = anulaReversa.Id;
            await _db.SaveChangesAsync();
            return $"Revaluación {id} anulada con los asientos {anulaCierre.Id} y {anulaReversa.Id}, motivo: {motivoLimpio}";
        });
    }

    public async Task<(IReadOnlyList<RevaluacionResumenDto>? Filas, ErrorValidacion? Error)> ListarAsync(
        int? anio, int? mes, string? monedaCodigo, string? estado)
    {
        if (mes is < 1 or > 12 || (estado is not null && estado is not (Revaluacion.EstadoVigente or Revaluacion.EstadoAnulada)))
        {
            return (null, ErrorValidacion.Solicitud("Los filtros de mes o estado no son válidos."));
        }
        var codigo = monedaCodigo?.Trim().ToUpperInvariant();
        var filas = await (
            from r in _db.Revaluaciones.AsNoTracking()
            join m in _db.Monedas on r.MonedaId equals m.Id
            where (anio == null || r.Anio == anio) && (mes == null || r.Mes == mes) && (codigo == null || m.Codigo == codigo)
                  && (estado == null || r.Estado == estado)
            orderby r.Id descending
            select new RevaluacionResumenDto(
                r.Id, m.Codigo, r.Anio, r.Mes, r.FechaCierre, r.Tasa, r.TotalGanancia, r.TotalPerdida, r.Estado, r.AsientoId, r.AsientoReversaId)
            ).ToListAsync();
        return (filas, null);
    }

    public async Task<RevaluacionRespuesta?> ObtenerAsync(int id)
    {
        var revaluacion = await _db.Revaluaciones.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return revaluacion is null ? null : await ComoRespuestaAsync(revaluacion);
    }

    private async Task<(Moneda? Moneda, ErrorValidacion? Error)> ValidarSolicitudAsync(SolicitudRevaluacion solicitud)
    {
        var errorMes = RevaluacionRules.ValidarMes(solicitud.Anio, solicitud.Mes, CambioRules.HoyGuatemala(_tiempo.GetUtcNow()));
        if (errorMes is not null)
        {
            return (null, errorMes.EsConflicto ? ErrorValidacion.Conflicto(errorMes.Mensaje) : ErrorValidacion.Solicitud(errorMes.Mensaje));
        }
        var codigo = solicitud.MonedaCodigo?.Trim().ToUpperInvariant();
        var moneda = string.IsNullOrEmpty(codigo) ? null : await _db.Monedas.AsNoTracking().FirstOrDefaultAsync(m => m.Codigo == codigo);
        return moneda is { Activa: true, EsFuncional: false }
            ? (moneda, null)
            : (null, ErrorValidacion.Solicitud("La revaluación aplica a una moneda extranjera activa (USD o EUR)."));
    }

    private async Task<(Preparacion? Preparacion, ErrorValidacion? Error)> PrepararAsync(SolicitudRevaluacion solicitud)
    {
        var (moneda, errorSolicitud) = await ValidarSolicitudAsync(solicitud);
        if (moneda is null)
        {
            return (null, errorSolicitud);
        }
        var cierre = RevaluacionRules.UltimoDiaDelMes(solicitud.Anio, solicitud.Mes);
        var reversa = RevaluacionRules.PrimerDiaDelMesSiguiente(solicitud.Anio, solicitud.Mes);

        var (resultado, errorTasa) = await _tipos.ConsultarAsync(moneda.Codigo, cierre);
        if (resultado is null)
        {
            return (null, errorTasa);
        }
        if (!resultado.Disponible)
        {
            return (null, ErrorValidacion.Conflicto(
                $"No hay tipo de cambio de {moneda.Codigo} para el {VigenciaRules.Iso(cierre)}: Banguat no respondió. " +
                "Un Contador o Administrador puede capturar la tasa manual en Catálogo > Tipos de cambio."));
        }
        if (await _db.Revaluaciones.AnyAsync(r => r.MonedaId == moneda.Id && r.Anio == solicitud.Anio && r.Mes == solicitud.Mes && r.Estado == Revaluacion.EstadoVigente))
        {
            return (null, ErrorValidacion.Conflicto(
                $"Ya existe una revaluación vigente de {moneda.Codigo} para {solicitud.Mes:00}/{solicitud.Anio}; anúlela antes de repetirla."));
        }
        var (periodoCierre, errorCierre) = await PeriodoAbiertoAsync(cierre);
        var (periodoReversa, errorReversa) = await PeriodoAbiertoAsync(reversa);
        var errorPeriodo = errorCierre ?? errorReversa;
        if (errorPeriodo is not null)
        {
            return (null, errorPeriodo);
        }

        var tasa = resultado.Fila!;
        var partidas = await CargarPartidasAsync(moneda, cierre, tasa.Tasa);
        if (partidas.All(x => x.Ajuste == 0))
        {
            return (null, ErrorValidacion.Conflicto($"No hay saldos en {moneda.Codigo} con diferencia al {VigenciaRules.Iso(cierre)}."));
        }
        var (ganancia, perdida) = RevaluacionRules.Totales(partidas);
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaGanancia, errorGanancia) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            ganancia, configuracion.CuentaRevalGananciaId, "ganancia por revaluación (no realizada)", ContextoRevaluacion);
        var (cuentaPerdida, errorPerdida) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            perdida, configuracion.CuentaRevalPerdidaId, "pérdida por revaluación (no realizada)", ContextoRevaluacion);
        if ((errorGanancia ?? errorPerdida) is { } errorCuenta)
        {
            return (null, errorCuenta);
        }
        var lineas = RevaluacionRules.ConstruirLineas(partidas, cuentaGanancia, cuentaPerdida);
        return (new Preparacion(moneda, cierre, reversa, tasa, periodoCierre, periodoReversa, partidas, lineas), null);
    }

    private async Task<(int PeriodoId, ErrorValidacion? Error)> PeriodoAbiertoAsync(DateOnly fecha)
    {
        var periodo = await _tesoreria.BuscarPeriodoPorFechaAsync(fecha);
        if (periodo is null)
        {
            return (0, ErrorValidacion.Conflicto(TesoreriaService.MensajeSinPeriodo(fecha)));
        }
        return (periodo.Id, await ValidarPeriodoAbiertoAsync(periodo.Id));
    }

    private async Task<ErrorValidacion?> ValidarPeriodoAbiertoAsync(int periodoId)
    {
        var periodo = await _db.PeriodosContables.AsNoTracking().FirstAsync(p => p.Id == periodoId);
        return periodo.Estado == PeriodoContable.EstadoAbierto
            ? null
            : ErrorValidacion.Conflicto($"El periodo '{periodo.Nombre}' está en estado '{periodo.Estado}'; la revaluación requiere periodos Abiertos para el cierre y para la reversa.");
    }

    private async Task<List<PartidaRevaluacion>> CargarPartidasAsync(Moneda moneda, DateOnly cierre, decimal tasa)
    {
        var partidas = new List<PartidaRevaluacion>();
        foreach (var d in await _saldos.DocumentosAsync(true, cierre, moneda.Id))
        {
            partidas.Add(RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxC, d.DocumentoId, null, d.CuentaControlId, d.Numero), d.SaldoOriginal, d.SaldoGtq, tasa));
        }
        foreach (var d in await _saldos.DocumentosAsync(false, cierre, moneda.Id))
        {
            partidas.Add(RevaluacionRules.Crear(new OrigenPartida(TipoPartida.CxP, d.DocumentoId, null, d.CuentaControlId, d.Numero), d.SaldoOriginal, d.SaldoGtq, tasa));
        }
        foreach (var b in await _saldos.BancosAsync(cierre, moneda.Id))
        {
            partidas.Add(RevaluacionRules.Crear(new OrigenPartida(TipoPartida.Banco, null, b.CuentaBancariaId, b.CuentaContableId, b.Nombre), b.SaldoOriginal, b.LibrosGtq, tasa));
        }
        return partidas;
    }

    private static AsientoContable NuevoAsiento(
        string numero, DateOnly fecha, int periodoId, int usuarioId, IReadOnlyList<LineaRevaluacion> lineas, decimal tasa, int? reversaDeId) => new()
    {
        Numero = numero,
        Fecha = fecha,
        PeriodoId = periodoId,
        Monto = lineas.Sum(l => l.Debito),
        Estado = AsientoContable.EstadoConfirmado,
        UsuarioId = usuarioId,
        TipoCambioAplicado = tasa,
        ReversaDeId = reversaDeId,
        Lineas = lineas.Select(l => new LineaAsiento { CuentaId = l.CuentaId, Debito = l.Debito, Credito = l.Credito }).ToList(),
    };

    private static IReadOnlyList<LineaRevaluacion> Invertidas(AsientoContable asiento) =>
        RevaluacionRules.Invertir(asiento.Lineas.Select(l => new LineaRevaluacion(l.CuentaId, l.Debito, l.Credito)).ToList());

    private static RevaluacionPartida NuevaPartida(int revaluacionId, PartidaRevaluacion x) => new()
    {
        RevaluacionId = revaluacionId,
        Tipo = x.Tipo.ToString(),
        DocumentoCxcId = x.Tipo == TipoPartida.CxC ? x.DocumentoId : null,
        DocumentoCxpId = x.Tipo == TipoPartida.CxP ? x.DocumentoId : null,
        CuentaBancariaId = x.CuentaBancariaId,
        CuentaId = x.CuentaId,
        SaldoOriginal = x.SaldoOriginal,
        ValorLibrosGtq = x.ValorLibrosGtq,
        ValorCierreGtq = x.ValorCierreGtq,
        AjusteGtq = x.Ajuste,
    };

    private static TipoCambioResumen ResumenTasa(HistorialTipoCambio t) => new(t.Id, t.Tasa, t.Fuente, t.Arrastrado);

    private static PartidaRevaluacionDto Dto(PartidaRevaluacion x) => new(
        x.Tipo.ToString(), x.Referencia, x.CuentaId, x.SaldoOriginal, x.ValorLibrosGtq, x.ValorCierreGtq, x.Ajuste,
        x.EsGanancia ? EfectoGanancia : EfectoPerdida);

    private async Task<RevaluacionRespuesta> ComoRespuestaAsync(Revaluacion r)
    {
        var moneda = await _db.Monedas.AsNoTracking().FirstAsync(m => m.Id == r.MonedaId);
        var tasa = await _db.TiposCambio.AsNoTracking().FirstAsync(t => t.Id == r.TipoCambioId);
        var partidas = await _db.PartidasRevaluacion.AsNoTracking().Where(p => p.RevaluacionId == r.Id).OrderBy(p => p.Id).ToListAsync();
        var idsCxc = partidas.Select(p => p.DocumentoCxcId).OfType<int>().ToList();
        var idsCxp = partidas.Select(p => p.DocumentoCxpId).OfType<int>().ToList();
        var idsBanco = partidas.Select(p => p.CuentaBancariaId).OfType<int>().ToList();
        var numerosCxc = await _db.DocumentosCxC.Where(d => idsCxc.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Numero);
        var numerosCxp = await _db.DocumentosCxP.Where(d => idsCxp.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Numero);
        var bancos = await _db.CuentasBancarias.Where(c => idsBanco.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => $"{c.Banco} {c.Numero}");

        string Referencia(RevaluacionPartida p) => p.Tipo switch
        {
            RevaluacionPartida.TipoCxC => numerosCxc.GetValueOrDefault(p.DocumentoCxcId ?? 0, string.Empty),
            RevaluacionPartida.TipoCxP => numerosCxp.GetValueOrDefault(p.DocumentoCxpId ?? 0, string.Empty),
            _ => bancos.GetValueOrDefault(p.CuentaBancariaId ?? 0, string.Empty),
        };
        bool EsGanancia(RevaluacionPartida p) => p.Tipo == RevaluacionPartida.TipoCxP ? p.AjusteGtq < 0 : p.AjusteGtq > 0;

        var dtos = partidas.Select(p => new PartidaRevaluacionDto(
            p.Tipo, Referencia(p), p.CuentaId, p.SaldoOriginal, p.ValorLibrosGtq, p.ValorCierreGtq, p.AjusteGtq,
            EsGanancia(p) ? EfectoGanancia : EfectoPerdida)).ToList();
        return new RevaluacionRespuesta(
            r.Id, moneda.Codigo, r.Anio, r.Mes, r.FechaCierre, ResumenTasa(tasa), dtos, r.TotalGanancia, r.TotalPerdida,
            r.AsientoId, r.AsientoReversaId, r.Estado, r.MotivoAnulacion, RevaluacionRules.Leyenda);
    }
}
