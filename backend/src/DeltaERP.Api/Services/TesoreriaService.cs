using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public sealed record PagoBancario(CuentaBancaria Cuenta, int PeriodoId, IReadOnlyList<(int CuentaId, decimal Monto)> Contrapartidas);

public class TesoreriaService
{
    private const string TablaMovimientos = "movimientotesoreria";
    private const string PrefijoManual = "TES-MAN";
    private const string PrefijoTransferencia = "TES-TRF";
    private const string PrefijoApertura = "TES-APE";

    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly AuditoriaService _auditoria;

    public TesoreriaService(DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria)
    {
        _db = db;
        _validacion = validacion;
        _auditoria = auditoria;
    }

    public static string MensajeSinPeriodo(DateOnly fecha) =>
        $"No existe un periodo contable que contenga la fecha {fecha:yyyy-MM-dd}.";

    public async Task<(CuentaBancaria? Cuenta, string? Error)> ValidarCuentaOperableAsync(int id)
    {
        var cuenta = await _db.CuentasBancarias.FindAsync(id);
        if (cuenta is null)
        {
            return (null, "La cuenta bancaria indicada no existe.");
        }
        return cuenta.Activa ? (cuenta, null) : (null, $"La cuenta bancaria '{cuenta.Banco} {cuenta.Numero}' está inactiva.");
    }

    // Prefiere el período Abierto cuando varios contienen la fecha.
    public async Task<PeriodoContable?> BuscarPeriodoPorFechaAsync(DateOnly fecha) =>
        await _db.PeriodosContables
            .Where(p => p.FechaInicio <= fecha && p.FechaFin >= fecha)
            .OrderBy(p => p.Estado == PeriodoContable.EstadoAbierto ? 0 : 1)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync();

    public async Task<(int PeriodoId, string? Error)> ResolverPeriodoAbiertoAsync(DateOnly fecha)
    {
        var periodo = await BuscarPeriodoPorFechaAsync(fecha);
        if (periodo is null)
        {
            return (0, MensajeSinPeriodo(fecha));
        }
        return (periodo.Id, await _validacion.ValidarPeriodoAbiertoAsync(periodo.Id, "movimientos de tesorería"));
    }

    public async Task<(CuentaBancaria? Cuenta, string? Error)> CrearCuentaAsync(CuentaBancariaRequest req, int usuarioId)
    {
        var error = TesoreriaRules.ValidarCuentaBancaria(req.Banco, req.Numero, req.Tipo)
            ?? TesoreriaRules.ValidarApertura(req.SaldoApertura, req.FechaApertura, req.CuentaContrapartidaId)
            ?? await ValidarCuentaDeTipoAsync(req.CuentaContableId, "Activo", "La cuenta contable del banco debe ser de tipo Activo.");
        var conApertura = req.SaldoApertura > 0;
        var periodoApertura = 0;
        if (error is null && conApertura)
        {
            (periodoApertura, error) = await ResolverPeriodoAbiertoAsync(req.FechaApertura!.Value);
            error ??= await ValidarCuentaDeTipoAsync(req.CuentaContrapartidaId!.Value, "Capital", "La cuenta de contrapartida de la apertura debe ser de tipo Capital.");
        }
        if (error is not null)
        {
            return (null, error);
        }

        var cuenta = new CuentaBancaria
        {
            Banco = req.Banco!.Trim(),
            Numero = req.Numero!.Trim(),
            Tipo = req.Tipo!,
            CuentaContableId = req.CuentaContableId,
            SaldoApertura = req.SaldoApertura,
            FechaApertura = conApertura ? req.FechaApertura : null,
        };
        await _auditoria.EjecutarAsync(usuarioId, "crear_cuenta_bancaria", "cuentabancaria", async () =>
        {
            _db.CuentasBancarias.Add(cuenta);
            await _db.SaveChangesAsync();
            if (conApertura)
            {
                var fechaApertura = req.FechaApertura!.Value;
                var asientoApertura = AsientoTesoreriaBuilder.Construir(
                    PrefijoApertura, fechaApertura, periodoApertura, usuarioId, MovimientoTesoreria.TipoIngreso,
                    cuenta.CuentaContableId, UnaContrapartida(req.CuentaContrapartidaId!.Value, req.SaldoApertura));
                var movimientoApertura = NuevoMovimiento(
                    cuenta.Id, fechaApertura, MovimientoTesoreria.TipoIngreso, req.SaldoApertura,
                    "Saldo de apertura", null, MovimientoTesoreria.OrigenApertura);
                await InsertarAsientoYMovimientosAsync(asientoApertura, new[] { movimientoApertura });
            }
            return $"Cuenta bancaria {cuenta.Id} ({cuenta.Banco} {cuenta.Numero}) creada, saldo de apertura {cuenta.SaldoApertura}";
        });
        return (cuenta, null);
    }

