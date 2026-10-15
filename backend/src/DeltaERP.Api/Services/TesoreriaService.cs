using System.Globalization;
using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public sealed record PagoBancario(
    CuentaBancaria Cuenta,
    int PeriodoId,
    IReadOnlyList<(int CuentaId, decimal Monto)> Contrapartidas,
    IReadOnlyList<(int CuentaId, decimal Monto)> Retenciones,
    IReadOnlyList<AplicacionLiquidada> Aplicaciones,
    int MonedaDocumentoId,
    string MonedaDocumentoCodigo,
    decimal MontoCuenta,
    decimal MontoBancoGtq,
    int? TipoCambioId,
    decimal TipoCambio,
    DiferencialLiquidacion Diferencial,
    bool UsaLiquidacion)
{
    public decimal Neto => Contrapartidas.Sum(c => c.Monto) - Retenciones.Sum(r => r.Monto);

    public decimal TotalAplicado => Aplicaciones.Sum(a => a.MontoOriginal);

    public decimal TotalValorGtq => Aplicaciones.Sum(a => a.ValorGtq);

    public decimal MontoNetoDocumento => UsaLiquidacion ? TotalAplicado : Neto;

    public decimal? TipoCambioPago => TipoCambioId is null ? null : TipoCambio;

    public DiferencialRealizadoDto DiferencialRealizado => new(Diferencial.Ganancia, Diferencial.Perdida);
}

public class TesoreriaService
{
    private const string TablaMovimientos = "movimientotesoreria";
    private const string PrefijoManual = "TES-MAN";
    private const string PrefijoTransferencia = "TES-TRF";
    private const string PrefijoApertura = "TES-APE";
    private const string ContextoCambiario = "cobros y pagos en moneda extranjera";
    private const string ContextoTransferencia = "transferencias entre monedas";

    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly AuditoriaService _auditoria;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly TipoCambioService _tiposCambio;

    public TesoreriaService(
        DeltaErpDbContext db, ValidacionContable validacion, AuditoriaService auditoria,
        ConfiguracionFiscalService configuracion, TipoCambioService tiposCambio)
    {
        _db = db;
        _validacion = validacion;
        _auditoria = auditoria;
        _configuracion = configuracion;
        _tiposCambio = tiposCambio;
    }

    private sealed record MonedasPago(CuentaMonedaInfo Cuenta, IReadOnlyList<DocumentoMonedaInfo> Documentos);

    private sealed record LiquidacionPago(
        IReadOnlyList<AplicacionLiquidada> Aplicaciones, TipoCambioAplicable? CambioCuenta, DiferencialLiquidacion Diferencial);

    public static string MensajeSinPeriodo(DateOnly fecha) =>
        $"No existe un periodo contable que contenga la fecha {fecha:yyyy-MM-dd}.";

    private async Task<string?> ValidarContrapartidaAsync(int contrapartidaId, int cuentaContableBancoId)
    {
        if (contrapartidaId == cuentaContableBancoId)
        {
            return "La contrapartida no puede ser la cuenta contable del banco.";
        }
        if (await _db.CuentasBancarias.AnyAsync(b => b.CuentaContableId == contrapartidaId))
        {
            return "La contrapartida no puede ser la cuenta contable de una cuenta bancaria; usa una transferencia.";
        }
        return await _validacion.ValidarCuentasPorIdAsync([contrapartidaId]);
    }

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

    private Task<Moneda> MonedaDeCuentaAsync(CuentaBancaria cuenta) =>
        _db.Monedas.AsNoTracking().FirstAsync(m => m.Id == cuenta.MonedaId);

    private async Task<Moneda> ResolverMonedaCuentaAsync(string? codigo)
    {
        var normalizado = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
        return normalizado is null
            ? await _db.Monedas.AsNoTracking().FirstAsync(m => m.EsFuncional)
            : await _db.Monedas.AsNoTracking().FirstAsync(m => m.Codigo == normalizado);
    }

