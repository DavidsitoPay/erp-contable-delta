using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record CuentaReporte(int CuentaId, string Codigo, string Nombre, int Nivel, bool EsHoja, decimal Saldo);

public sealed record SeccionReporte(IReadOnlyList<CuentaReporte> Cuentas, decimal Total);

public sealed record BalanceGeneralCalculado(
    SeccionReporte Activo,
    SeccionReporte Pasivo,
    SeccionReporte Capital,
    decimal ResultadoEjercicio,
    decimal TotalCapital,
    decimal TotalPasivoCapital,
    decimal Diferencia,
    bool Cuadra);

public sealed record EstadoResultadosCalculado(SeccionReporte Ingresos, SeccionReporte Gastos, decimal UtilidadNeta);

public static class EstadosFinancieros
{
    private const int NivelRaiz = 1;

    public static SeccionReporte Seccionar(IEnumerable<SaldoReporteCuenta> filas, string tipo)
    {
        var cuentas = filas
            .Where(f => f.Tipo == tipo)
            .OrderBy(f => f.Codigo, StringComparer.Ordinal)
            .Select(f => new CuentaReporte(f.CuentaId, f.Codigo, f.Nombre, f.Nivel, f.EsHoja, f.Saldo))
            .ToList();
        return new SeccionReporte(cuentas, cuentas.Where(c => c.Nivel == NivelRaiz).Sum(c => c.Saldo));
    }

    public static BalanceGeneralCalculado BalanceGeneral(IReadOnlyCollection<SaldoReporteCuenta> filas)
    {
        var activo = Seccionar(filas, "Activo");
        var pasivo = Seccionar(filas, "Pasivo");
        var capital = Seccionar(filas, "Capital");
        var resultado = EstadoResultados(filas).UtilidadNeta;
        var totalCapital = capital.Total + resultado;
        var totalPasivoCapital = pasivo.Total + totalCapital;
        var diferencia = activo.Total - totalPasivoCapital;
        return new BalanceGeneralCalculado(activo, pasivo, capital, resultado, totalCapital, totalPasivoCapital, diferencia, diferencia == 0);
    }

    public static EstadoResultadosCalculado EstadoResultados(IReadOnlyCollection<SaldoReporteCuenta> filas)
    {
        var ingresos = Seccionar(filas, "Ingreso");
        var gastos = Seccionar(filas, "Gasto");
        return new EstadoResultadosCalculado(ingresos, gastos, ingresos.Total - gastos.Total);
    }
}
