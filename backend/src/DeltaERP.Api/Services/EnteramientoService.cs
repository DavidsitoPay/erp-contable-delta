using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class EnteramientoService
{
    private const string Tabla = "enteramiento";
    private const string AvisoMulta =
        "Pago fuera de plazo: puede aplicar multa (Código Tributario, art. 91: 100 % del impuesto no enterado, 50 % si se paga antes del requerimiento). Informativo; el sistema no la calcula.";

    private readonly DeltaErpDbContext _db;
    private readonly TesoreriaService _tesoreria;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly AuditoriaService _auditoria;
    private readonly TimeProvider _tiempo;

    public EnteramientoService(
        DeltaErpDbContext db, TesoreriaService tesoreria, ConfiguracionFiscalService configuracion, AuditoriaService auditoria, TimeProvider tiempo)
    {
        _db = db;
        _tesoreria = tesoreria;
        _configuracion = configuracion;
        _auditoria = auditoria;
        _tiempo = tiempo;
    }

    private DateOnly Hoy() => CambioRules.HoyGuatemala(_tiempo.GetUtcNow());

    private Task<decimal> SaldoPeriodoAsync(DateOnly periodo) =>
        _db.SaldosEnterar.AsNoTracking().Where(s => s.Periodo == periodo).Select(s => s.Saldo).FirstOrDefaultAsync();

    public async Task<(IReadOnlyList<SaldoEnterarDto>? Filas, ErrorValidacion? Error)> SaldoAsync(DateOnly? periodo)
    {
        if (periodo is { } p && EnteramientoRules.ValidarPeriodo(p) is { } errorPeriodo)
        {
            return (null, ErrorValidacion.Solicitud(errorPeriodo));
        }
        var consulta = _db.SaldosEnterar.AsNoTracking();
        if (periodo is { } filtro)
        {
            consulta = consulta.Where(s => s.Periodo == filtro);
        }
        var filas = await consulta.OrderByDescending(s => s.Periodo).ToListAsync();
        var feriados = await _db.Feriados.AsNoTracking().ToListAsync();
        var inhabiles = DiaHabilRules.Inhabiles(feriados.Select(f => (f.Fecha, f.MedioDia)));
        var anios = feriados.Select(f => f.Fecha.Year).ToHashSet();
        var hoy = Hoy();
        return (filas.Select(s => ConstruirSaldo(s, hoy, inhabiles, anios)).ToList(), null);
    }

    private static SaldoEnterarDto ConstruirSaldo(VwSaldoEnterar s, DateOnly hoy, IReadOnlySet<DateOnly> inhabiles, HashSet<int> anios)
    {
        var vence = DiaHabilRules.DecimoDiaHabil(s.Periodo, inhabiles);
        var estado = DiaHabilRules.EstadoVencimiento(hoy, vence, s.Saldo);
        return new SaldoEnterarDto(
            s.Periodo, s.Retenido, s.Enterado, s.Saldo, vence, DiaHabilRules.DiasParaVencer(hoy, vence), estado,
            estado == DiaHabilRules.Vencido ? AvisoMulta : null, !anios.Contains(vence.Year));
    }

    public async Task<(VistaPreviaEnteramientoDto? Vista, ErrorValidacion? Error)> VistaPreviaAsync(SolicitudVistaPreviaEnteramiento req)
    {
        var errorDatos = EnteramientoRules.ValidarPeriodo(req.Periodo) ?? EnteramientoRules.ValidarMonto(req.Monto);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var (banco, cuentaIsrId, errorCuentas) = await ResolverCuentasAsync(req.CuentaBancariaId);
        if (errorCuentas is not null)
        {
            return (null, errorCuentas);
        }
        var saldoAntes = await SaldoPeriodoAsync(req.Periodo);
        var errorSaldo = EnteramientoRules.ValidarSaldo(req.Monto, saldoAntes);
        if (errorSaldo is not null)
        {
            return (null, ErrorValidacion.Conflicto(errorSaldo));
        }
        var ids = new[] { cuentaIsrId, banco!.CuentaContableId };
        var cuentas = await _db.CuentasContables.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var lineas = new[]
        {
            Linea(cuentas[cuentaIsrId], req.Monto, 0m),
            Linea(cuentas[banco.CuentaContableId], 0m, req.Monto),
        };
        return (new VistaPreviaEnteramientoDto(lineas, saldoAntes, saldoAntes - req.Monto), null);
    }

    private static LineaPartidaDto Linea(CuentaContable c, decimal debe, decimal haber) => new(c.Id, c.Codigo, c.Nombre, debe, haber);

    public async Task<(EnteramientoDto? Enteramiento, ErrorValidacion? Error)> RegistrarAsync(SolicitudEnteramiento req, int usuarioId)
    {
        var errorDatos = EnteramientoRules.ValidarSolicitud(req.Formulario, req.Periodo, req.FechaPago, Hoy(), req.Boleta, req.Monto);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var (banco, cuentaIsrId, errorCuentas) = await ResolverCuentasAsync(req.CuentaBancariaId);
        if (errorCuentas is not null)
        {
            return (null, errorCuentas);
        }
        var errorSaldo = EnteramientoRules.ValidarSaldo(req.Monto, await SaldoPeriodoAsync(req.Periodo));
        if (errorSaldo is not null)
        {
            return (null, ErrorValidacion.Conflicto(errorSaldo));
        }
        var (_, errorPeriodo) = await _tesoreria.ResolverPeriodoAbiertoAsync(req.FechaPago);
        if (errorPeriodo is not null)
        {
            return (null, ErrorValidacion.Conflicto(errorPeriodo));
        }

        var boleta = req.Boleta!.Trim();
        var descripcion = EnteramientoRules.Descripcion(req.Formulario!, boleta, req.Periodo);
        var (asiento, movimiento, _, errorPreparar) = await _tesoreria.PrepararManualAsync(
            new NuevoMovimientoManual
            {
                CuentaBancariaId = banco!.Id,
                Fecha = req.FechaPago,
                Tipo = MovimientoTesoreria.TipoEgreso,
                Monto = req.Monto,
                Descripcion = descripcion,
                Referencia = boleta,
                CuentaContrapartidaId = cuentaIsrId,
            },
            usuarioId);
        if (errorPreparar is not null)
        {
            return (null, errorPreparar);
        }

        Enteramiento? creado = null;
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "registrar_enteramiento", Tabla, async () =>
        {
            await _tesoreria.PersistirAsync(asiento!, movimiento!);
            creado = new Enteramiento
            {
                Formulario = req.Formulario!,
                Periodo = req.Periodo,
                FechaPago = req.FechaPago,
                Boleta = boleta,
                Monto = req.Monto,
                CuentaBancariaId = banco.Id,
                AsientoId = asiento!.Id,
                MovimientoTesoreriaId = movimiento!.Id,
                RegistradoPor = usuarioId,
                CreadoEn = _tiempo.GetUtcNow(),
            };
            _db.Enteramientos.Add(creado);
            await _db.SaveChangesAsync();
            return $"Enteramiento {creado.Id}: {descripcion}, monto Q{creado.Monto}, asiento {asiento.Id}";
        });
        return error is null ? (EnteramientoDto.Desde(creado!), null) : (null, error);
    }

    public async Task<(EnteramientoDto? Enteramiento, ErrorValidacion? Error)> AnularAsync(int id, string? motivo, int usuarioId)
    {
        var errorMotivo = EnteramientoRules.ValidarMotivo(motivo);
        if (errorMotivo is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorMotivo));
        }
        var entero = await _db.Enteramientos.FirstOrDefaultAsync(e => e.Id == id);
        if (entero is null)
        {
            return (null, ErrorValidacion.NoEncontrado("El enteramiento indicado no existe."));
        }
        if (entero.Estado == Enteramiento.EstadoAnulado)
        {
            return (null, ErrorValidacion.Conflicto("El enteramiento ya está anulado."));
        }
        var hoy = Hoy();
        var (_, errorPeriodo) = await _tesoreria.ResolverPeriodoAbiertoAsync(hoy);
        if (errorPeriodo is not null)
        {
            return (null, ErrorValidacion.Conflicto(errorPeriodo));
        }

        var original = await _db.AsientosContables.AsNoTracking().Include(a => a.Lineas).FirstAsync(a => a.Id == entero.AsientoId);
        var cuentaIsrId = original.Lineas.First(l => l.Debito > 0).CuentaId;
        var (asiento, movimiento, _, errorPreparar) = await _tesoreria.PrepararManualAsync(
            new NuevoMovimientoManual
            {
                CuentaBancariaId = entero.CuentaBancariaId,
                Fecha = hoy,
                Tipo = MovimientoTesoreria.TipoIngreso,
                Monto = entero.Monto,
                Descripcion = $"Anulación del enteramiento {entero.Id} boleta {entero.Boleta}",
                Referencia = entero.Boleta,
                CuentaContrapartidaId = cuentaIsrId,
            },
            usuarioId);
        if (errorPreparar is not null)
        {
            return (null, errorPreparar);
        }

        asiento!.ReversaDeId = entero.AsientoId;
        var motivoLimpio = motivo!.Trim();
        var error = await _auditoria.EjecutarComoErrorAsync(usuarioId, "anular_enteramiento", Tabla, async () =>
        {
            await _tesoreria.PersistirAsync(asiento, movimiento!);
            entero.Estado = Enteramiento.EstadoAnulado;
            entero.MotivoAnulacion = motivoLimpio;
            entero.AnuladoPor = usuarioId;
            entero.AnuladoEn = _tiempo.GetUtcNow();
            entero.AsientoReversaId = asiento.Id;
            entero.MovimientoReversaId = movimiento!.Id;
            await _db.SaveChangesAsync();
            return $"Enteramiento {id} anulado con el asiento {asiento.Id}, motivo: {motivoLimpio}";
        });
        return error is null ? (EnteramientoDto.Desde(entero), null) : (null, error);
    }

    public async Task<IReadOnlyList<EnteramientoDto>> ListarAsync(DateOnly? periodo, string? estado)
    {
        var consulta = _db.Enteramientos.AsNoTracking();
        if (periodo is { } p)
        {
            consulta = consulta.Where(e => e.Periodo == p);
        }
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var normalizado = estado.Trim().ToUpperInvariant();
            consulta = consulta.Where(e => e.Estado == normalizado);
        }
        var filas = await consulta.OrderByDescending(e => e.FechaPago).ThenByDescending(e => e.Id).ToListAsync();
        return filas.Select(EnteramientoDto.Desde).ToList();
    }

    // Banco existente, activo y en moneda funcional (409 si no) y cuenta ISR NR por pagar configurada.
    private async Task<(CuentaBancaria? Banco, int CuentaIsrId, ErrorValidacion? Error)> ResolverCuentasAsync(int cuentaBancariaId)
    {
        var banco = await _db.CuentasBancarias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cuentaBancariaId);
        if (banco is null)
        {
            return (null, 0, ErrorValidacion.NoEncontrado("La cuenta bancaria indicada no existe."));
        }
        if (!banco.Activa)
        {
            return (null, 0, ErrorValidacion.Conflicto($"La cuenta bancaria '{banco.Banco} {banco.Numero}' está inactiva."));
        }
        if (!await _db.Monedas.AsNoTracking().AnyAsync(m => m.Id == banco.MonedaId && m.EsFuncional))
        {
            return (null, 0, ErrorValidacion.Conflicto("El enteramiento solo se paga desde una cuenta bancaria en quetzales."));
        }
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaIsrId, errorCuenta) = await _configuracion.ResolverCuentaAsync(
            configuracion.CuentaIsrNrRetenidoPagarId, "ISR retenido a no residentes por pagar", "enteramientos");
        return errorCuenta is null ? (banco, cuentaIsrId!.Value, null) : (null, 0, errorCuenta);
    }
}