    // Valor en quetzales de un importe en la moneda de la cuenta, a la referencia de la fecha (RN-27).
    private async Task<(decimal ValorGtq, TipoCambioAplicable? Cambio, ErrorValidacion? Error)> ValorarAsync(Moneda moneda, DateOnly fecha, decimal monto)
    {
        if (moneda.EsFuncional)
        {
            return (monto, null, null);
        }
        var (cambio, error) = await _tiposCambio.ResolverParaDocumentoAsync(moneda.Codigo, fecha);
        if (cambio is null)
        {
            return (0m, null, error);
        }
        return (CambioRules.ConvertirAGtq(monto, cambio.Tasa), cambio, null);
    }

    private static MovimientoTesoreria ConCambio(MovimientoTesoreria movimiento, decimal montoGtq, TipoCambioAplicable? cambio)
    {
        movimiento.MontoGtq = montoGtq;
        if (cambio?.TipoCambioId is { } tipoCambioId)
        {
            movimiento.TipoCambio = cambio.Tasa;
            movimiento.TipoCambioId = tipoCambioId;
        }
        return movimiento;
    }

    public async Task<(CuentaBancaria? Cuenta, ErrorValidacion? Error)> CrearCuentaAsync(CuentaBancariaRequest req, int usuarioId)
    {
        var error = TesoreriaRules.ValidarCuentaBancaria(req.Banco, req.Numero, req.Tipo)
            ?? TesoreriaRules.ValidarMonedaCuenta(req.MonedaCodigo)
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
            return (null, ErrorValidacion.Solicitud(error));
        }

        var moneda = await ResolverMonedaCuentaAsync(req.MonedaCodigo);
        var (valorApertura, cambio, errorCambio) = conApertura
            ? await ValorarAsync(moneda, req.FechaApertura!.Value, req.SaldoApertura)
            : (0m, null, null);
        if (errorCambio is not null)
        {
            return (null, errorCambio);
        }