    public async Task<(MovimientoTesoreria? Movimiento, string? Error)> RegistrarManualAsync(NuevoMovimientoManual req, int usuarioId)
    {
        var errorDatos = TesoreriaRules.ValidarMovimiento(req.Tipo, req.Monto, req.Descripcion);
        if (errorDatos is not null)
        {
            return (null, errorDatos);
        }
        var (cuenta, errorCuenta) = await ValidarCuentaOperableAsync(req.CuentaBancariaId);
        if (errorCuenta is not null)
        {
            return (null, errorCuenta);
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(req.Fecha);
        if (errorPeriodo is not null)
        {
            return (null, errorPeriodo);
        }
        var errorContrapartida = req.CuentaContrapartidaId == cuenta!.CuentaContableId
            ? "La contrapartida no puede ser la cuenta contable del banco."
            : await _db.CuentasBancarias.AnyAsync(b => b.CuentaContableId == req.CuentaContrapartidaId)
            ? "La contrapartida no puede ser la cuenta contable de una cuenta bancaria; usa una transferencia."
            : await _validacion.ValidarCuentasPorIdAsync(new[] { req.CuentaContrapartidaId });
        if (errorContrapartida is not null)
        {
            return (null, errorContrapartida);
        }

        var asiento = AsientoTesoreriaBuilder.Construir(
            PrefijoManual, req.Fecha, periodoId, usuarioId, req.Tipo!, cuenta.CuentaContableId,
            UnaContrapartida(req.CuentaContrapartidaId, req.Monto));
        var movimiento = NuevoMovimiento(
            cuenta.Id, req.Fecha, req.Tipo!, req.Monto, req.Descripcion!.Trim(), req.Referencia, MovimientoTesoreria.OrigenManual);
        await _auditoria.EjecutarAsync(usuarioId, "registrar_movimiento_tesoreria", TablaMovimientos, async () =>
        {
            await InsertarAsientoYMovimientosAsync(asiento, new[] { movimiento });
            return $"Movimiento {movimiento.Id} ({movimiento.Tipo} {movimiento.Monto}) en cuenta {cuenta.Banco} {cuenta.Numero}";
        });
        return (movimiento, null);
    }

    public async Task<(TransferenciaResultado? Resultado, string? Error)> RegistrarTransferenciaAsync(NuevaTransferencia req, int usuarioId)
    {
        var errorDatos = TesoreriaRules.ValidarTransferencia(req.CuentaOrigenId, req.CuentaDestinoId, req.Monto, req.Descripcion);
        if (errorDatos is not null)
        {
            return (null, errorDatos);
        }
        var (origen, errorOrigen) = await ValidarCuentaOperableAsync(req.CuentaOrigenId);
        if (errorOrigen is not null)
        {
            return (null, errorOrigen);
        }
        var (destino, errorDestino) = await ValidarCuentaOperableAsync(req.CuentaDestinoId);
        if (errorDestino is not null)
        {
            return (null, errorDestino);
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(req.Fecha);
        if (errorPeriodo is not null)
        {
            return (null, errorPeriodo);
        }

        var asiento = AsientoTesoreriaBuilder.Construir(
            PrefijoTransferencia, req.Fecha, periodoId, usuarioId, MovimientoTesoreria.TipoIngreso,
            destino!.CuentaContableId, UnaContrapartida(origen!.CuentaContableId, req.Monto));
        var transferenciaId = Guid.NewGuid();
        var descripcion = req.Descripcion!.Trim();
        var egreso = NuevoMovimiento(origen.Id, req.Fecha, MovimientoTesoreria.TipoEgreso, req.Monto, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia);
        var ingreso = NuevoMovimiento(destino.Id, req.Fecha, MovimientoTesoreria.TipoIngreso, req.Monto, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia);
        egreso.TransferenciaId = transferenciaId;
        ingreso.TransferenciaId = transferenciaId;
        await _auditoria.EjecutarAsync(usuarioId, "registrar_transferencia_tesoreria", TablaMovimientos, async () =>
        {
            await InsertarAsientoYMovimientosAsync(asiento, new[] { egreso, ingreso });
            return $"Transferencia {transferenciaId} de {origen.Banco} {origen.Numero} a {destino.Banco} {destino.Numero}, monto {req.Monto}";
        });
        return (new TransferenciaResultado(transferenciaId, asiento.Id, egreso, ingreso), null);
    }

    public async Task<(PagoBancario? Pago, string? Error)> PrepararPagoAsync(
        PerfilFactura perfil,
        int? cuentaBancariaId,
        DateOnly fecha,
        IReadOnlyCollection<DocumentoPagable> documentos,
        IReadOnlyCollection<(int DocumentoId, decimal Monto)> aplicaciones)
    {
        if (cuentaBancariaId is null)
        {
            return (null, "La cuenta bancaria es obligatoria para registrar un cobro o un pago.");
        }
        var errorEscala = TesoreriaRules.ValidarEscalaPago(aplicaciones.Select(a => a.Monto));
        if (errorEscala is not null)
        {
            return (null, errorEscala);
        }
        var (cuenta, errorCuenta) = await ValidarCuentaOperableAsync(cuentaBancariaId.Value);
        if (errorCuenta is not null)
        {
            return (null, errorCuenta);
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(fecha);
        if (errorPeriodo is not null)
        {
            return (null, errorPeriodo);
        }

        var controlPorAsiento = await ObtenerControlPorAsientoAsync(perfil, documentos);
        var controlPorDocumento = new Dictionary<int, int>();
        foreach (var documento in documentos)
        {
            if (documento.AsientoId is null || !controlPorAsiento.TryGetValue(documento.AsientoId.Value, out var cuentaControlId))
            {
                return (null, $"No se pudo determinar la cuenta de control de la factura {documento.Numero}.");
            }
            controlPorDocumento[documento.Id] = cuentaControlId;
        }

        var contrapartidas = TesoreriaRules.AgruparPorCuentaControl(aplicaciones, controlPorDocumento);
        return (new PagoBancario(cuenta!, periodoId, contrapartidas), null);
    }

    // Sin transacción propia: el llamador ya está dentro de AuditoriaService.EjecutarAsync.
    public async Task RegistrarMovimientoDePagoAsync(
        PagoBancario pago, PerfilFactura perfil, DateOnly fecha, string descripcion, string? referencia, int pagoId, int usuarioId)
    {
        var esCobro = perfil.LadoControl == LadoControl.Debito;
        var tipo = esCobro ? MovimientoTesoreria.TipoIngreso : MovimientoTesoreria.TipoEgreso;
        var asiento = AsientoTesoreriaBuilder.Construir(
            $"TES-{perfil.Sigla.ToUpperInvariant()}", fecha, pago.PeriodoId, usuarioId, tipo,
            pago.Cuenta.CuentaContableId, pago.Contrapartidas);
        var movimiento = NuevoMovimiento(
            pago.Cuenta.Id, fecha, tipo, pago.Contrapartidas.Sum(c => c.Monto), descripcion, referencia, perfil.Sigla);
        if (esCobro)
        {
            movimiento.ReciboPagoId = pagoId;
        }
        else
        {
            movimiento.PagoProveedorId = pagoId;
        }
        await InsertarAsientoYMovimientosAsync(asiento, new[] { movimiento });
    }

    // La línea de control es la única del lado de control (débito en CxC, crédito en CxP).
    private async Task<Dictionary<int, int>> ObtenerControlPorAsientoAsync(PerfilFactura perfil, IReadOnlyCollection<DocumentoPagable> documentos)
    {
        var asientosId = documentos.Where(d => d.AsientoId is not null).Select(d => d.AsientoId!.Value).Distinct().ToList();
        var lineas = _db.LineasAsiento.Where(l => asientosId.Contains(l.AsientoId));
        lineas = perfil.LadoControl == LadoControl.Debito
            ? lineas.Where(l => l.Debito > 0)
            : lineas.Where(l => l.Credito > 0);
        var controles = await lineas.OrderBy(l => l.Id).Select(l => new { l.AsientoId, l.CuentaId }).ToListAsync();
        return controles.GroupBy(l => l.AsientoId).ToDictionary(g => g.Key, g => g.First().CuentaId);
    }

    private async Task<string?> ValidarCuentaDeTipoAsync(int cuentaId, string tipo, string mensajeTipo)
    {
        var error = await _validacion.ValidarCuentasPorIdAsync(new[] { cuentaId });
        if (error is not null)
        {
            return error;
        }
        var cuenta = await _db.CuentasContables.FindAsync(cuentaId);
        return cuenta!.Tipo == tipo ? null : mensajeTipo;
    }

    private static (int CuentaId, decimal Monto)[] UnaContrapartida(int cuentaId, decimal monto) =>
        new[] { (CuentaId: cuentaId, Monto: monto) };

    private static MovimientoTesoreria NuevoMovimiento(
        int cuentaBancariaId, DateOnly fecha, string tipo, decimal monto, string descripcion, string? referencia, string origen) =>
        new()
        {
            CuentaBancariaId = cuentaBancariaId,
            Fecha = fecha,
            Tipo = tipo,
            Monto = monto,
            Descripcion = descripcion,
            Referencia = referencia,
            Origen = origen,
        };

    // Sin transacción propia: el llamador ya está dentro de AuditoriaService.EjecutarAsync.
    private async Task InsertarAsientoYMovimientosAsync(AsientoContable asiento, IReadOnlyList<MovimientoTesoreria> movimientos)
    {
        _db.AsientosContables.Add(asiento);
        await _db.SaveChangesAsync();

        foreach (var movimiento in movimientos)
        {
            movimiento.AsientoId = asiento.Id;
        }
        _db.MovimientosTesoreria.AddRange(movimientos);
        await _db.SaveChangesAsync();
    }
}
