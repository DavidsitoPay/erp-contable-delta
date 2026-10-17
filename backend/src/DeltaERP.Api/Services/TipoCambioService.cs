using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeltaERP.Api.Services;

public sealed record PropuestaTipoCambio(DateOnly Fecha, decimal Tasa, string Fuente);

public sealed record ResultadoTipoCambio(bool Disponible, HistorialTipoCambio? Fila, PropuestaTipoCambio? Propuesta, string? Mensaje);

public sealed record CapturaTasaManual(string MonedaCodigo, DateOnly Fecha, decimal Tasa, string? Motivo);

public class TipoCambioService
{
    public const string MensajeFechaFutura = "No hay tipo de cambio para fechas futuras.";
    public const string MensajeBanguatCaido = "Banguat no respondió; capture la tasa manual o reintente.";
    private const string MensajeMonedaInvalida = "La moneda indicada no admite tipo de cambio.";
    private const string MensajeOficialExistente = "Ya existe el tipo de cambio oficial de Banguat para esa fecha.";
    private const string RestriccionActiva = "ux_historialtipocambio_activa";
    private const string TablaTiposCambio = "historialtipocambio";
    private const int CodigoBanguatUsd = 2;

    private readonly DeltaErpDbContext _db;
    private readonly IBanguatClient _banguat;
    private readonly AuditoriaService _auditoria;
    private readonly TimeProvider _tiempo;
    private readonly BanguatOptions _opciones;

    public TipoCambioService(
        DeltaErpDbContext db, IBanguatClient banguat, AuditoriaService auditoria, TimeProvider tiempo, IOptions<BanguatOptions> opciones)
    {
        _db = db;
        _banguat = banguat;
        _auditoria = auditoria;
        _tiempo = tiempo;
        _opciones = opciones.Value;
    }

    public static string MensajeUsada(int id) =>
        $"La tasa de cambio {id} ya fue usada en documentos, cobros, pagos, movimientos o revaluaciones; no puede darse de baja.";

    public async Task<(ResultadoTipoCambio? Resultado, ErrorValidacion? Error)> ConsultarAsync(string? monedaCodigo, DateOnly fecha)
    {
        var (moneda, errorMoneda) = await BuscarMonedaExtranjeraAsync(monedaCodigo);
        if (moneda is null)
        {
            return (null, errorMoneda);
        }
        var hoy = Hoy();
        if (CambioRules.EsFechaFutura(fecha, hoy))
        {
            return (null, ErrorValidacion.Conflicto(MensajeFechaFutura));
        }
        var existente = await FilaActivaAsync(moneda.Id, fecha);
        if (existente is not null && (!existente.Arrastrado || fecha < hoy || existente.Fuente == HistorialTipoCambio.FuenteManual))
        {
            return (Disponible(existente), null);
        }
        try
        {
            return await ResolverConsultaAsync(moneda, fecha, existente, await CalcularDeBanguatAsync(moneda, fecha));
        }
        catch (BanguatNoDisponibleException)
        {
            return (existente is null ? await NoDisponibleAsync(moneda.Id, fecha, MensajeBanguatCaido) : Disponible(existente), null);
        }
    }

    public async Task<ConsultaTipoCambioRespuesta> ComoRespuestaAsync(string monedaCodigo, DateOnly fecha, ResultadoTipoCambio resultado)
    {
        var codigo = monedaCodigo.Trim().ToUpperInvariant();
        var dto = resultado.Fila is null ? null : await ObtenerDtoAsync(resultado.Fila.Id);
        return new ConsultaTipoCambioRespuesta(codigo, fecha, resultado.Disponible, dto, resultado.Propuesta, resultado.Mensaje);
    }

    public async Task<TipoCambioDto?> ObtenerDtoAsync(int id)
    {
        var filas = await ConsultarHistorialAsync(t => t.Id == id, true);
        return filas.Count > 0 ? filas[0] : null;
    }

    public async Task<(IReadOnlyList<TipoCambioDto>? Filas, ErrorValidacion? Error)> HistorialAsync(
        string? moneda, DateOnly? desde, DateOnly? hasta, bool incluirInactivas)
    {
        if (desde > hasta)
        {
            return (null, ErrorValidacion.Solicitud("La fecha inicial no puede ser posterior a la final."));
        }
        var codigo = moneda?.Trim().ToUpperInvariant();
        var filas = await ConsultarHistorialAsync(
            t => (desde == null || t.Fecha >= desde) && (hasta == null || t.Fecha <= hasta)
                 && (codigo == null || _db.Monedas.Any(m => m.Id == t.MonedaId && m.Codigo == codigo)),
            incluirInactivas);
        return (filas, null);
    }

