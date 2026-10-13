using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

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
}