        var cuenta = new CuentaBancaria
        {
            Banco = req.Banco!.Trim(),
            Numero = req.Numero!.Trim(),
            Tipo = req.Tipo!,
            CuentaContableId = req.CuentaContableId,
            MonedaId = moneda.Id,
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
                    cuenta.CuentaContableId, UnaContrapartida(req.CuentaContrapartidaId!.Value, valorApertura));
                var movimientoApertura = ConCambio(
                    NuevoMovimiento(cuenta.Id, fechaApertura, MovimientoTesoreria.TipoIngreso, req.SaldoApertura, "Saldo de apertura", null, MovimientoTesoreria.OrigenApertura),
                    valorApertura, cambio);
                await InsertarAsientoYMovimientosAsync(asientoApertura, new[] { movimientoApertura });
            }
            return $"Cuenta bancaria {cuenta.Id} ({cuenta.Banco} {cuenta.Numero}) creada, saldo de apertura {cuenta.SaldoApertura}, moneda {moneda.Codigo}";
        });
        return (cuenta, null);
    }

    public async Task<(MovimientoTesoreria? Movimiento, ErrorValidacion? Error)> RegistrarManualAsync(NuevoMovimientoManual req, int usuarioId)
    {
        var errorDatos = TesoreriaRules.ValidarMovimiento(req.Tipo, req.Monto, req.Descripcion);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var (cuenta, errorCuenta) = await ValidarCuentaOperableAsync(req.CuentaBancariaId);
        if (errorCuenta is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorCuenta));
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(req.Fecha);
        if (errorPeriodo is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorPeriodo));
        }
        var errorContrapartida = await ValidarContrapartidaAsync(req.CuentaContrapartidaId, cuenta!.CuentaContableId);
        if (errorContrapartida is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorContrapartida));
        }
        var (valorGtq, cambio, errorCambio) = await ValorarAsync(await MonedaDeCuentaAsync(cuenta), req.Fecha, req.Monto);
        if (errorCambio is not null)
        {
            return (null, errorCambio);
        }

        var asiento = AsientoTesoreriaBuilder.Construir(
            PrefijoManual, req.Fecha, periodoId, usuarioId, req.Tipo!, cuenta.CuentaContableId,
            UnaContrapartida(req.CuentaContrapartidaId, valorGtq));
        var movimiento = ConCambio(
            NuevoMovimiento(cuenta.Id, req.Fecha, req.Tipo!, req.Monto, req.Descripcion!.Trim(), req.Referencia, MovimientoTesoreria.OrigenManual),
            valorGtq, cambio);
        await _auditoria.EjecutarAsync(usuarioId, "registrar_movimiento_tesoreria", TablaMovimientos, async () =>
        {
            await InsertarAsientoYMovimientosAsync(asiento, new[] { movimiento });
            return $"Movimiento {movimiento.Id} ({movimiento.Tipo} {movimiento.Monto}) en cuenta {cuenta.Banco} {cuenta.Numero}";
        });
        return (movimiento, null);
    }

    public async Task<(TransferenciaResultado? Resultado, ErrorValidacion? Error)> RegistrarTransferenciaAsync(NuevaTransferencia req, int usuarioId)
    {
        var errorDatos = TesoreriaRules.ValidarTransferencia(req.CuentaOrigenId, req.CuentaDestinoId, req.Monto, req.Descripcion);
        if (errorDatos is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDatos));
        }
        var (origen, errorOrigen) = await ValidarCuentaOperableAsync(req.CuentaOrigenId);
        if (errorOrigen is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorOrigen));
        }
        var (destino, errorDestino) = await ValidarCuentaOperableAsync(req.CuentaDestinoId);
        if (errorDestino is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorDestino));
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(req.Fecha);
        if (errorPeriodo is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorPeriodo));
        }
        var mismaMoneda = origen!.MonedaId == destino!.MonedaId;
        var errorCruzada = TesoreriaRules.ValidarTransferenciaCruzada(req.Monto, req.MontoDestino, mismaMoneda);
        if (errorCruzada is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorCruzada));
        }
        if (!mismaMoneda)
        {
            return await RegistrarTransferenciaCruzadaAsync(req, origen, destino, periodoId, usuarioId);
        }
        var (valorGtq, cambio, errorCambio) = await ValorarAsync(await MonedaDeCuentaAsync(origen), req.Fecha, req.Monto);
        if (errorCambio is not null)
        {
            return (null, errorCambio);
        }

        var asiento = AsientoTesoreriaBuilder.Construir(
            PrefijoTransferencia, req.Fecha, periodoId, usuarioId, MovimientoTesoreria.TipoIngreso,
            destino.CuentaContableId, UnaContrapartida(origen.CuentaContableId, valorGtq));
        var transferenciaId = Guid.NewGuid();
        var descripcion = req.Descripcion!.Trim();
        var egreso = ConCambio(NuevoMovimiento(origen.Id, req.Fecha, MovimientoTesoreria.TipoEgreso, req.Monto, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia), valorGtq, cambio);
        var ingreso = ConCambio(NuevoMovimiento(destino.Id, req.Fecha, MovimientoTesoreria.TipoIngreso, req.Monto, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia), valorGtq, cambio);
        egreso.TransferenciaId = transferenciaId;
        ingreso.TransferenciaId = transferenciaId;
        await _auditoria.EjecutarAsync(usuarioId, "registrar_transferencia_tesoreria", TablaMovimientos, async () =>
        {
            await InsertarAsientoYMovimientosAsync(asiento, new[] { egreso, ingreso });
            return $"Transferencia {transferenciaId} de {origen.Banco} {origen.Numero} a {destino.Banco} {destino.Numero}, monto {req.Monto}";
        });
        return (new TransferenciaResultado(transferenciaId, asiento.Id, egreso, ingreso, new DiferencialRealizadoDto(0m, 0m)), null);
    }

    private async Task<(TransferenciaResultado? Resultado, ErrorValidacion? Error)> RegistrarTransferenciaCruzadaAsync(
        NuevaTransferencia req, CuentaBancaria origen, CuentaBancaria destino, int periodoId, int usuarioId)
    {
        var (valorOrigen, cambioOrigen, errorOrigen) = await ValorarAsync(await MonedaDeCuentaAsync(origen), req.Fecha, req.Monto);
        if (errorOrigen is not null)
        {
            return (null, errorOrigen);
        }
        var (valorDestino, cambioDestino, errorDestino) = await ValorarAsync(await MonedaDeCuentaAsync(destino), req.Fecha, req.MontoDestino!.Value);
        if (errorDestino is not null)
        {
            return (null, errorDestino);
        }
        var diferencia = valorDestino - valorOrigen;
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaGanancia, errorGanancia) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            Math.Max(diferencia, 0m), configuracion.CuentaDifGananciaRealizadaId, "ganancia cambiaria realizada", ContextoTransferencia);
        var (cuentaPerdida, errorPerdida) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            Math.Max(-diferencia, 0m), configuracion.CuentaDifPerdidaRealizadaId, "pérdida cambiaria realizada", ContextoTransferencia);
        if ((errorGanancia ?? errorPerdida) is { } errorCuenta)
        {
            return (null, errorCuenta);
        }

        var asiento = AsientoTesoreriaBuilder.ConstruirTransferenciaCruzada(
            new CabeceraAsiento(PrefijoTransferencia, req.Fecha, periodoId, usuarioId), destino.CuentaContableId, valorDestino,
            origen.CuentaContableId, valorOrigen, cuentaGanancia, cuentaPerdida);
        var transferenciaId = Guid.NewGuid();
        var descripcion = req.Descripcion!.Trim();
        var egreso = ConCambio(NuevoMovimiento(origen.Id, req.Fecha, MovimientoTesoreria.TipoEgreso, req.Monto, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia), valorOrigen, cambioOrigen);
        var ingreso = ConCambio(NuevoMovimiento(destino.Id, req.Fecha, MovimientoTesoreria.TipoIngreso, req.MontoDestino!.Value, descripcion, req.Referencia, MovimientoTesoreria.OrigenTransferencia), valorDestino, cambioDestino);
        egreso.TransferenciaId = transferenciaId;
        ingreso.TransferenciaId = transferenciaId;
        await _auditoria.EjecutarAsync(usuarioId, "registrar_transferencia_tesoreria", TablaMovimientos, async () =>
        {
            await InsertarAsientoYMovimientosAsync(asiento, new[] { egreso, ingreso });
            return $"Transferencia {transferenciaId} entre monedas de {origen.Banco} {origen.Numero} a {destino.Banco} {destino.Numero}, monto {req.Monto}, destino {req.MontoDestino}, diferencial realizado Q{diferencia:+0.00;-0.00;0.00}";
        });
        return (new TransferenciaResultado(transferenciaId, asiento.Id, egreso, ingreso,
            new DiferencialRealizadoDto(Math.Max(diferencia, 0m), Math.Max(-diferencia, 0m))), null);
    }

    public async Task<(PagoBancario? Pago, ErrorValidacion? Error)> PrepararPagoAsync(
        PerfilFactura perfil,
        int? cuentaBancariaId,
        DateOnly fecha,
        IReadOnlyCollection<DocumentoPagable> documentos,
        IReadOnlyList<AplicacionEntrada> entradas,
        IReadOnlyList<(int CuentaId, decimal Monto)>? retenciones = null)
    {
        if (cuentaBancariaId is null)
        {
            return (null, ErrorValidacion.Solicitud("La cuenta bancaria es obligatoria para registrar un cobro o un pago."));
        }
        var retenido = retenciones ?? [];
        if (TesoreriaRules.ValidarEscalaPago(entradas.Select(a => a.Monto)) is { } errorEscala)
        {
            return (null, ErrorValidacion.Solicitud(errorEscala));
        }
        var (cuenta, errorCuenta) = await ValidarCuentaOperableAsync(cuentaBancariaId.Value);
        if (errorCuenta is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorCuenta));
        }
        var (periodoId, errorPeriodo) = await ResolverPeriodoAbiertoAsync(fecha);
        if (errorPeriodo is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorPeriodo));
        }
        var (controles, errorControl) = await ResolverControlesAsync(perfil, documentos);
        if (errorControl is not null)
        {
            return (null, ErrorValidacion.Solicitud(errorControl));
        }

        var monedas = await MonedasDePagoAsync(cuenta!, documentos);
        var errorMoneda = PagoMonedaRules.Validar(monedas.Cuenta, monedas.Documentos, entradas);
        if (errorMoneda is not null)
        {
            return (null, errorMoneda.EsConflicto ? ErrorValidacion.Conflicto(errorMoneda.Mensaje) : ErrorValidacion.Solicitud(errorMoneda.Mensaje));
        }
        var (liquidacion, errorLiquidacion) = await LiquidarAsync(perfil.EsVenta, fecha, documentos, entradas, monedas);
        if (liquidacion is null)
        {
            return (null, errorLiquidacion);
        }
        if (TesoreriaRules.ValidarCobroNeto(liquidacion.Aplicaciones.Sum(a => a.LibrosGtq), retenido.Sum(r => r.Monto)) is { } errorNeto)
        {
            return (null, ErrorValidacion.Solicitud(errorNeto));
        }
        var contrapartidas = TesoreriaRules.AgruparPorCuentaControl(
            liquidacion.Aplicaciones.Select(l => (l.DocumentoId, Monto: l.LibrosGtq)).ToList(), controles);
        return (ConstruirPago(cuenta!, periodoId, contrapartidas, retenido, liquidacion, monedas), null);
    }

    private async Task<(IReadOnlyDictionary<int, int> Controles, string? Error)> ResolverControlesAsync(
        PerfilFactura perfil, IReadOnlyCollection<DocumentoPagable> documentos)
    {
        var controlPorAsiento = await ObtenerControlPorAsientoAsync(perfil.LadoControl, documentos.Select(d => d.AsientoId));
        var controles = new Dictionary<int, int>();
        foreach (var documento in documentos)
        {
            if (documento.AsientoId is null || !controlPorAsiento.TryGetValue(documento.AsientoId.Value, out var cuentaControlId))
            {
                return (controles, $"No se pudo determinar la cuenta de control de la factura {documento.Numero}.");
            }
            controles[documento.Id] = cuentaControlId;
        }
        return (controles, null);
    }

    private async Task<MonedasPago> MonedasDePagoAsync(CuentaBancaria cuenta, IReadOnlyCollection<DocumentoPagable> documentos)
    {
        var ids = documentos.Select(d => d.MonedaId).Append(cuenta.MonedaId).Distinct().ToList();
        var monedas = await _db.Monedas.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        var infoCuenta = new CuentaMonedaInfo(cuenta.MonedaId, monedas[cuenta.MonedaId].Codigo, monedas[cuenta.MonedaId].EsFuncional);
        var infoDocumentos = documentos
            .Select(d => new DocumentoMonedaInfo(d.Id, d.Numero, d.MonedaId, monedas[d.MonedaId].Codigo, monedas[d.MonedaId].EsFuncional))
            .ToList();
        return new MonedasPago(infoCuenta, infoDocumentos);
    }

    private async Task<(LiquidacionPago? Liquidacion, ErrorValidacion? Error)> LiquidarAsync(
        bool esCobro, DateOnly fecha, IReadOnlyCollection<DocumentoPagable> documentos, IReadOnlyList<AplicacionEntrada> entradas, MonedasPago monedas)
    {
        TipoCambioAplicable? cambioCuenta = null;
        if (!monedas.Cuenta.EsFuncional)
        {
            var (cambio, error) = await _tiposCambio.ResolverParaDocumentoAsync(monedas.Cuenta.MonedaCodigo, fecha);
            if (cambio is null)
            {
                return (null, error);
            }
            cambioCuenta = cambio;
        }
        var aplicaciones = entradas
            .Select(e => LiquidarEntrada(esCobro, e, documentos.First(d => d.Id == e.DocumentoId), monedas, cambioCuenta))
            .ToList();
        var (ganancia, perdida) = DiferencialCambiarioRules.Totalizar(aplicaciones);
        var (diferencial, errorCuentas) = await ResolverCuentasDiferencialAsync(ganancia, perdida);
        if (diferencial is null)
        {
            return (null, errorCuentas);
        }
        return (new LiquidacionPago(aplicaciones, cambioCuenta, diferencial), null);
    }

    private static AplicacionLiquidada LiquidarEntrada(
        bool esCobro, AplicacionEntrada entrada, DocumentoPagable documento, MonedasPago monedas, TipoCambioAplicable? cambioCuenta)
    {
        if (monedas.Documentos.First(m => m.DocumentoId == documento.Id).EsFuncional)
        {
            return DiferencialCambiarioRules.SinDiferencial(documento.Id, entrada.Monto);
        }
        var valor = cambioCuenta is null
            ? entrada.ValorEfectivoGtq!.Value
            : DiferencialCambiarioRules.ValorEnCuentaMonedaExtranjera(entrada.Monto, cambioCuenta.Tasa);
        var saldo = new SaldoDocumentoMe(documento.SaldoPendiente, documento.SaldoPendienteGtq, documento.TipoCambio);
        return DiferencialCambiarioRules.Liquidar(esCobro, documento.Id, entrada.Monto, valor, saldo);
    }

    private async Task<(DiferencialLiquidacion? Diferencial, ErrorValidacion? Error)> ResolverCuentasDiferencialAsync(decimal ganancia, decimal perdida)
    {
        if (ganancia == 0 && perdida == 0)
        {
            return (DiferencialLiquidacion.Ninguno, null);
        }
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaGanancia, errorGanancia) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            ganancia, configuracion.CuentaDifGananciaRealizadaId, "ganancia cambiaria realizada", ContextoCambiario);
        var (cuentaPerdida, errorPerdida) = await _configuracion.ResolverCuentaSiHayMontoAsync(
            perdida, configuracion.CuentaDifPerdidaRealizadaId, "pérdida cambiaria realizada", ContextoCambiario);
        var error = errorGanancia ?? errorPerdida;
        if (error is not null)
        {
            return (null, error);
        }
        return (new DiferencialLiquidacion(cuentaGanancia, cuentaPerdida, ganancia, perdida), null);
    }

    private static PagoBancario ConstruirPago(
        CuentaBancaria cuenta, int periodoId, List<(int CuentaId, decimal Monto)> contrapartidas,
        IReadOnlyList<(int CuentaId, decimal Monto)> retenido, LiquidacionPago liquidacion, MonedasPago monedas)
    {
        var usaLiquidacion = !monedas.Cuenta.EsFuncional || monedas.Documentos.Any(d => !d.EsFuncional);
        var neto = contrapartidas.Sum(c => c.Monto) - retenido.Sum(r => r.Monto);
        var totalValor = liquidacion.Aplicaciones.Sum(a => a.ValorGtq);
        var montoCuenta = neto;
        var montoBanco = neto;
        if (usaLiquidacion)
        {
            montoBanco = totalValor;
            montoCuenta = liquidacion.CambioCuenta is null ? totalValor : liquidacion.Aplicaciones.Sum(a => a.MontoOriginal);
        }
        var monedaDocumento = monedas.Documentos[0];
        return new PagoBancario(
            cuenta, periodoId, contrapartidas, retenido, liquidacion.Aplicaciones, monedaDocumento.MonedaId, monedaDocumento.MonedaCodigo,
            montoCuenta, montoBanco, liquidacion.CambioCuenta?.TipoCambioId, liquidacion.CambioCuenta?.Tasa ?? 1m,
            liquidacion.Diferencial, usaLiquidacion);
    }

    // El orden de pago.Aplicaciones coincide con el de las aplicaciones del recibo o pago.
    public static void AplicarLiquidacion(IReadOnlyList<IAplicacionPago> aplicaciones, PagoBancario pago)
    {
        for (var i = 0; i < aplicaciones.Count; i++)
        {
            var liquidada = pago.Aplicaciones[i];
            aplicaciones[i].MontoAplicadoGtq = liquidada.LibrosGtq;
            aplicaciones[i].ValorGtq = liquidada.ValorGtq;
            aplicaciones[i].DiferencialGtq = liquidada.ResultadoGtq;
            aplicaciones[i].TipoCambioPagoId = pago.TipoCambioId;
        }
    }

    public static string DescribirDiferencial(PagoBancario pago)
    {
        if (!pago.UsaLiquidacion)
        {
            return string.Empty;
        }
        var neto = pago.Diferencial.Ganancia - pago.Diferencial.Perdida;
        return string.Create(CultureInfo.InvariantCulture, $", moneda {pago.MonedaDocumentoCodigo}, diferencial realizado Q{neto:+0.00;-0.00;0.00}");
    }

    // Sin transacción propia: el llamador ya está dentro de AuditoriaService.EjecutarAsync.
    public async Task<int> RegistrarMovimientoDePagoAsync(
        PagoBancario pago, PerfilFactura perfil, DateOnly fecha, string descripcion, string? referencia, int pagoId, int usuarioId)
    {
        var esCobro = perfil.LadoControl == LadoControl.Debito;
        var tipo = esCobro ? MovimientoTesoreria.TipoIngreso : MovimientoTesoreria.TipoEgreso;
        var prefijo = $"TES-{perfil.Sigla.ToUpperInvariant()}";
        var asiento = ConstruirAsientoDePago(pago, esCobro, tipo, prefijo, fecha, usuarioId);
        var movimiento = NuevoMovimiento(pago.Cuenta.Id, fecha, tipo, pago.MontoCuenta, descripcion, referencia, perfil.Sigla);
        movimiento.MontoGtq = pago.MontoBancoGtq;
        movimiento.TipoCambio = pago.TipoCambio;
        movimiento.TipoCambioId = pago.TipoCambioId;
        if (esCobro)
        {
            movimiento.ReciboPagoId = pagoId;
        }
        else
        {
            movimiento.PagoProveedorId = pagoId;
        }
        await InsertarAsientoYMovimientosAsync(asiento, new[] { movimiento });
        return asiento.Id;
    }

    private static AsientoContable ConstruirAsientoDePago(PagoBancario pago, bool esCobro, string tipo, string prefijo, DateOnly fecha, int usuarioId)
    {
        if (pago.UsaLiquidacion)
        {
            return AsientoTesoreriaBuilder.ConstruirLiquidacion(
                new CabeceraAsiento(prefijo, fecha, pago.PeriodoId, usuarioId), esCobro, pago.Cuenta.CuentaContableId, pago.MontoBancoGtq,
                pago.Contrapartidas, pago.Diferencial);
        }
        return pago.Retenciones.Count == 0
            ? AsientoTesoreriaBuilder.Construir(prefijo, fecha, pago.PeriodoId, usuarioId, tipo, pago.Cuenta.CuentaContableId, pago.Contrapartidas)
            : AsientoTesoreriaBuilder.ConstruirCobroConRetenciones(
                prefijo, fecha, pago.PeriodoId, usuarioId, pago.Cuenta.CuentaContableId, pago.Contrapartidas, pago.Retenciones);
    }

    // La línea de control es la única del lado de control (débito en CxC, crédito en CxP) salvo las retenciones de la factura.
    public async Task<Dictionary<int, int>> ObtenerControlPorAsientoAsync(LadoControl lado, IEnumerable<int?> asientoIds)
    {
        var asientosId = asientoIds.OfType<int>().Distinct().ToList();
        var lineas = _db.LineasAsiento.Where(l => asientosId.Contains(l.AsientoId));
        lineas = lado == LadoControl.Debito
            ? lineas.Where(l => l.Debito > 0)
            : lineas.Where(l => l.Credito > 0);
        lineas = lineas.Where(l => !_db.Retenciones.Any(r => r.AsientoId == l.AsientoId && r.CuentaId == l.CuentaId));
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
        return cuenta is not null && cuenta.Tipo == tipo ? null : mensajeTipo;
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
            MontoGtq = monto,
            TipoCambio = 1m,
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
