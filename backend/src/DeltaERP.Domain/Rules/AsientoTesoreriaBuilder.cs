using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record DiferencialLiquidacion(int? CuentaGananciaId, int? CuentaPerdidaId, decimal Ganancia, decimal Perdida)
{
    public static readonly DiferencialLiquidacion Ninguno = new(null, null, 0m, 0m);
}

public sealed record CabeceraAsiento(string Prefijo, DateOnly Fecha, int PeriodoId, int UsuarioId);

public static class AsientoTesoreriaBuilder
{
    // RN-01: cuadra por construcción (el banco lleva el total; las contrapartidas suman lo mismo).
    public static AsientoContable Construir(
        string prefijo, DateOnly fecha, int periodoId, int usuarioId,
        string tipoMovimiento, int cuentaBancoId,
        IReadOnlyList<(int CuentaId, decimal Monto)> contrapartidas)
    {
        var total = contrapartidas.Sum(c => c.Monto);
        var bancoEsDebito = tipoMovimiento == MovimientoTesoreria.TipoIngreso;

        var asiento = new AsientoContable
        {
            Numero = $"{prefijo}-{fecha:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}",
            Fecha = fecha,
            PeriodoId = periodoId,
            Estado = AsientoContable.EstadoConfirmado,
            UsuarioId = usuarioId,
            Monto = total,
        };
        asiento.Lineas.Add(new LineaAsiento
        {
            CuentaId = cuentaBancoId,
            Debito = bancoEsDebito ? total : 0,
            Credito = bancoEsDebito ? 0 : total,
        });
        foreach (var (cuentaId, monto) in contrapartidas)
        {
            asiento.Lineas.Add(new LineaAsiento
            {
                CuentaId = cuentaId,
                Debito = bancoEsDebito ? 0 : monto,
                Credito = bancoEsDebito ? monto : 0,
            });
        }
        return asiento;
    }

    // RN-01: banco por el neto + retenciones = total de las contrapartidas.
    public static AsientoContable ConstruirCobroConRetenciones(
        string prefijo, DateOnly fecha, int periodoId, int usuarioId, int cuentaBancoId,
        IReadOnlyList<(int CuentaId, decimal Monto)> contrapartidas,
        IReadOnlyList<(int CuentaId, decimal Monto)> retenciones)
    {
        var asiento = Construir(prefijo, fecha, periodoId, usuarioId, MovimientoTesoreria.TipoIngreso, cuentaBancoId, contrapartidas);
        var retenido = retenciones.Sum(r => r.Monto);
        if (retenido >= asiento.Monto)
        {
            throw new InvalidOperationException("Las retenciones deben ser menores al total cobrado.");
        }
        asiento.Lineas[0].Debito -= retenido;
        foreach (var (cuentaId, monto) in retenciones)
        {
            asiento.Lineas.Add(new LineaAsiento { CuentaId = cuentaId, Debito = monto });
        }
        return asiento;
    }

    // RN-01 y RN-26: Σ libros + diferencial = Σ valor; la ganancia va al crédito y la pérdida al débito.
    public static AsientoContable ConstruirLiquidacion(
        CabeceraAsiento cabecera, bool esCobro, int cuentaBancoId, decimal montoBancoGtq,
        IReadOnlyList<(int CuentaId, decimal Monto)> libros, DiferencialLiquidacion diferencial)
    {
        var asiento = NuevoAsiento(cabecera);
        asiento.Lineas.Add(NuevaLinea(cuentaBancoId, montoBancoGtq, esCobro));
        foreach (var (cuentaId, monto) in libros)
        {
            asiento.Lineas.Add(NuevaLinea(cuentaId, monto, !esCobro));
        }
        if (diferencial.Ganancia > 0)
        {
            asiento.Lineas.Add(NuevaLinea(diferencial.CuentaGananciaId ?? throw CuentaFaltante("ganancia"), diferencial.Ganancia, false));
        }
        if (diferencial.Perdida > 0)
        {
            asiento.Lineas.Add(NuevaLinea(diferencial.CuentaPerdidaId ?? throw CuentaFaltante("pérdida"), diferencial.Perdida, true));
        }
        return Cerrar(asiento);
    }

    // RN-01 y RN-27: Σ débitos = Σ créditos con la diferencia contra la referencia en ganancia o pérdida realizada.
    public static AsientoContable ConstruirTransferenciaCruzada(
        CabeceraAsiento cabecera, int cuentaDestinoContableId, decimal montoDestinoGtq, int cuentaOrigenContableId, decimal montoOrigenGtq,
        int? cuentaGananciaId, int? cuentaPerdidaId)
    {
        var asiento = NuevoAsiento(cabecera);
        asiento.Lineas.Add(NuevaLinea(cuentaDestinoContableId, montoDestinoGtq, true));
        asiento.Lineas.Add(NuevaLinea(cuentaOrigenContableId, montoOrigenGtq, false));
        var diferencia = montoDestinoGtq - montoOrigenGtq;
        if (diferencia > 0)
        {
            asiento.Lineas.Add(NuevaLinea(cuentaGananciaId ?? throw CuentaFaltante("ganancia"), diferencia, false));
        }
        if (diferencia < 0)
        {
            asiento.Lineas.Add(NuevaLinea(cuentaPerdidaId ?? throw CuentaFaltante("pérdida"), -diferencia, true));
        }
        return Cerrar(asiento);
    }

    private static AsientoContable NuevoAsiento(CabeceraAsiento cabecera) => new()
    {
        Numero = $"{cabecera.Prefijo}-{cabecera.Fecha:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}",
        Fecha = cabecera.Fecha,
        PeriodoId = cabecera.PeriodoId,
        Estado = AsientoContable.EstadoConfirmado,
        UsuarioId = cabecera.UsuarioId,
    };

    private static LineaAsiento NuevaLinea(int cuentaId, decimal monto, bool esDebito) => new()
    {
        CuentaId = cuentaId,
        Debito = esDebito ? monto : 0,
        Credito = esDebito ? 0 : monto,
    };

    private static InvalidOperationException CuentaFaltante(string lado) => new($"Falta la cuenta de {lado} cambiaria realizada.");

    private static AsientoContable Cerrar(AsientoContable asiento)
    {
        var debitos = asiento.Lineas.Sum(l => l.Debito);
        var creditos = asiento.Lineas.Sum(l => l.Credito);
        if (debitos != creditos)
        {
            throw new InvalidOperationException($"El asiento no cuadra: débitos {debitos:F2} != créditos {creditos:F2}.");
        }
        asiento.Monto = debitos;
        return asiento;
    }
}
