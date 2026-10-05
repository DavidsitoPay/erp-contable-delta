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
}