    public async Task<(TipoCambioAplicable? Cambio, ErrorValidacion? Error)> ResolverParaDocumentoAsync(string? monedaCodigo, DateOnly fecha)
    {
        var funcional = await MonedaFuncionalAsync();
        var codigo = monedaCodigo?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo) || codigo == funcional.Codigo)
        {
            return (CambioFuncional(funcional, fecha), null);
        }
        var moneda = await _db.Monedas.AsNoTracking().FirstOrDefaultAsync(m => m.Codigo == codigo);
        if (moneda is null || !moneda.Activa)
        {
            return (null, ErrorValidacion.Solicitud($"La moneda {codigo} no existe o está inactiva."));
        }
        var (resultado, error) = await ConsultarAsync(codigo, fecha);
        if (resultado is null)
        {
            return (null, error);
        }
        if (!resultado.Disponible)
        {
            return (null, ErrorValidacion.Conflicto(
                $"No hay tipo de cambio de {codigo} para el {VigenciaRules.Iso(fecha)}: Banguat no respondió. " +
                "Un Contador o Administrador puede capturar la tasa manual en Catálogo > Tipos de cambio."));
        }
        return (Aplicable(resultado.Fila!, moneda, fecha), null);
    }

    public async Task<(TipoCambioAplicable? Cambio, ErrorValidacion? Error)> ResolverPorIdAsync(int tipoCambioId)
    {
        var fila = await _db.TiposCambio.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tipoCambioId);
        if (fila is null)
        {
            return (null, ErrorValidacion.Conflicto("El tipo de cambio de la factura de origen no existe."));
        }
        var moneda = await _db.Monedas.AsNoTracking().FirstAsync(m => m.Id == fila.MonedaId);
        return (Aplicable(fila, moneda, fila.Fecha), null);
    }

    public async Task<TipoCambioAplicable> CambioFuncionalAsync(DateOnly fecha) =>
        CambioFuncional(await MonedaFuncionalAsync(), fecha);

    public async Task<(HistorialTipoCambio? Fila, ErrorValidacion? Error)> CapturarManualAsync(CapturaTasaManual datos, int usuarioId)
    {
        var errorDatos = CambioRules.ValidarTasaManual(datos.Tasa, datos.Motivo);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var (moneda, errorMoneda) = await BuscarMonedaExtranjeraAsync(datos.MonedaCodigo);
        if (moneda is null)
        {
            return (null, errorMoneda);
        }
        if (CambioRules.EsFechaFutura(datos.Fecha, Hoy()))
        {
            return (null, ErrorValidacion.Conflicto(MensajeFechaFutura));
        }
        var existente = await FilaActivaAsync(moneda.Id, datos.Fecha);
        var errorExistente = await ValidarReemplazableAsync(existente);
        if (errorExistente is not null)
        {
            return (null, errorExistente);
        }

        var nueva = new HistorialTipoCambio
        {
            MonedaId = moneda.Id,
            Fecha = datos.Fecha,
            Tasa = datos.Tasa,
            Fuente = HistorialTipoCambio.FuenteManual,
            FechaReferencia = datos.Fecha,
            Motivo = datos.Motivo!.Trim(),
            UsuarioId = usuarioId,
            CreadoEn = _tiempo.GetUtcNow(),
        };
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "capturar_tipo_cambio_manual", TablaTiposCambio, async () =>
        {
            await GuardarReemplazoAsync(existente, nueva);
            var reemplazo = existente is null ? string.Empty : $"; reemplaza la tasa {existente.Id} ({existente.Fuente})";
            return $"{moneda.Codigo} {VigenciaRules.Iso(datos.Fecha)} tasa {CambioRules.FormatoTasa(datos.Tasa)}, motivo: {nueva.Motivo}{reemplazo}";
        });
        if (error is not null)
        {
            return (null, error);
        }
        return (nueva, null);
    }

    public async Task<(HistorialTipoCambio? Fila, ErrorValidacion? Error)> SustituirPorBanguatAsync(string monedaCodigo, DateOnly fecha, int usuarioId)
    {
        var (moneda, errorMoneda) = await BuscarMonedaExtranjeraAsync(monedaCodigo);
        if (moneda is null)
        {
            return (null, errorMoneda);
        }
        var manual = await FilaActivaAsync(moneda.Id, fecha);
        if (manual is not { Fuente: HistorialTipoCambio.FuenteManual })
        {
            return (null, ErrorValidacion.Conflicto("No hay una tasa manual sin uso para esa fecha."));
        }
        if (await EstaUsadaAsync(manual.Id))
        {
            return (null, ErrorValidacion.Conflicto(MensajeUsada(manual.Id)));
        }
        var (calculada, errorBanguat) = await ConsultarBanguatSeguroAsync(moneda, fecha);
        if (calculada is null)
        {
            return (null, errorBanguat);
        }

        var nueva = NuevaFilaBanguat(moneda.Id, fecha, calculada);
        nueva.UsuarioId = usuarioId;
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "sustituir_tipo_cambio_banguat", TablaTiposCambio, async () =>
        {
            await GuardarReemplazoAsync(manual, nueva);
            return $"{moneda.Codigo} {VigenciaRules.Iso(fecha)}: la tasa manual {manual.Id} se sustituye por la de Banguat {CambioRules.FormatoTasa(nueva.Tasa)}";
        });
        if (error is not null)
        {
            return (null, error);
        }
        return (nueva, null);
    }

    public async Task<ErrorValidacion?> DarDeBajaAsync(int id, string? motivo, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > CambioRules.LongitudMaximaMotivo)
        {
            return ErrorValidacion.Solicitud("El motivo de la baja es obligatorio y no puede exceder 300 caracteres.");
        }
        var fila = await _db.TiposCambio.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (fila is null)
        {
            return ErrorValidacion.NoEncontrado("La tasa de cambio indicada no existe.");
        }
        if (!fila.Activa)
        {
            return ErrorValidacion.Conflicto($"La tasa de cambio {id} ya está dada de baja.");
        }
        if (await EstaUsadaAsync(id))
        {
            return ErrorValidacion.Conflicto(MensajeUsada(id));
        }
        var motivoLimpio = motivo.Trim();
        return await _auditoria.EjecutarComoErrorAsync(usuarioId, "baja_tipo_cambio", TablaTiposCambio, async () =>
        {
            await DarDeBajaFilaAsync(id);
            return $"Tasa de cambio {id} dada de baja, motivo: {motivoLimpio}";
        });
    }

    public async Task<bool> EstaUsadaAsync(int id) => (await UsadasAsync([id])).Contains(id);

    private DateOnly Hoy() => CambioRules.HoyGuatemala(_tiempo.GetUtcNow());

    private async Task<(ResultadoTipoCambio? Resultado, ErrorValidacion? Error)> ResolverConsultaAsync(
        Moneda moneda, DateOnly fecha, HistorialTipoCambio? existente, TasaCalculada? calculada)
    {
        if (existente is not null)
        {
            var mejora = calculada is { Arrastrado: false } && !await EstaUsadaAsync(existente.Id);
            return (Disponible(mejora ? await ReemplazarAsync(existente, NuevaFilaBanguat(moneda.Id, fecha, calculada!)) : existente), null);
        }
        if (calculada is not null)
        {
            return (Disponible(await InsertarOReleerAsync(NuevaFilaBanguat(moneda.Id, fecha, calculada))), null);
        }
        var previa = await UltimaFilaAsync(moneda.Id, fecha);
        if (previa is null)
        {
            return (null, ErrorValidacion.Conflicto(
                $"Banguat no tiene un tipo de cambio de {moneda.Codigo} anterior al {VigenciaRules.Iso(fecha)}; capture la tasa manual."));
        }
        return (NoDisponible(previa, $"Banguat no publicó un tipo de cambio de {moneda.Codigo} para el {VigenciaRules.Iso(fecha)}; capture la tasa manual."), null);
    }

    private async Task<(TasaCalculada? Calculada, ErrorValidacion? Error)> ConsultarBanguatSeguroAsync(Moneda moneda, DateOnly fecha)
    {
        try
        {
            var calculada = await CalcularDeBanguatAsync(moneda, fecha);
            if (calculada is null)
            {
                return (null, ErrorValidacion.Conflicto($"Banguat no tiene un tipo de cambio de {moneda.Codigo} para el {VigenciaRules.Iso(fecha)}."));
            }
            return (calculada, null);
        }
        catch (BanguatNoDisponibleException)
        {
            return (null, ErrorValidacion.Conflicto("Banguat no respondió; intente más tarde."));
        }
    }

    private async Task<TasaCalculada?> CalcularDeBanguatAsync(Moneda moneda, DateOnly fecha)
    {
        var desde = fecha.AddDays(-_opciones.VentanaDias);
        var usd = _banguat.ObtenerRangoAsync(CodigoBanguatUsd, desde, fecha);
        if (moneda.CodigoBanguat == CodigoBanguatUsd)
        {
            return CambioRules.CalcularUsd(fecha, await usd);
        }
        var propia = _banguat.ObtenerRangoAsync(moneda.CodigoBanguat!.Value, desde, fecha);
        await Task.WhenAll(usd, propia);
        var referencia = CambioRules.CalcularUsd(fecha, await usd);
        return referencia is null ? null : CambioRules.CalcularEur(fecha, await propia, referencia);
    }

    private HistorialTipoCambio NuevaFilaBanguat(int monedaId, DateOnly fecha, TasaCalculada calculada) => new()
    {
        MonedaId = monedaId,
        Fecha = fecha,
        Tasa = calculada.Tasa,
        Fuente = HistorialTipoCambio.FuenteBanguat,
        FechaReferencia = calculada.FechaReferencia,
        Arrastrado = calculada.Arrastrado,
        CotizacionVenta = calculada.CotizacionVenta,
        CotizacionCompra = calculada.CotizacionCompra,
        ReferenciaUsd = calculada.ReferenciaUsd,
        CreadoEn = _tiempo.GetUtcNow(),
    };

    private async Task<HistorialTipoCambio> InsertarOReleerAsync(HistorialTipoCambio fila)
    {
        _db.TiposCambio.Add(fila);
        try
        {
            await _db.SaveChangesAsync();
            return fila;
        }
        catch (DbUpdateException ex) when (ErroresPostgres.EsViolacionUnica(ex, RestriccionActiva))
        {
            _db.Entry(fila).State = EntityState.Detached;
            return await FilaActivaAsync(fila.MonedaId, fila.Fecha)
                ?? throw new InvalidOperationException("No se pudo leer el tipo de cambio existente.");
        }
    }

    private async Task<HistorialTipoCambio> ReemplazarAsync(HistorialTipoCambio existente, HistorialTipoCambio nueva)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync();
        await GuardarReemplazoAsync(existente, nueva);
        await transaccion.CommitAsync();
        return nueva;
    }

    // Baja primero: el índice único parcial solo admite una fila activa por moneda y fecha.
    private async Task GuardarReemplazoAsync(HistorialTipoCambio? existente, HistorialTipoCambio nueva)
    {
        if (existente is not null)
        {
            await DarDeBajaFilaAsync(existente.Id);
        }
        _db.TiposCambio.Add(nueva);
        await _db.SaveChangesAsync();
    }

    private Task<int> DarDeBajaFilaAsync(int id) =>
        _db.TiposCambio.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activa, false));

    private async Task<ErrorValidacion?> ValidarReemplazableAsync(HistorialTipoCambio? existente)
    {
        if (existente is null)
        {
            return null;
        }
        if (existente.Fuente == HistorialTipoCambio.FuenteBanguat && !existente.Arrastrado)
        {
            return ErrorValidacion.Conflicto(MensajeOficialExistente);
        }
        return await EstaUsadaAsync(existente.Id) ? ErrorValidacion.Conflicto(MensajeUsada(existente.Id)) : null;
    }

    private Task<HistorialTipoCambio?> FilaActivaAsync(int monedaId, DateOnly fecha) =>
        _db.TiposCambio.AsNoTracking().FirstOrDefaultAsync(t => t.MonedaId == monedaId && t.Fecha == fecha && t.Activa);

    private Task<HistorialTipoCambio?> UltimaFilaAsync(int monedaId, DateOnly fecha) =>
        _db.TiposCambio.AsNoTracking()
            .Where(t => t.MonedaId == monedaId && t.Activa && t.Fecha <= fecha)
            .OrderByDescending(t => t.Fecha)
            .FirstOrDefaultAsync();

    private async Task<ResultadoTipoCambio> NoDisponibleAsync(int monedaId, DateOnly fecha, string mensaje) =>
        NoDisponible(await UltimaFilaAsync(monedaId, fecha), mensaje);

    private static ResultadoTipoCambio NoDisponible(HistorialTipoCambio? previa, string mensaje) =>
        new(false, null, previa is null ? null : new PropuestaTipoCambio(previa.Fecha, previa.Tasa, previa.Fuente), mensaje);

    private static ResultadoTipoCambio Disponible(HistorialTipoCambio fila) => new(true, fila, null, null);

    private async Task<(Moneda? Moneda, ErrorValidacion? Error)> BuscarMonedaExtranjeraAsync(string? codigo)
    {
        var normalizado = codigo?.Trim().ToUpperInvariant();
        var moneda = string.IsNullOrEmpty(normalizado)
            ? null
            : await _db.Monedas.AsNoTracking().FirstOrDefaultAsync(m => m.Codigo == normalizado);
        if (moneda is { Activa: true, EsFuncional: false, CodigoBanguat: not null })
        {
            return (moneda, null);
        }
        return (null, ErrorValidacion.Solicitud(MensajeMonedaInvalida));
    }

    private Task<Moneda> MonedaFuncionalAsync() => _db.Monedas.AsNoTracking().FirstAsync(m => m.EsFuncional);

    private static TipoCambioAplicable CambioFuncional(Moneda funcional, DateOnly fecha) =>
        new(funcional.Id, funcional.Codigo, null, 1m, null, false, fecha);

    private static TipoCambioAplicable Aplicable(HistorialTipoCambio fila, Moneda moneda, DateOnly fecha) =>
        new(moneda.Id, moneda.Codigo, fila.Id, fila.Tasa, fila.Fuente, fila.Arrastrado, fecha);

    private async Task<IReadOnlyList<TipoCambioDto>> ConsultarHistorialAsync(
        System.Linq.Expressions.Expression<Func<HistorialTipoCambio, bool>> filtro, bool incluirInactivas)
    {
        var consulta =
            from t in _db.TiposCambio.AsNoTracking().Where(filtro)
            join m in _db.Monedas on t.MonedaId equals m.Id
            join u in _db.Usuarios on t.UsuarioId equals (int?)u.Id into usuarios
            from u in usuarios.DefaultIfEmpty()
            where incluirInactivas || t.Activa
            orderby t.Fecha descending, t.Id descending
            select new { Fila = t, Moneda = m.Codigo, Usuario = u == null ? null : u.Nombre };
        var filas = await consulta.ToListAsync();
        var usadas = await UsadasAsync(filas.Select(f => f.Fila.Id).ToList());
        return filas.Select(f => ComoDto(f.Fila, f.Moneda, f.Usuario, usadas.Contains(f.Fila.Id))).ToList();
    }

    private static TipoCambioDto ComoDto(HistorialTipoCambio t, string moneda, string? usuario, bool usada) => new(
        t.Id, moneda, t.Fecha, t.Tasa, t.Fuente, t.Arrastrado, t.FechaReferencia, t.CotizacionVenta, t.CotizacionCompra,
        t.ReferenciaUsd, t.Motivo, usuario, usada, t.Activa);

    private async Task<HashSet<int>> UsadasAsync(IReadOnlyCollection<int> ids)
    {
        var usadas = new HashSet<int>();
        usadas.UnionWith(await _db.DocumentosCxC.Where(d => d.TipoCambioId != null && ids.Contains(d.TipoCambioId.Value)).Select(d => d.TipoCambioId!.Value).ToListAsync());
        usadas.UnionWith(await _db.DocumentosCxP.Where(d => d.TipoCambioId != null && ids.Contains(d.TipoCambioId.Value)).Select(d => d.TipoCambioId!.Value).ToListAsync());
        usadas.UnionWith(await _db.AplicacionesPagoCliente.Where(a => a.TipoCambioPagoId != null && ids.Contains(a.TipoCambioPagoId.Value)).Select(a => a.TipoCambioPagoId!.Value).ToListAsync());
        usadas.UnionWith(await _db.AplicacionesPagoProveedor.Where(a => a.TipoCambioPagoId != null && ids.Contains(a.TipoCambioPagoId.Value)).Select(a => a.TipoCambioPagoId!.Value).ToListAsync());
        usadas.UnionWith(await _db.MovimientosTesoreria.Where(m => m.TipoCambioId != null && ids.Contains(m.TipoCambioId.Value)).Select(m => m.TipoCambioId!.Value).ToListAsync());
        usadas.UnionWith(await _db.Revaluaciones.Where(r => ids.Contains(r.TipoCambioId)).Select(r => r.TipoCambioId).ToListAsync());
        return usadas;
    }
}
